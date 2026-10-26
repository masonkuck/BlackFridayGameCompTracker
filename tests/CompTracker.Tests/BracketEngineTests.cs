using CompTracker.Data.Entities;
using CompTracker.Services;

namespace CompTracker.Tests;

public class BracketEngineTests
{
    private static readonly DateTime T0 = new(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);

    // Plays the bracket to the end, picking each ready match's winner with the given rule.
    private static (BracketState State, Dictionary<string, BracketResultInput> Results) PlayOut(
        List<int> slots, Func<BracketMatch, int> pickWinner)
    {
        var results = new Dictionary<string, BracketResultInput>();
        var state = BracketEngine.Build(slots, results);
        for (var guard = 0; !state.Finished; guard++)
        {
            Assert.True(guard < 200, "bracket never finished");
            var match = Assert.Single(state.Ready.Take(1));
            results[match.Key] = new BracketResultInput(pickWinner(match), T0.AddMinutes(guard));
            state = BracketEngine.Build(slots, results);
        }
        return (state, results);
    }

    private static List<int> Players(int n) => Enumerable.Range(1, n).ToList();

    [Fact]
    public void SeedOrder_StandardBracket() =>
        Assert.Equal([1, 8, 4, 5, 2, 7, 3, 6], BracketEngine.SeedOrder(8));

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(5, 8)]
    [InlineData(8, 8)]
    [InlineData(9, 16)]
    public void SizeFor_NextPowerOfTwo(int players, int size) => Assert.Equal(size, BracketEngine.SizeFor(players));

    [Fact]
    public void Seed_ByesGoToTopSeeds_AndNeverMeetEachOther()
    {
        var slots = BracketEngine.Seed(Players(5));
        Assert.Equal(8, slots.Count);
        Assert.Equal(3, slots.Count(id => id == 0));
        for (var i = 0; i < slots.Count; i += 2)
            Assert.False(slots[i] == 0 && slots[i + 1] == 0);
        // Seeds 1-3 get the byes.
        foreach (var top in new[] { 1, 2, 3 })
        {
            var at = slots.IndexOf(top);
            Assert.Equal(0, slots[at ^ 1]);
        }
    }

    [Fact]
    public void RandomSeed_KeepsEveryone()
    {
        var slots = BracketEngine.RandomSeed(Players(6), new Random(7));
        Assert.Equal(Players(6), slots.Where(id => id != 0).Order());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(11)]
    [InlineData(16)]
    [InlineData(17)]
    public void PlaysOut_EveryoneLosesTwiceExceptTheChampion(int n)
    {
        foreach (var seed in new[] { 1, 2, 3 })
        {
            var random = new Random(seed);
            var (state, _) = PlayOut(BracketEngine.RandomSeed(Players(n), random),
                m => random.Next(2) == 0 ? m.A.PlayerId : m.B.PlayerId);

            var losses = state.Matches.Where(m => m.Decided).GroupBy(m => m.LoserId!.Value).ToDictionary(g => g.Key, g => g.Count());
            foreach (var id in Players(n))
            {
                var expected = id == state.ChampionId ? losses.GetValueOrDefault(id) : 2;
                Assert.Equal(expected, losses.GetValueOrDefault(id));
            }
            Assert.True(losses.GetValueOrDefault(state.ChampionId!.Value) <= 1);

            // Everyone gets exactly one place, and the places follow competition ranking.
            Assert.Equal(Players(n), state.Placements.Select(p => p.PlayerId).Order());
            var places = state.Placements.Select(p => p.Place).Order().ToList();
            for (var i = 0; i < places.Count; i++)
                Assert.True(places[i] == i + 1 || places[i] == places[i - 1], $"place list {string.Join(",", places)}");
            Assert.Equal(1, places.Count(p => p == 1));
            Assert.Equal(1, places.Count(p => p == 2));
        }
    }

    [Fact]
    public void EightPlayers_StandardPlaces()
    {
        var (state, _) = PlayOut(BracketEngine.Seed(Players(8)), m => Math.Min(m.A.PlayerId, m.B.PlayerId));
        Assert.Equal([1, 2, 3, 4, 5, 5, 7, 7], state.Placements.Select(p => p.Place).Order());
        Assert.Equal(1, state.ChampionId);
    }

    [Fact]
    public void ThreePlayers_OneByeStillGivesThreePlaces()
    {
        var (state, _) = PlayOut(BracketEngine.Seed(Players(3)), m => Math.Min(m.A.PlayerId, m.B.PlayerId));
        Assert.Equal([1, 2, 3], state.Placements.OrderBy(p => p.Place).Select(p => p.Place));
        Assert.Equal([1, 2, 3], state.Placements.OrderBy(p => p.Place).Select(p => p.PlayerId));
    }

    [Fact]
    public void ByeMatches_AreNeverReady()
    {
        var state = BracketEngine.Build(BracketEngine.Seed(Players(5)), new Dictionary<string, BracketResultInput>());
        Assert.All(state.Ready, m => Assert.False(m.HasBye));
        // 4 v 5, and 2 v 3 since both of them had byes.
        Assert.Equal(["W1-2", "W2-2"], state.Ready.Select(m => m.Key));
    }

    [Fact]
    public void GrandFinal_ResetWhenLosersChampionWinsFirst()
    {
        // Two players: 1 beats 2, then 2 wins the grand final, forcing a reset that 2 also wins.
        var slots = new List<int> { 1, 2 };
        var results = new Dictionary<string, BracketResultInput>
        {
            ["W1-1"] = new(1, T0),
            ["GF1"] = new(2, T0.AddMinutes(1)),
        };
        var state = BracketEngine.Build(slots, results);
        Assert.False(state.Finished);
        var reset = Assert.Single(state.Ready);
        Assert.Equal("GF2", reset.Key);

        results["GF2"] = new(2, T0.AddMinutes(2));
        state = BracketEngine.Build(slots, results);
        Assert.Equal(2, state.ChampionId);
        Assert.Equal([(2, 1), (1, 2)], state.Placements.Select(p => (p.PlayerId, p.Place)));
    }

    [Fact]
    public void GrandFinal_NoResetWhenWinnersChampionWins()
    {
        var state = BracketEngine.Build([1, 2], new Dictionary<string, BracketResultInput>
        {
            ["W1-1"] = new(1, T0),
            ["GF1"] = new(1, T0.AddMinutes(1)),
        });
        Assert.Equal(1, state.ChampionId);
        Assert.DoesNotContain(state.Matches, m => m.Key == "GF2");
    }

    [Fact]
    public void ChangingAnEarlyResult_DropsTheResultsThatDependedOnIt()
    {
        var slots = BracketEngine.Seed(Players(4)); // 1 v 4, 2 v 3
        var results = new Dictionary<string, BracketResultInput>
        {
            ["W1-1"] = new(1, T0),
            ["W1-2"] = new(2, T0),
            ["W2-1"] = new(1, T0.AddMinutes(1)),
        };
        Assert.Contains("W2-1", BracketEngine.Build(slots, results).UsedResults);

        results["W1-1"] = new(4, T0.AddMinutes(2)); // the first match actually went the other way
        var state = BracketEngine.Build(slots, results);
        Assert.DoesNotContain("W2-1", state.UsedResults);
        Assert.Contains(state.Ready, m => m.Key == "W2-1" && m.A.PlayerId == 4 && m.B.PlayerId == 2);
    }

    [Fact]
    public void PlacesAreKnownAsSoonAsSomeoneIsOut()
    {
        // 8 players: losing in losers round 1 means 7th, even with the rest of the bracket unplayed.
        var slots = BracketEngine.Seed(Players(8)); // W1: 1v8, 4v5, 2v7, 3v6
        var results = new Dictionary<string, BracketResultInput>
        {
            ["W1-1"] = new(1, T0), ["W1-2"] = new(4, T0), ["W1-3"] = new(2, T0), ["W1-4"] = new(3, T0),
            ["L1-1"] = new(8, T0.AddMinutes(5)),
        };
        var state = BracketEngine.Build(slots, results);
        var out1 = Assert.Single(state.Placements);
        Assert.Equal((5, 7, T0.AddMinutes(5)), (out1.PlayerId, out1.Place, out1.DecidedAt));
    }

    [Fact]
    public void RoundNames_SkipLosersRoundsThatAreAllByes()
    {
        // Six players: losers round 1 is only byes, so the first one people play is called round 1.
        var state = BracketEngine.Build(BracketEngine.Seed(Players(6)), new Dictionary<string, BracketResultInput>());
        var played = state.Matches.Where(m => m.Side == BracketSide.Losers && !m.HasBye).Select(state.RoundName).Distinct();
        Assert.Equal(["Losers round 1", "Losers round 2", "Losers final"], played);
        Assert.Equal("Winners semifinal", state.RoundName(state.Matches.First(m => m.Key == "W2-1")));
        Assert.Equal("Grand final", state.RoundName(state.Matches.First(m => m.Key == "GF1")));
    }

    [Fact]
    public void Validate_RejectsBadSlots()
    {
        Assert.NotNull(BracketEngine.Validate([1, 2, 3]));     // not a power of two
        Assert.NotNull(BracketEngine.Validate([1, 0, 0, 0]));  // one player
        Assert.NotNull(BracketEngine.Validate([1, 2, 1, 0]));  // duplicate
        Assert.Null(BracketEngine.Validate([1, 0, 2, 3]));
    }

    [Fact]
    public void Text_RoundTrips() =>
        Assert.Equal([3, 0, 7, 1], BracketEngine.Parse(BracketEngine.ToText([3, 0, 7, 1])));

    [Fact]
    public void Rank_UsesBracketPlacesAsIs()
    {
        var ann = new Player { Id = 1, Name = "Ann" };
        var bob = new Player { Id = 2, Name = "Bob" };
        var cat = new Player { Id = 3, Name = "Cat" };
        var table = AppDbContextDefaults();
        var standings = ScoringService.Rank(ScoringType.Bracket,
        [
            new Score { PlayerId = 3, Player = cat, Value = 5 },
            new Score { PlayerId = 1, Player = ann, Value = 5 },
            new Score { PlayerId = 2, Player = bob, Value = 7 },
        ], table);
        // Only knocked-out players have places so far; they keep their real place, not 1st/2nd among themselves.
        Assert.Equal([("Ann", 5), ("Cat", 5), ("Bob", 7)], standings.Select(s => (s.PlayerName, s.Place)));
        Assert.Equal([6, 6, 4], standings.Select(s => s.Points));
    }

    [Fact]
    public void Celebrations_CrowningABracketChampionCounts()
    {
        var before = new LeaderSnapshot([new Standing(2, "Bob", 3, 3, 8, T0)], []);
        var after = new LeaderSnapshot([new Standing(1, "Ann", 1, 1, 10, T0), new Standing(3, "Cat", 2, 2, 9, T0), new Standing(2, "Bob", 3, 3, 8, T0)], []);
        var c = Assert.Single(Celebrations.Detect(before, after, "Smash", ScoringType.Bracket, gameIsActive: true));
        Assert.Equal(("Ann", CelebrationKind.GameLeader), (c.PlayerName, c.Kind));
        Assert.Empty(c.Dethroned);
    }

    private static List<PlacePoints> AppDbContextDefaults() => CompTracker.Data.AppDbContext.DefaultPlacePoints().ToList();
}
