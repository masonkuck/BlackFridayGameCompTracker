using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CompTracker.Data;
using Microsoft.EntityFrameworkCore;

namespace CompTracker.Services;

// "Backups" in appsettings.json. Folder may be relative (to the app folder) or absolute, e.g. "D:\\CompTracker Backups".
public class BackupOptions
{
    public string Folder { get; set; } = "backups";
    public int IntervalMinutes { get; set; } = 15;
    public int Keep { get; set; } = 200;
}

public record BackupFile(string Name, DateTime CreatedAt, long SizeBytes);

// Point-in-time copies of the SQLite database. VACUUM INTO makes a consistent copy even while the app is writing,
// which a plain file copy of a live database does not.
public partial class BackupService(IDbContextFactory<AppDbContext> dbFactory, BackupOptions options)
{
    private static readonly TimeSpan DownloadLinkLifetime = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ConcurrentDictionary<string, (string Name, DateTime Expires)> downloadLinks = new();

    public string Folder => options.Folder;
    public TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(1, options.IntervalMinutes));
    public int Keep => Math.Max(1, options.Keep);

    public async Task<BackupFile> CreateAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            Directory.CreateDirectory(Folder);
            var path = NewPath(DateTime.Now);
            await using (var db = await dbFactory.CreateDbContextAsync(token))
            {
                await db.Database.ExecuteSqlRawAsync("VACUUM INTO {0}", [path], token);
            }
            Prune();
            return Describe(new FileInfo(path));
        }
        finally
        {
            gate.Release();
        }
    }

    // Newest first.
    public IReadOnlyList<BackupFile> List()
    {
        var dir = new DirectoryInfo(Folder);
        if (!dir.Exists)
            return [];
        return dir.EnumerateFiles("comptracker-*.db")
            .Select(f => (File: f, Match: BackupName().Match(f.Name)))
            .Where(x => x.Match.Success)
            // Timestamp, then the -2, -3… suffix for backups taken within the same second.
            .OrderByDescending(x => x.Match.Groups["stamp"].Value, StringComparer.Ordinal)
            .ThenByDescending(x => x.Match.Groups["n"].Success ? int.Parse(x.Match.Groups["n"].Value) : 1)
            .Select(x => Describe(x.File))
            .ToList();
    }

    // Full path for a backup by file name; null for anything that isn't one of ours (no path tricks).
    public string? PathFor(string name)
    {
        if (!IsBackupName(name))
            return null;
        var path = Path.Combine(Folder, name);
        return File.Exists(path) ? path : null;
    }

    // Admin pages sit behind a per-tab PIN that a plain download request can't see, so the page hands out
    // short-lived random links instead of exposing the files directly.
    public string CreateDownloadLink(string name)
    {
        foreach (var (key, link) in downloadLinks)
        {
            if (link.Expires < DateTime.UtcNow)
                downloadLinks.TryRemove(key, out _);
        }
        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        downloadLinks[token] = (name, DateTime.UtcNow + DownloadLinkLifetime);
        return $"backups/download/{token}";
    }

    public string? RedeemDownloadLink(string token) =>
        downloadLinks.TryGetValue(token, out var link) && link.Expires >= DateTime.UtcNow ? PathFor(link.Name) : null;

    private string NewPath(DateTime now)
    {
        var stamp = now.ToString("yyyy-MM-dd_HHmmss");
        var path = Path.Combine(Folder, $"comptracker-{stamp}.db");
        for (var n = 2; File.Exists(path); n++)
            path = Path.Combine(Folder, $"comptracker-{stamp}-{n}.db");
        return path;
    }

    private void Prune()
    {
        foreach (var old in List().Skip(Keep))
            File.Delete(Path.Combine(Folder, old.Name));
    }

    private static BackupFile Describe(FileInfo f) => new(f.Name, f.LastWriteTime, f.Length);

    private static bool IsBackupName(string name) => BackupName().IsMatch(name);

    [GeneratedRegex(@"^comptracker-(?<stamp>\d{4}-\d{2}-\d{2}_\d{6})(-(?<n>\d+))?\.db$")]
    private static partial Regex BackupName();
}
