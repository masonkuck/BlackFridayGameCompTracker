using CompTracker.Data;
using CompTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CompTracker.Services;

// Changes to a bracket game: drawing and rearranging it, locking it in, and recording or undoing match winners.
// Every change notifies the dashboard; recording a winner can also crown a champion or a new overall leader.
public class BracketAdmin(IDbContextFactory<AppDbContext> dbFactory, ScoringService scoring, ChangeNotifier notifier)
{
    // A fresh random draw of all active players. Only while the bracket isn't locked in.
    public async Task<string?> DrawAsync(int gameId, Random? random = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var players = await db.Players.Where(p => p.IsActive).Select(p => p.Id).ToListAsync();
        if (players.Count < BracketEngine.MinPlayers)
            return "A bracket needs at least two active players.";
        return await SetSlotsAsync(gameId, BracketEngine.RandomSeed(players, random ?? Random.Shared));
    }

    // Saves rearranged matchups. Only while the bracket isn't locked in.
    public async Task<string?> SetSlotsAsync(int gameId, IReadOnlyList<int> slots)
    {
        if (BracketEngine.Validate(slots) is { } problem)
            return problem;
        await using var db = await dbFactory.CreateDbContextAsync();
        var game = await db.Games.Include(g => g.BracketResults).FirstOrDefaultAsync(g => g.Id == gameId);
        if (game is null)
            return "That game doesn't exist anymore.";
        if (game.BracketLocked || game.BracketResults.Count > 0)
            return "The bracket is locked in. Unlock it to change the matchups.";

        game.BracketSlots = BracketEngine.ToText(slots);
        await db.SaveChangesAsync();
        notifier.Notify();
        return null;
    }

    // Lock in freezes the matchups so results can be entered; unlocking is only possible before any results.
    public async Task<string?> SetLockedAsync(int gameId, bool locked)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var game = await db.Games.Include(g => g.BracketResults).FirstOrDefaultAsync(g => g.Id == gameId);
        if (game is null)
            return "That game doesn't exist anymore.";
        if (locked && BracketEngine.Validate(BracketEngine.Parse(game.BracketSlots)) is { } problem)
            return problem;
        if (!locked && game.BracketResults.Count > 0)
            return "Results are already in. Reset the results before unlocking the matchups.";

        game.BracketLocked = locked;
        await db.SaveChangesAsync();
        notifier.Notify();
        return null;
    }

    // Removes the bracket entirely (matchups and results).
    public async Task RemoveAsync(int gameId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.BracketResults.Where(r => r.GameId == gameId).ExecuteDeleteAsync();
        await db.Games.Where(g => g.Id == gameId).ExecuteUpdateAsync(s => s
            .SetProperty(g => g.BracketSlots, (string?)null)
            .SetProperty(g => g.BracketLocked, false));
        notifier.Notify();
    }

    // Clears every result but keeps the matchups (still locked in).
    public async Task ResetResultsAsync(int gameId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.BracketResults.Where(r => r.GameId == gameId).ExecuteDeleteAsync();
        notifier.Notify();
    }

    // How many later results would be thrown out if this match's winner changed (or was cleared, with null).
    public async Task<int> ResultsLostIfAsync(int gameId, string matchKey, int? winnerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var game = await db.Games.AsNoTracking().Include(g => g.BracketResults).FirstOrDefaultAsync(g => g.Id == gameId);
        if (game is null)
            return 0;
        var results = Inputs(game.BracketResults);
        var others = results.Keys.Where(k => k != matchKey).ToList();
        Apply(results, matchKey, winnerId, DateTime.UtcNow);
        var used = BracketEngine.Build(BracketEngine.Parse(game.BracketSlots), results).UsedResults;
        return others.Count(k => !used.Contains(k));
    }

    public Task<string?> SetWinnerAsync(int gameId, string matchKey, int winnerId) => ChangeAsync(gameId, matchKey, winnerId);

    public Task<string?> ClearResultAsync(int gameId, string matchKey) => ChangeAsync(gameId, matchKey, null);

    private async Task<string?> ChangeAsync(int gameId, string matchKey, int? winnerId)
    {
        var before = await scoring.GetLeaderSnapshotAsync(gameId);

        await using var db = await dbFactory.CreateDbContextAsync();
        var game = await db.Games.Include(g => g.BracketResults).FirstOrDefaultAsync(g => g.Id == gameId);
        if (game is null)
            return "That game doesn't exist anymore.";
        var slots = BracketEngine.Parse(game.BracketSlots);
        if (BracketEngine.Validate(slots) is { } problem)
            return problem;
        if (!game.BracketLocked)
            return "Lock in the bracket before picking winners.";

        if (winnerId is { } id)
        {
            var match = BracketEngine.Build(slots, Inputs(game.BracketResults)).Matches.FirstOrDefault(m => m.Key == matchKey);
            if (match is null || !match.A.IsPlayer || !match.B.IsPlayer)
                return "That match isn't ready to be played yet.";
            if (id != match.A.PlayerId && id != match.B.PlayerId)
                return "That player isn't in this match.";
        }

        var existing = game.BracketResults.FirstOrDefault(r => r.MatchKey == matchKey);
        if (winnerId is null)
        {
            if (existing is not null)
                db.BracketResults.Remove(existing);
        }
        else if (existing is null)
            db.BracketResults.Add(new BracketResult { GameId = gameId, MatchKey = matchKey, WinnerId = winnerId.Value });
        else if (existing.WinnerId != winnerId)
        {
            existing.WinnerId = winnerId.Value;
            existing.DecidedAt = DateTime.UtcNow;
        }

        // Later results that no longer fit (their match now has different players) are thrown out.
        var remaining = game.BracketResults.Where(r => r.MatchKey != matchKey).ToList();
        var inputs = Inputs(remaining);
        Apply(inputs, matchKey, winnerId, DateTime.UtcNow);
        var used = BracketEngine.Build(slots, inputs).UsedResults;
        db.BracketResults.RemoveRange(remaining.Where(r => !used.Contains(r.MatchKey)));

        await db.SaveChangesAsync();

        var after = await scoring.GetLeaderSnapshotAsync(gameId);
        notifier.Notify(new ScorePosted(gameId, Celebrations.Detect(before, after, game.Title, game.ScoringType, game.IsActive)));
        return null;
    }

    private static Dictionary<string, BracketResultInput> Inputs(IEnumerable<BracketResult> results) =>
        results.ToDictionary(r => r.MatchKey, r => new BracketResultInput(r.WinnerId, r.DecidedAt));

    private static void Apply(Dictionary<string, BracketResultInput> results, string key, int? winnerId, DateTime at)
    {
        if (winnerId is null)
            results.Remove(key);
        else
            results[key] = new BracketResultInput(winnerId.Value, at);
    }
}
