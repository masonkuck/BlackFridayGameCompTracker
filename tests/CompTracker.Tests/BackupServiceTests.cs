using CompTracker.Data;
using CompTracker.Data.Entities;
using CompTracker.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CompTracker.Tests;

// Runs against a real SQLite file in a temp folder, since VACUUM INTO is the whole point.
public sealed class BackupServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "comptracker-tests-" + Guid.NewGuid().ToString("N"));
    private readonly IDbContextFactory<AppDbContext> factory;

    public BackupServiceTests()
    {
        Directory.CreateDirectory(root);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "live.db")};Pooling=False")
            .Options;
        factory = new PooledDbContextFactory<AppDbContext>(options);
        using var db = factory.CreateDbContext();
        db.Database.Migrate();
        db.Players.Add(new Player { Name = "Ann" });
        db.SaveChanges();
    }

    private BackupService Service(int keep = 200) =>
        new(factory, new BackupOptions { Folder = Path.Combine(root, "backups"), Keep = keep });

    [Fact]
    public async Task Create_WritesAReadableCopy()
    {
        var backups = Service();

        var file = await backups.CreateAsync();

        var path = backups.PathFor(file.Name);
        Assert.NotNull(path);
        using var conn = new SqliteConnection($"Data Source={path};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Players";
        Assert.Equal("Ann", cmd.ExecuteScalar());
    }

    [Fact]
    public async Task Create_SameSecond_GetsUniqueNames_AndPrunesToKeep()
    {
        var backups = Service(keep: 2);

        var first = await backups.CreateAsync();
        await backups.CreateAsync();
        var third = await backups.CreateAsync();

        var list = backups.List();
        Assert.Equal(2, list.Count);
        Assert.Equal(third.Name, list[0].Name);
        Assert.DoesNotContain(list, f => f.Name == first.Name);
    }

    [Theory]
    [InlineData("../live.db")]
    [InlineData("live.db")]
    [InlineData("comptracker-2026-11-28_211500.db")] // well-formed but doesn't exist
    public void PathFor_RejectsAnythingElse(string name) => Assert.Null(Service().PathFor(name));

    [Fact]
    public async Task DownloadLink_RedeemsToTheFile_AndUnknownTokensDoNot()
    {
        var backups = Service();
        var file = await backups.CreateAsync();

        var link = backups.CreateDownloadLink(file.Name);

        Assert.Equal(backups.PathFor(file.Name), backups.RedeemDownloadLink(link.Split('/').Last()));
        Assert.Null(backups.RedeemDownloadLink("NOT-A-TOKEN"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
    }
}
