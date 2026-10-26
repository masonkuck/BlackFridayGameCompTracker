using CompTracker.Data.Entities;

namespace CompTracker.Services;

public record GameSummary(int GameId, string Title, ScoringType ScoringType, IReadOnlyList<Standing> Standings);

public record RecentScore(GameSummary Game, Standing Standing);

public record DashboardData(
    IReadOnlyList<LeaderboardRow> Leaderboard,
    IReadOnlyList<TodoRow> Todo,
    IReadOnlyList<GameSummary> Games,
    IReadOnlyList<RecentScore> Recent,
    int PlayerCount,
    IReadOnlyDictionary<int, BracketSummary> Brackets);

// One game's live standings page.
public record GameBoard(GameSummary Summary, string? Notes, bool IsActive, IReadOnlyList<string> NotPlayed);

// One game from one player's point of view. Ahead is the nearest player who placed better (null if 1st or not played).
public record GameResult(GameSummary Game, Standing? Standing, Standing? Ahead)
{
    public bool Played => Standing is not null;
    public int Competitors => Game.Standings.Count;
}

// The game where the player finished closest to the person directly ahead of them.
public record ClosestCall(GameResult Result, decimal Gap);

public record Rival(string PlayerName, int Times);

public record PlayerReport(
    LeaderboardRow Row,
    int PlayersTotal,
    IReadOnlyList<GameResult> Results,
    int Podiums,
    double? AveragePlace,
    GameResult? BestGame,
    ClosestCall? ClosestCall,
    Rival? Rival,
    LeaderboardRow? NextAbove,
    LeaderboardRow? NextBelow)
{
    public int MaxPointsPerGame { get; init; }
}

public record TournamentReport(
    IReadOnlyList<LeaderboardRow> Leaderboard,
    IReadOnlyList<GameSummary> Games,
    IReadOnlyList<PlayerReport> Players,
    int MaxPointsPerGame);

public static class ReportBuilder
{
    // Current standings for each game, counting only the given (active) players.
    public static List<GameSummary> BuildGameSummaries(IReadOnlyCollection<Player> players, IReadOnlyCollection<Game> games, IReadOnlyCollection<PlacePoints> table)
    {
        var activeIds = players.Select(p => p.Id).ToHashSet();
        return games
            .Select(g => new GameSummary(g.Id, g.Title, g.ScoringType,
                ScoringService.Rank(g.ScoringType, g.Scores.Where(s => activeIds.Contains(s.PlayerId)), table)))
            .ToList();
    }

    // Most recently posted (or updated) scores across all games, newest first, with their current place.
    public static List<RecentScore> BuildRecent(IEnumerable<GameSummary> games, int count) =>
        games
            .SelectMany(g => g.Standings.Select(s => new RecentScore(g, s)))
            .OrderByDescending(r => r.Standing.UpdatedAt)
            .Take(count)
            .ToList();

    public static TournamentReport Build(IReadOnlyCollection<Player> players, IReadOnlyCollection<Game> games, IReadOnlyCollection<PlacePoints> table)
    {
        var summaries = BuildGameSummaries(players, games, table);
        var leaderboard = ScoringService.BuildLeaderboard(players, games, table);
        var maxPoints = ScoringService.PointsForPlace(1, table);

        var reports = leaderboard
            .Select((row, i) => BuildPlayer(row, i, leaderboard, summaries) with { MaxPointsPerGame = maxPoints })
            .ToList();

        return new TournamentReport(leaderboard, summaries, reports, maxPoints);
    }

    private static PlayerReport BuildPlayer(LeaderboardRow row, int index, IReadOnlyList<LeaderboardRow> leaderboard, IReadOnlyList<GameSummary> games)
    {
        var results = games.Select(g =>
        {
            var mine = g.Standings.FirstOrDefault(s => s.PlayerId == row.PlayerId);
            var ahead = mine is null ? null : g.Standings.LastOrDefault(s => s.Place < mine.Place);
            return new GameResult(g, mine, ahead);
        }).ToList();

        var played = results.Where(r => r.Played).ToList();

        var bestGame = played
            .OrderBy(r => r.Standing!.Place)
            .ThenByDescending(r => r.Competitors)
            .FirstOrDefault();

        // Compare gaps relative to the score size so a 0.5s gap in a 1-minute run beats 50 points in a 100k-point game.
        var closest = played
            .Where(r => r.Ahead is not null && r.Game.ScoringType != ScoringType.Bracket)
            .Select(r => new ClosestCall(r, Math.Abs(r.Standing!.Value - r.Ahead!.Value)))
            .MinBy(c => c.Gap / Math.Max(Math.Abs(c.Result.Ahead!.Value), 1m));

        var rival = played
            .Where(r => r.Ahead is not null)
            .GroupBy(r => r.Ahead!.PlayerName)
            .Select(g => new Rival(g.Key, g.Count()))
            .OrderByDescending(r => r.Times).ThenBy(r => r.PlayerName)
            .FirstOrDefault();

        var nextAbove = leaderboard.Take(index).LastOrDefault(r => r.TotalPoints > row.TotalPoints);
        var nextBelow = leaderboard.Skip(index + 1).FirstOrDefault(r => r.TotalPoints < row.TotalPoints);

        return new PlayerReport(
            row,
            leaderboard.Count,
            results,
            Podiums: played.Count(r => r.Standing!.Place <= 3),
            AveragePlace: played.Count == 0 ? null : played.Average(r => r.Standing!.Place),
            bestGame,
            closest,
            rival,
            nextAbove,
            nextBelow);
    }
}
