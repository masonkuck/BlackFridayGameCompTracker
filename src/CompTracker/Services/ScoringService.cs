using CompTracker.Data;
using CompTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CompTracker.Services;

public record Standing(int PlayerId, string PlayerName, decimal Value, int Place, int Points, DateTime UpdatedAt);

public record LeaderboardRow(int PlayerId, string PlayerName, int Rank, int TotalPoints, int FirstPlaces, int GamesDone, int GamesTotal);

public record TodoRow(int PlayerId, string PlayerName, IReadOnlyList<string> RemainingGames);

// Everything is computed on read from the current scores and points table, so edits show up immediately.
public class ScoringService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<IReadOnlyList<Standing>> GetGameStandingsAsync(int gameId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var game = await db.Games.AsNoTracking()
            .Include(g => g.Scores.Where(s => s.Player.IsActive)).ThenInclude(s => s.Player)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game is null)
            return [];

        var table = await db.PlacePoints.AsNoTracking().ToListAsync();
        return Rank(game.ScoringType, game.Scores, table);
    }

    public async Task<IReadOnlyList<LeaderboardRow>> GetLeaderboardAsync()
    {
        var (players, games, table) = await LoadAsync();
        return BuildLeaderboard(players, games, table);
    }

    public async Task<LeaderSnapshot> GetLeaderSnapshotAsync(int gameId) =>
        new(await GetGameStandingsAsync(gameId), await GetLeaderboardAsync());

    public async Task<IReadOnlyList<TodoRow>> GetTodoAsync()
    {
        var (players, games, _) = await LoadAsync();
        return BuildTodo(players, games);
    }

    // Everything the dashboard shows, from a single load.
    public async Task<DashboardData> GetDashboardAsync(int recentCount = 15)
    {
        var (players, games, table) = await LoadAsync();
        var summaries = ReportBuilder.BuildGameSummaries(players, games, table);
        return new DashboardData(
            BuildLeaderboard(players, games, table),
            BuildTodo(players, games),
            summaries,
            ReportBuilder.BuildRecent(summaries, recentCount),
            players.Count);
    }

    public async Task<GameBoard?> GetGameBoardAsync(int gameId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var game = await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId);
        if (game is null)
            return null;

        var standings = await GetGameStandingsAsync(gameId);
        var played = standings.Select(s => s.PlayerId).ToHashSet();
        var notPlayed = await db.Players.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync();

        return new GameBoard(
            new GameSummary(game.Id, game.Title, game.ScoringType, standings),
            game.Notes,
            game.IsActive,
            notPlayed.Where(p => !played.Contains(p.Id)).Select(p => p.Name).ToList());
    }

    public async Task<TournamentReport> GetReportAsync()
    {
        var (players, games, table) = await LoadAsync();
        return ReportBuilder.Build(players, games, table);
    }

    private async Task<(List<Player>, List<Game>, List<PlacePoints>)> LoadAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var players = await db.Players.AsNoTracking().Where(p => p.IsActive).ToListAsync();
        var games = await db.Games.AsNoTracking().Where(g => g.IsActive)
            .Include(g => g.Scores).ThenInclude(s => s.Player)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Title)
            .ToListAsync();
        var table = await db.PlacePoints.AsNoTracking().ToListAsync();
        return (players, games, table);
    }

    // Uses the exact place if defined, otherwise the closest lower place (so places past the end of the
    // list get the last value). Never less than 1 point.
    public static int PointsForPlace(int place, IReadOnlyCollection<PlacePoints> table)
    {
        var entry = table.Where(pp => pp.Place <= place).MaxBy(pp => pp.Place);
        return Math.Max(entry?.Points ?? 1, 1);
    }

    // Standard competition ranking: tied values share a place and the next place is skipped (1, 2, 2, 4).
    public static List<Standing> Rank(ScoringType type, IEnumerable<Score> scores, IReadOnlyCollection<PlacePoints> table)
    {
        var ordered = type.LowerIsBetter()
            ? scores.OrderBy(s => s.Value).ThenBy(s => s.Player.Name).ToList()
            : scores.OrderByDescending(s => s.Value).ThenBy(s => s.Player.Name).ToList();

        var standings = new List<Standing>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var s = ordered[i];
            var place = i > 0 && ordered[i - 1].Value == s.Value ? standings[i - 1].Place : i + 1;
            standings.Add(new Standing(s.PlayerId, s.Player.Name, s.Value, place, PointsForPlace(place, table), s.UpdatedAt));
        }
        return standings;
    }

    public static List<LeaderboardRow> BuildLeaderboard(IReadOnlyCollection<Player> players, IReadOnlyCollection<Game> games, IReadOnlyCollection<PlacePoints> table)
    {
        var activeIds = players.Select(p => p.Id).ToHashSet();
        var standings = games
            .SelectMany(g => Rank(g.ScoringType, g.Scores.Where(s => activeIds.Contains(s.PlayerId)), table))
            .ToLookup(s => s.PlayerId);

        var rows = players
            .Select(p => new
            {
                Player = p,
                Total = standings[p.Id].Sum(s => s.Points),
                Firsts = standings[p.Id].Count(s => s.Place == 1),
                Done = standings[p.Id].Count(),
            })
            .OrderByDescending(r => r.Total).ThenByDescending(r => r.Firsts).ThenBy(r => r.Player.Name)
            .ToList();

        var result = new List<LeaderboardRow>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var tiedWithPrevious = i > 0 && rows[i - 1].Total == r.Total && rows[i - 1].Firsts == r.Firsts;
            var rank = tiedWithPrevious ? result[i - 1].Rank : i + 1;
            result.Add(new LeaderboardRow(r.Player.Id, r.Player.Name, rank, r.Total, r.Firsts, r.Done, games.Count));
        }
        return result;
    }

    public static List<TodoRow> BuildTodo(IReadOnlyCollection<Player> players, IReadOnlyCollection<Game> games) =>
        players
            .OrderBy(p => p.Name)
            .Select(p => new TodoRow(
                p.Id,
                p.Name,
                games.Where(g => g.Scores.All(s => s.PlayerId != p.Id)).Select(g => g.Title).ToList()))
            .ToList();
}
