using CompTracker.Data;
using CompTracker.Data.Entities;
using CompTracker.Services;

namespace CompTracker.Tests;

public class ScoringServiceTests
{
    private static readonly List<PlacePoints> DefaultTable = AppDbContext.DefaultPlacePoints().ToList();

    private static Player P(int id, string name) => new() { Id = id, Name = name };

    private static Score S(Player p, decimal value) => new() { PlayerId = p.Id, Player = p, Value = value };

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 9)]
    [InlineData(10, 1)]
    [InlineData(11, 1)]
    [InlineData(50, 1)]
    public void PointsForPlace_UsesDefaultTable(int place, int expected) =>
        Assert.Equal(expected, ScoringService.PointsForPlace(place, DefaultTable));

    [Fact]
    public void PointsForPlace_PastEndOfShortList_UsesLastValue()
    {
        var table = new List<PlacePoints> { new() { Place = 1, Points = 5 }, new() { Place = 2, Points = 3 } };
        Assert.Equal(3, ScoringService.PointsForPlace(7, table));
    }

    [Fact]
    public void PointsForPlace_NeverBelowOne()
    {
        Assert.Equal(1, ScoringService.PointsForPlace(1, [new() { Place = 1, Points = 0 }]));
        Assert.Equal(1, ScoringService.PointsForPlace(3, []));
    }

    [Fact]
    public void Rank_Speedrun_LowestTimeWins()
    {
        Player a = P(1, "A"), b = P(2, "B"), c = P(3, "C");
        var standings = ScoringService.Rank(ScoringType.TimeLowest, [S(a, 90_000), S(b, 60_000), S(c, 120_000)], DefaultTable);

        Assert.Equal(["B", "A", "C"], standings.Select(s => s.PlayerName));
        Assert.Equal([1, 2, 3], standings.Select(s => s.Place));
        Assert.Equal([10, 9, 8], standings.Select(s => s.Points));
    }

    [Fact]
    public void Rank_HighScore_HighestWins()
    {
        Player a = P(1, "A"), b = P(2, "B");
        var standings = ScoringService.Rank(ScoringType.ScoreHighest, [S(a, 100), S(b, 500)], DefaultTable);
        Assert.Equal(["B", "A"], standings.Select(s => s.PlayerName));
    }

    [Fact]
    public void Rank_LowScore_LowestWins()
    {
        Player a = P(1, "A"), b = P(2, "B");
        var standings = ScoringService.Rank(ScoringType.ScoreLowest, [S(a, 100), S(b, 500)], DefaultTable);
        Assert.Equal(["A", "B"], standings.Select(s => s.PlayerName));
    }

    [Fact]
    public void Rank_TiesSharePlaceAndSkipNext()
    {
        Player a = P(1, "A"), b = P(2, "B"), c = P(3, "C"), d = P(4, "D");
        var standings = ScoringService.Rank(ScoringType.ScoreHighest, [S(a, 50), S(b, 80), S(c, 80), S(d, 10)], DefaultTable);

        Assert.Equal([1, 1, 3, 4], standings.Select(s => s.Place));
        Assert.Equal([10, 10, 8, 7], standings.Select(s => s.Points));
    }

    [Fact]
    public void BuildLeaderboard_SumsPointsAcrossGames_AndCountsProgress()
    {
        Player a = P(1, "A"), b = P(2, "B");
        var g1 = new Game { Id = 1, Title = "G1", ScoringType = ScoringType.ScoreHighest, Scores = [S(a, 10), S(b, 20)] };
        var g2 = new Game { Id = 2, Title = "G2", ScoringType = ScoringType.TimeLowest, Scores = [S(a, 1000)] };

        var board = ScoringService.BuildLeaderboard([a, b], [g1, g2], DefaultTable);

        Assert.Equal("A", board[0].PlayerName);
        Assert.Equal(19, board[0].TotalPoints); // 9 + 10
        Assert.Equal(2, board[0].GamesDone);
        Assert.Equal(2, board[0].GamesTotal);
        Assert.Equal("B", board[1].PlayerName);
        Assert.Equal(10, board[1].TotalPoints);
        Assert.Equal(1, board[1].GamesDone);
    }

    [Fact]
    public void BuildLeaderboard_IgnoresInactivePlayersScores()
    {
        Player a = P(1, "A"), gone = P(2, "Gone");
        var g = new Game { Id = 1, Title = "G", ScoringType = ScoringType.ScoreHighest, Scores = [S(a, 10), S(gone, 99)] };

        var board = ScoringService.BuildLeaderboard([a], [g], DefaultTable);

        Assert.Single(board);
        Assert.Equal(10, board[0].TotalPoints); // A is 1st once "Gone" is excluded
    }

    [Fact]
    public void BuildLeaderboard_TieBrokenByFirstPlaces_ThenShared()
    {
        Player a = P(1, "A"), b = P(2, "B"), c = P(3, "C");
        // A: 10 + 8 = 18 with one 1st. B: 9 + 9 = 18 with none, so A ranks above B.
        var g1 = new Game { Id = 1, ScoringType = ScoringType.ScoreHighest, Scores = [S(a, 30), S(b, 20), S(c, 10)] };
        var g2 = new Game { Id = 2, ScoringType = ScoringType.ScoreHighest, Scores = [S(c, 30), S(b, 20), S(a, 10)] };

        var board = ScoringService.BuildLeaderboard([a, b, c], [g1, g2], DefaultTable);

        // A and C both have 18 points and one 1st, so they share rank 1. B has 18 and no 1st, so B is 3rd.
        Assert.Equal([("A", 1), ("C", 1), ("B", 3)], board.Select(r => (r.PlayerName, r.Rank)));
    }

    [Fact]
    public void BuildTodo_ListsGamesWithoutAScore()
    {
        Player a = P(1, "A"), b = P(2, "B");
        var g1 = new Game { Id = 1, Title = "G1", Scores = [S(a, 1)] };
        var g2 = new Game { Id = 2, Title = "G2", Scores = [] };

        var todo = ScoringService.BuildTodo([b, a], [g1, g2]);

        Assert.Equal(["A", "B"], todo.Select(t => t.PlayerName));
        Assert.Equal(["G2"], todo[0].RemainingGames);
        Assert.Equal(["G1", "G2"], todo[1].RemainingGames);
    }
}
