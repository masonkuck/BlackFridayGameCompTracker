using CompTracker.Data.Entities;

namespace CompTracker.Services;

public enum CelebrationKind { GameLeader, OverallLeader }

// One "NEW LEADER" moment for the dashboard. Game fields are set for GameLeader, TotalPoints for OverallLeader.
public record Celebration(
    CelebrationKind Kind,
    string PlayerName,
    IReadOnlyList<string> Dethroned,
    string? GameTitle = null,
    ScoringType? ScoringType = null,
    decimal? Value = null,
    int? TotalPoints = null);

// Raised (through ChangeNotifier) when a score is saved; the dashboard pings and plays any celebrations in order.
public record ScorePosted(int GameId, IReadOnlyList<Celebration> Celebrations);

// Who held 1st in one game and overall at a moment in time; compare one from before a save with one from after.
public record LeaderSnapshot(IReadOnlyList<Standing> GameStandings, IReadOnlyList<LeaderboardRow> Leaderboard);

public static class Celebrations
{
    // A new leader only counts when they hold 1st alone and took it from someone else:
    // ties don't count, keeping your own lead doesn't count, and the first score ever doesn't count.
    public static int? NewSoleLeader(IReadOnlyCollection<int> before, IReadOnlyCollection<int> after)
    {
        if (after.Count != 1 || before.Count == 0)
            return null;
        var leader = after.First();
        return before.Count == 1 && before.Contains(leader) ? null : leader;
    }

    public static List<Celebration> Detect(LeaderSnapshot before, LeaderSnapshot after, string gameTitle, ScoringType type, bool gameIsActive)
    {
        var result = new List<Celebration>();

        if (gameIsActive)
        {
            var was = GameLeaders(before);
            var now = GameLeaders(after);
            // A bracket has no leader until its champion is crowned, and that's the moment worth celebrating.
            var crowned = type.IsBracket() && was.Count == 0 && now.Count == 1 ? now[0].PlayerId : (int?)null;
            if ((crowned ?? NewSoleLeader(Ids(was), Ids(now))) is { } id)
            {
                var leader = now.Single(s => s.PlayerId == id);
                result.Add(new Celebration(CelebrationKind.GameLeader, leader.PlayerName,
                    was.Where(s => s.PlayerId != id).Select(s => s.PlayerName).ToList(),
                    GameTitle: gameTitle, ScoringType: type, Value: leader.Value));
            }
        }

        var wasTop = OverallLeaders(before);
        var nowTop = OverallLeaders(after);
        if (NewSoleLeader(wasTop.Select(r => r.PlayerId).ToList(), nowTop.Select(r => r.PlayerId).ToList()) is { } topId)
        {
            var leader = nowTop.Single(r => r.PlayerId == topId);
            result.Add(new Celebration(CelebrationKind.OverallLeader, leader.PlayerName,
                wasTop.Where(r => r.PlayerId != topId).Select(r => r.PlayerName).ToList(),
                TotalPoints: leader.TotalPoints));
        }

        return result;
    }

    private static List<Standing> GameLeaders(LeaderSnapshot s) => s.GameStandings.Where(x => x.Place == 1).ToList();

    // Nobody "leads" while everyone is still on zero.
    private static List<LeaderboardRow> OverallLeaders(LeaderSnapshot s) =>
        s.Leaderboard.Where(r => r.Rank == 1 && r.TotalPoints > 0).ToList();

    private static List<int> Ids(IEnumerable<Standing> standings) => standings.Select(s => s.PlayerId).ToList();
}
