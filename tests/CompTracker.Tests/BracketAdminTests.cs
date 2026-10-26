using CompTracker.Data;
using CompTracker.Data.Entities;
using CompTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CompTracker.Tests;

// Runs against a real SQLite file: the bracket lives in the database (seeding on the game, winners in BracketResults).
public sealed class BracketAdminTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "comptracker-tests-" + Guid.NewGuid().ToString("N"));
    private readonly IDbContextFactory<AppDbContext> factory;
    private readonly ChangeNotifier notifier = new();
    private readonly BracketAdmin admin;
    private readonly ScoringService scoring;
    private readonly int gameId;

    public BracketAdminTests()
    {
        Directory.CreateDirectory(root);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "live.db")};Pooling=False")
            .Options;
        factory = new PooledDbContextFactory<AppDbContext>(options);
        using var db = factory.CreateDbContext();
        db.Database.Migrate();
        // Ids 1-4 in name order.
        foreach (var name in new[] { "Ann", "Bob", "Cat", "Dan" })
            db.Players.Add(new Player { Name = name });
        var game = new Game { Title = "Smash", ScoringType = ScoringType.Bracket };
        db.Games.Add(game);
        db.SaveChanges();
        gameId = game.Id;

        scoring = new ScoringService(factory);
        admin = new BracketAdmin(factory, scoring, notifier);
    }

    // 1 v 4 and 2 v 3.
    private Task<string?> SeedAsync() => admin.SetSlotsAsync(gameId, [1, 4, 2, 3]);

    [Fact]
    public async Task Draw_PutsEveryActivePlayerIn()
    {
        Assert.Null(await admin.DrawAsync(gameId, new Random(1)));
        var view = await scoring.GetBracketAsync(gameId);
        Assert.Equal([1, 2, 3, 4], view!.State!.Entrants.Order());
    }

    [Fact]
    public async Task Winners_TurnIntoPlacesOnTheLeaderboard()
    {
        await SeedAsync();
        // Ann and Bob win round 1; Cat beats Dan in the losers bracket, so Dan is out in 4th.
        await admin.SetWinnerAsync(gameId, "W1-1", 1);
        await admin.SetWinnerAsync(gameId, "W1-2", 2);
        await admin.SetWinnerAsync(gameId, "L1-1", 3);

        var standings = await scoring.GetGameStandingsAsync(gameId);
        var dan = Assert.Single(standings);
        Assert.Equal(("Dan", 4, 7), (dan.PlayerName, dan.Place, dan.Points));

        var board = await scoring.GetLeaderboardAsync();
        Assert.Equal(7, board.Single(r => r.PlayerName == "Dan").TotalPoints);
        // Still in the bracket = still to play.
        var todo = await scoring.GetTodoAsync();
        Assert.Contains("Smash", todo.Single(t => t.PlayerName == "Ann").RemainingGames);
        Assert.DoesNotContain("Smash", todo.Single(t => t.PlayerName == "Dan").RemainingGames);
    }

    [Fact]
    public async Task ChangingAWinner_ClearsResultsThatDependedOnIt()
    {
        await SeedAsync();
        await admin.SetWinnerAsync(gameId, "W1-1", 1);
        await admin.SetWinnerAsync(gameId, "W1-2", 2);
        await admin.SetWinnerAsync(gameId, "W2-1", 1);

        Assert.Equal(1, await admin.ResultsLostIfAsync(gameId, "W1-1", 4));
        Assert.Equal(0, await admin.ResultsLostIfAsync(gameId, "W2-1", null));

        await admin.SetWinnerAsync(gameId, "W1-1", 4);

        await using var db = await factory.CreateDbContextAsync();
        var keys = await db.BracketResults.Select(r => r.MatchKey).OrderBy(k => k).ToListAsync();
        Assert.Equal(["W1-1", "W1-2"], keys);
    }

    [Fact]
    public async Task MatchupsLockOnceResultsAreIn()
    {
        await SeedAsync();
        await admin.SetWinnerAsync(gameId, "W1-1", 1);
        Assert.NotNull(await admin.SetSlotsAsync(gameId, [1, 3, 2, 4]));

        await admin.ResetResultsAsync(gameId);
        Assert.Null(await admin.SetSlotsAsync(gameId, [1, 3, 2, 4]));
    }

    [Fact]
    public async Task RejectsWinnersWhoArentInTheMatch()
    {
        await SeedAsync();
        Assert.NotNull(await admin.SetWinnerAsync(gameId, "W1-1", 2));   // Bob plays in the other match
        Assert.NotNull(await admin.SetWinnerAsync(gameId, "W2-1", 1));   // not ready yet
    }

    [Fact]
    public async Task CrowningTheChampion_Celebrates()
    {
        await SeedAsync();
        ScorePosted? last = null;
        notifier.ScorePosted += p => last = p;
        foreach (var (key, winner) in new[] { ("W1-1", 1), ("W1-2", 2), ("L1-1", 3), ("W2-1", 1), ("L2-1", 3) })
            await admin.SetWinnerAsync(gameId, key, winner);
        Assert.DoesNotContain(last!.Celebrations, c => c.Kind == CelebrationKind.GameLeader);

        await admin.SetWinnerAsync(gameId, "GF1", 1);
        var c = Assert.Single(last!.Celebrations, c => c.Kind == CelebrationKind.GameLeader);
        Assert.Equal("Ann", c.PlayerName);
    }

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
    }
}
