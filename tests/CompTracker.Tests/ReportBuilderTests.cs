using CompTracker.Data;
using CompTracker.Data.Entities;
using CompTracker.Services;

namespace CompTracker.Tests;

public class ReportBuilderTests
{
    private static readonly List<PlacePoints> DefaultTable = AppDbContext.DefaultPlacePoints().ToList();

    private static Player P(int id, string name) => new() { Id = id, Name = name };

    private static Score S(Player p, decimal value) => new() { PlayerId = p.Id, Player = p, Value = value };

    // A: 1st Speedrun, 2nd Tetris (tied with B). B: 2nd Speedrun, 2nd Tetris. C: 1st Tetris, skipped Speedrun.
    private static TournamentReport Sample()
    {
        Player a = P(1, "A"), b = P(2, "B"), c = P(3, "C");
        var speedrun = new Game { Id = 1, Title = "Speedrun", ScoringType = ScoringType.TimeLowest, Scores = [S(a, 60_000), S(b, 60_400)] };
        var tetris = new Game { Id = 2, Title = "Tetris", ScoringType = ScoringType.ScoreHighest, Scores = [S(c, 9000), S(a, 5000), S(b, 5000)] };
        return ReportBuilder.Build([a, b, c], [speedrun, tetris], DefaultTable);
    }

    private static PlayerReport For(TournamentReport r, string name) => r.Players.Single(p => p.Row.PlayerName == name);

    [Fact]
    public void Build_OrdersPlayersByLeaderboard()
    {
        var report = Sample();
        Assert.Equal(["A", "B", "C"], report.Players.Select(p => p.Row.PlayerName));
        Assert.Equal(10, report.MaxPointsPerGame);
    }

    [Fact]
    public void PlayerReport_HasResultForEveryGame_IncludingUnplayed()
    {
        var c = For(Sample(), "C");
        Assert.Equal(2, c.Results.Count);
        Assert.False(c.Results.Single(r => r.Game.Title == "Speedrun").Played);
        Assert.Equal(1, c.Results.Single(r => r.Game.Title == "Tetris").Standing!.Place);
    }

    [Fact]
    public void PlayerReport_HeadlineStats()
    {
        var a = For(Sample(), "A");
        Assert.Equal(19, a.Row.TotalPoints);
        Assert.Equal(2, a.Podiums);
        Assert.Equal(1.5, a.AveragePlace);
        Assert.Equal("Speedrun", a.BestGame!.Game.Title);
    }

    [Fact]
    public void ClosestCall_IsGapToPlayerDirectlyAhead()
    {
        var b = For(Sample(), "B");
        // B was 0.4s behind A in Speedrun (relative 0.0067) vs 4000 behind C in Tetris (relative 0.44).
        Assert.Equal("Speedrun", b.ClosestCall!.Result.Game.Title);
        Assert.Equal(400m, b.ClosestCall.Gap);
        Assert.Equal("A", b.ClosestCall.Result.Ahead!.PlayerName);
    }

    [Fact]
    public void TiedPlayersAreNotAheadOfEachOther()
    {
        var a = For(Sample(), "A");
        // A tied B for 2nd in Tetris; the player ahead is C, not B.
        Assert.Equal("C", a.Results.Single(r => r.Game.Title == "Tetris").Ahead!.PlayerName);
    }

    [Fact]
    public void Rival_IsMostFrequentPlayerJustAhead()
    {
        var b = For(Sample(), "B");
        Assert.Equal("A", b.Rival!.PlayerName); // A once, C once -> tie broken by name
        Assert.Equal(1, b.Rival.Times);
    }

    [Fact]
    public void NextAboveAndBelow_SkipEqualTotals()
    {
        var report = Sample();
        var a = For(report, "A");
        var b = For(report, "B");
        var c = For(report, "C");

        Assert.Null(a.NextAbove);
        Assert.Equal("B", a.NextBelow!.PlayerName);  // 19 vs 18
        Assert.Equal("A", b.NextAbove!.PlayerName);
        Assert.Equal("C", b.NextBelow!.PlayerName);  // 18 vs 10
        Assert.Equal("B", c.NextAbove!.PlayerName);
        Assert.Null(c.NextBelow);
    }

    [Fact]
    public void PlayerWithNoGames_HasEmptyHighlights()
    {
        Player a = P(1, "A"), idle = P(2, "Idle");
        var g = new Game { Id = 1, Title = "G", ScoringType = ScoringType.ScoreHighest, Scores = [S(a, 1)] };
        var r = For(ReportBuilder.Build([a, idle], [g], DefaultTable), "Idle");

        Assert.Null(r.BestGame);
        Assert.Null(r.AveragePlace);
        Assert.Null(r.ClosestCall);
        Assert.Null(r.Rival);
        Assert.Equal(0, r.Podiums);
    }

    [Fact]
    public void Build_LeavesOutForFunGames()
    {
        Player a = P(1, "A"), b = P(2, "B");
        var real = new Game { Id = 1, Title = "Real", ScoringType = ScoringType.ScoreHighest, Scores = [S(a, 20), S(b, 10)] };
        var fun = new Game { Id = 2, Title = "Fun", ScoringType = ScoringType.ScoreHighest, ForFun = true, Scores = [S(b, 99), S(a, 1)] };

        var report = ReportBuilder.Build([a, b], [real, fun], DefaultTable);

        Assert.Equal(["Real"], report.Games.Select(g => g.Title));
        Assert.Equal(2, For(report, "B").BestGame!.Standing!.Place); // B won the fun game, but that doesn't count
    }

    [Fact]
    public void BuildRecent_NewestFirst_WithCurrentPlace_AndLimit()
    {
        Player a = P(1, "A"), b = P(2, "B");
        var t0 = new DateTime(2026, 11, 28, 18, 0, 0, DateTimeKind.Utc);
        Score At(Player p, decimal v, int minutes) => new() { PlayerId = p.Id, Player = p, Value = v, UpdatedAt = t0.AddMinutes(minutes) };
        var g1 = new Game { Id = 1, Title = "G1", ScoringType = ScoringType.ScoreHighest, Scores = [At(a, 10, 0), At(b, 20, 30)] };
        var g2 = new Game { Id = 2, Title = "G2", ScoringType = ScoringType.TimeLowest, Scores = [At(a, 5000, 45)] };
        var summaries = ReportBuilder.BuildGameSummaries([a, b], [g1, g2], DefaultTable);

        var recent = ReportBuilder.BuildRecent(summaries, 2);

        Assert.Equal([("A", "G2", 1), ("B", "G1", 1)], recent.Select(r => (r.Standing.PlayerName, r.Game.Title, r.Standing.Place)));
    }

    [Theory]
    [InlineData(ScoringType.TimeLowest, 400, "0.4s")]
    [InlineData(ScoringType.TimeLowest, 12_000, "12s")]
    [InlineData(ScoringType.TimeLowest, 75_500, "1:15.500")]
    [InlineData(ScoringType.ScoreHighest, 1250, "1,250")]
    public void FormatGap(ScoringType type, long gap, string expected) =>
        Assert.Equal(expected, ScoreFormat.FormatGap(type, gap));
}
