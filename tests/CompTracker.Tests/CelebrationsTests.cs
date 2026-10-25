using CompTracker.Data.Entities;
using CompTracker.Services;

namespace CompTracker.Tests;

public class CelebrationsTests
{
    private static Standing St(int id, string name, int place, decimal value = 100) =>
        new(id, name, value, place, 0, DateTime.UtcNow);

    private static LeaderboardRow Lb(int id, string name, int rank, int points) =>
        new(id, name, rank, points, 0, 0, 0);

    private static LeaderSnapshot Snap(Standing[] game, LeaderboardRow[] board) => new(game, board);

    [Theory]
    [InlineData(new[] { 1 }, new[] { 2 }, 2)]      // took it from someone
    [InlineData(new[] { 1, 2 }, new[] { 2 }, 2)]   // broke a tie for 1st
    [InlineData(new[] { 1 }, new[] { 1 }, null)]   // kept own lead
    [InlineData(new[] { 1 }, new[] { 1, 2 }, null)] // tied, nobody leads alone
    [InlineData(new int[0], new[] { 1 }, null)]     // first score ever
    public void NewSoleLeader(int[] before, int[] after, int? expected) =>
        Assert.Equal(expected, Celebrations.NewSoleLeader(before, after));

    [Fact]
    public void Detect_GameAndOverall_InThatOrder()
    {
        var before = Snap([St(1, "Ann", 1), St(2, "Bob", 2)], [Lb(1, "Ann", 1, 19), Lb(2, "Bob", 2, 15)]);
        var after = Snap([St(2, "Bob", 1, 90), St(1, "Ann", 2)], [Lb(2, "Bob", 1, 20), Lb(1, "Ann", 2, 18)]);

        var result = Celebrations.Detect(before, after, "Tetris", ScoringType.ScoreHighest, gameIsActive: true);

        Assert.Collection(result,
            c =>
            {
                Assert.Equal(CelebrationKind.GameLeader, c.Kind);
                Assert.Equal("Bob", c.PlayerName);
                Assert.Equal(["Ann"], c.Dethroned);
                Assert.Equal("Tetris", c.GameTitle);
                Assert.Equal(90, c.Value);
            },
            c =>
            {
                Assert.Equal(CelebrationKind.OverallLeader, c.Kind);
                Assert.Equal("Bob", c.PlayerName);
                Assert.Equal(20, c.TotalPoints);
                Assert.Equal(["Ann"], c.Dethroned);
            });
    }

    [Fact]
    public void Detect_InactiveGame_OnlyChecksOverall()
    {
        var before = Snap([St(1, "Ann", 1)], [Lb(1, "Ann", 1, 10)]);
        var after = Snap([St(2, "Bob", 1), St(1, "Ann", 2)], [Lb(1, "Ann", 1, 10)]);

        Assert.Empty(Celebrations.Detect(before, after, "Tetris", ScoringType.ScoreHighest, gameIsActive: false));
    }

    [Fact]
    public void Detect_EveryoneOnZero_IsNotALead()
    {
        var before = Snap([], [Lb(1, "Ann", 1, 0), Lb(2, "Bob", 1, 0)]);
        var after = Snap([St(1, "Ann", 1)], [Lb(1, "Ann", 1, 10), Lb(2, "Bob", 2, 0)]);

        Assert.Empty(Celebrations.Detect(before, after, "Tetris", ScoringType.ScoreHighest, gameIsActive: true));
    }
}
