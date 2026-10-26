namespace CompTracker.Services;

// One side of a match: a known player, a bye (nobody will ever fill it), or someone still to be decided
// by an earlier match. Pending is always a real player eventually: byes are fixed by the seeding alone.
public readonly record struct Slot(SlotKind Kind, int PlayerId = 0)
{
    public static readonly Slot Bye = new(SlotKind.Bye);
    public static readonly Slot Pending = new(SlotKind.Pending);
    public static Slot Of(int playerId) => new(SlotKind.Player, playerId);

    public bool IsPlayer => Kind == SlotKind.Player;
    public bool IsBye => Kind == SlotKind.Bye;
}

public enum SlotKind { Player, Bye, Pending }

public enum BracketSide { Winners, Losers, Final }

public record BracketMatch(string Key, BracketSide Side, int Round, int Index, Slot A, Slot B, int? WinnerId, DateTime? DecidedAt)
{
    // Both players known and nobody has won yet: this is a match that can be played now.
    public bool Ready => A.IsPlayer && B.IsPlayer && WinnerId is null;
    public bool Decided => WinnerId is not null;
    public bool HasBye => A.IsBye || B.IsBye;
    public int? LoserId => WinnerId is null ? null : WinnerId == A.PlayerId ? B.PlayerId : A.PlayerId;
}

public record BracketPlacement(int PlayerId, int Place, DateTime DecidedAt);

public record BracketResultInput(int WinnerId, DateTime DecidedAt);

public record BracketState(
    int Size,
    IReadOnlyList<int> Slots,
    IReadOnlyList<BracketMatch> Matches,
    IReadOnlyList<BracketPlacement> Placements,
    int? ChampionId,
    IReadOnlySet<string> UsedResults)
{
    public IEnumerable<int> Entrants => Slots.Where(id => id != 0);
    public IEnumerable<BracketMatch> Ready => Matches.Where(m => m.Ready);
    public bool Started => UsedResults.Count > 0;
    public bool Finished => ChampionId is not null;

    // Number of rounds on each side, for naming ("Winners final", "Losers round 2").
    public int WinnersRounds => Matches.Where(m => m.Side == BracketSide.Winners).Max(m => m.Round);
    public int LosersRounds => Matches.Where(m => m.Side == BracketSide.Losers).Select(m => m.Round).DefaultIfEmpty(0).Max();

    public string RoundName(BracketMatch m) => m.Side switch
    {
        BracketSide.Final => m.Round == 1 ? "Grand final" : "Grand final reset",
        BracketSide.Winners when m.Round == WinnersRounds => "Winners final",
        BracketSide.Winners when m.Round == WinnersRounds - 1 && m.Round > 1 => "Winners semifinal",
        BracketSide.Winners => $"Winners round {m.Round}",
        _ when m.Round == LosersRounds => "Losers final",
        _ => $"Losers round {PlayedLosersRound(m.Round)}",
    };

    // With byes, early losers rounds can be nothing but byes; number the rounds people actually play.
    private int PlayedLosersRound(int round) =>
        Matches.Where(m => m.Side == BracketSide.Losers && m.Round <= round && !m.HasBye).Select(m => m.Round).Distinct().Count();
}

// Double elimination. The seeding is a list of 2^k slots (player id, or 0 for a bye) where slots 2i and 2i+1
// meet in the first round. Everything else (who plays whom, who is out, final places) is worked out from the
// seeding plus the stored winners, so a result can be changed or undone and the rest of the bracket follows.
//
// Losers bracket: round 1 pairs off the first-round losers; after that it alternates between a "drop" round
// (survivors meet the players who just lost in the winners bracket, in reverse order to avoid instant rematches)
// and a round where the survivors play each other. The grand final is winners champion vs losers champion,
// with a reset match if the losers champion wins the first one (both then have one loss).
public static class BracketEngine
{
    public const int MinPlayers = 2;

    public static int SizeFor(int players)
    {
        var size = 2;
        while (size < players)
            size *= 2;
        return size;
    }

    // Puts players (in seed order) into bracket slots so that byes go to the top seeds and the top two seeds
    // can only meet in the final. Call with a shuffled list for a random draw.
    public static List<int> Seed(IReadOnlyList<int> playerIds)
    {
        var size = SizeFor(playerIds.Count);
        return SeedOrder(size).Select(seed => seed <= playerIds.Count ? playerIds[seed - 1] : 0).ToList();
    }

    public static List<int> RandomSeed(IEnumerable<int> playerIds, Random random)
    {
        var list = playerIds.ToList();
        random.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list));
        return Seed(list);
    }

    // Seed numbers by slot for a bracket of the given size: 1 v 8, 4 v 5, 2 v 7, 3 v 6 for eight, and so on.
    public static List<int> SeedOrder(int size)
    {
        var order = new List<int> { 1, 2 };
        while (order.Count < size)
        {
            var n = order.Count * 2;
            order = order.SelectMany(s => new[] { s, n + 1 - s }).ToList();
        }
        return order;
    }

    public static string ToText(IEnumerable<int> slots) => string.Join(",", slots);

    public static List<int> Parse(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(',').Select(s => int.TryParse(s, out var id) ? id : 0).ToList();

    // Slot lists that can be played: a power of two, at least two real players, nobody twice.
    public static string? Validate(IReadOnlyList<int> slots)
    {
        if (slots.Count < 2 || (slots.Count & (slots.Count - 1)) != 0)
            return "The bracket size must be a power of two.";
        var players = slots.Where(id => id != 0).ToList();
        if (players.Count < MinPlayers)
            return "A bracket needs at least two players.";
        if (players.Distinct().Count() != players.Count)
            return "A player is in the bracket twice.";
        return null;
    }

    public static BracketState Build(IReadOnlyList<int> slots, IReadOnlyDictionary<string, BracketResultInput> results)
    {
        var size = slots.Count;
        var builder = new Builder(results);
        var k = (int)Math.Log2(size);

        // Winners bracket.
        var winners = new List<List<BracketMatch>>();
        var first = new List<BracketMatch>();
        for (var i = 0; i < size / 2; i++)
            first.Add(builder.Play($"W1-{i + 1}", BracketSide.Winners, 1, i, ToSlot(slots[2 * i]), ToSlot(slots[2 * i + 1])));
        winners.Add(first);
        for (var r = 2; r <= k; r++)
        {
            var prev = winners[^1];
            var round = new List<BracketMatch>();
            for (var i = 0; i < prev.Count / 2; i++)
                round.Add(builder.Play($"W{r}-{i + 1}", BracketSide.Winners, r, i, builder.Winner(prev[2 * i]), builder.Winner(prev[2 * i + 1])));
            winners.Add(round);
        }

        // Losers bracket (none for a two-player bracket: the first loser goes straight to the grand final).
        Slot losersChampion;
        var losersRounds = new List<List<BracketMatch>>();
        if (k == 1)
        {
            losersChampion = builder.Loser(winners[0][0]);
        }
        else
        {
            var lr = 1;
            var round = new List<BracketMatch>();
            var w1 = winners[0];
            for (var i = 0; i < w1.Count / 2; i++)
                round.Add(builder.Play($"L{lr}-{i + 1}", BracketSide.Losers, lr, i, builder.Loser(w1[2 * i]), builder.Loser(w1[2 * i + 1])));
            losersRounds.Add(round);

            for (var j = 2; j <= k; j++)
            {
                // Drop round: survivors vs the players who just lost winners round j.
                lr++;
                var survivors = losersRounds[^1];
                var dropped = winners[j - 1];
                var drop = new List<BracketMatch>();
                for (var i = 0; i < survivors.Count; i++)
                    drop.Add(builder.Play($"L{lr}-{i + 1}", BracketSide.Losers, lr, i,
                        builder.Winner(survivors[i]), builder.Loser(dropped[dropped.Count - 1 - i])));
                losersRounds.Add(drop);

                if (j < k)
                {
                    lr++;
                    var pairs = new List<BracketMatch>();
                    for (var i = 0; i < drop.Count / 2; i++)
                        pairs.Add(builder.Play($"L{lr}-{i + 1}", BracketSide.Losers, lr, i, builder.Winner(drop[2 * i]), builder.Winner(drop[2 * i + 1])));
                    losersRounds.Add(pairs);
                }
            }
            losersChampion = builder.Winner(losersRounds[^1][0]);
        }

        // Grand final, plus the reset if the losers champion takes the first one.
        var winnersChampion = builder.Winner(winners[^1][0]);
        var final1 = builder.Play("GF1", BracketSide.Final, 1, 0, winnersChampion, losersChampion);
        BracketMatch? final2 = null;
        Slot champion;
        if (final1.Decided && final1.WinnerId == losersChampion.PlayerId)
        {
            final2 = builder.Play("GF2", BracketSide.Final, 2, 0, winnersChampion, losersChampion);
            champion = builder.Winner(final2);
        }
        else
        {
            champion = builder.Winner(final1);
        }

        var placements = Places(losersRounds, final1, final2, champion);
        var matches = winners.SelectMany(r => r)
            .Concat(losersRounds.SelectMany(r => r))
            .Append(final1);
        if (final2 is not null)
            matches = matches.Append(final2);

        return new BracketState(size, slots.ToList(), matches.ToList(), placements,
            champion.IsPlayer ? champion.PlayerId : null, builder.Used);
    }

    // Losing in the losers bracket (or the grand final) is a player's second loss, so that's where they finish.
    // Everyone knocked out in the same losers round shares a place (5th-6th, 7th-8th...), and the places are
    // known as soon as they're knocked out because byes, which decide how many lose in each round, are fixed.
    private static List<BracketPlacement> Places(List<List<BracketMatch>> losersRounds, BracketMatch final1, BracketMatch? final2, Slot champion)
    {
        var placements = new List<BracketPlacement>();
        var better = 2; // champion and runner-up
        for (var r = losersRounds.Count - 1; r >= 0; r--)
        {
            var place = better + 1;
            foreach (var m in losersRounds[r].Where(m => !m.HasBye))
            {
                better++;
                if (m.LoserId is { } loser)
                    placements.Add(new BracketPlacement(loser, place, m.DecidedAt!.Value));
            }
        }

        var deciding = final2 ?? final1;
        if (champion.IsPlayer && deciding.LoserId is { } runnerUp && !deciding.HasBye)
        {
            placements.Add(new BracketPlacement(runnerUp, 2, deciding.DecidedAt!.Value));
            placements.Add(new BracketPlacement(champion.PlayerId, 1, deciding.DecidedAt!.Value));
        }
        else if (champion.IsPlayer)
        {
            // Only possible if the grand final was a bye; the champion still finishes 1st.
            placements.Add(new BracketPlacement(champion.PlayerId, 1, deciding.DecidedAt ?? DateTime.MinValue));
        }

        return placements.OrderBy(p => p.Place).ToList();
    }

    private static Slot ToSlot(int id) => id == 0 ? Slot.Bye : Slot.Of(id);

    private sealed class Builder(IReadOnlyDictionary<string, BracketResultInput> results)
    {
        private readonly Dictionary<string, (Slot Winner, Slot Loser)> outcomes = new();

        public HashSet<string> Used { get; } = [];

        public BracketMatch Play(string key, BracketSide side, int round, int index, Slot a, Slot b)
        {
            int? winnerId = null;
            DateTime? at = null;
            (Slot, Slot) outcome;

            if (a.IsBye && b.IsBye)
                outcome = (Slot.Bye, Slot.Bye);
            else if (a.IsBye)
                outcome = (b, Slot.Bye);
            else if (b.IsBye)
                outcome = (a, Slot.Bye);
            else if (a.IsPlayer && b.IsPlayer
                     && results.TryGetValue(key, out var r)
                     && (r.WinnerId == a.PlayerId || r.WinnerId == b.PlayerId))
            {
                winnerId = r.WinnerId;
                at = r.DecidedAt;
                Used.Add(key);
                outcome = r.WinnerId == a.PlayerId ? (a, b) : (b, a);
            }
            else
                outcome = (Slot.Pending, Slot.Pending);

            outcomes[key] = outcome;
            return new BracketMatch(key, side, round, index, a, b, winnerId, at);
        }

        public Slot Winner(BracketMatch m) => outcomes[m.Key].Winner;
        public Slot Loser(BracketMatch m) => outcomes[m.Key].Loser;
    }
}
