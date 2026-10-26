using CompTracker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CompTracker.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Score> Scores => Set<Score>();
    public DbSet<PlacePoints> PlacePoints => Set<PlacePoints>();
    public DbSet<BracketResult> BracketResults => Set<BracketResult>();

    public static IEnumerable<PlacePoints> DefaultPlacePoints() =>
        Enumerable.Range(1, 10).Select(place => new PlacePoints { Place = place, Points = 11 - place });

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100).UseCollation("NOCASE");
            e.HasIndex(p => p.Name).IsUnique();
        });

        modelBuilder.Entity<Game>(e =>
        {
            e.Property(g => g.Title).HasMaxLength(200);
            e.Property(g => g.Notes).HasMaxLength(2000);
            e.Property(g => g.ScoringType).HasConversion<string>().HasMaxLength(20);
            e.Property(g => g.BracketSlots).HasMaxLength(4000);
        });

        modelBuilder.Entity<BracketResult>(e =>
        {
            e.Property(r => r.MatchKey).HasMaxLength(20);
            e.HasIndex(r => new { r.GameId, r.MatchKey }).IsUnique();
            e.HasOne(r => r.Game).WithMany(g => g.BracketResults).HasForeignKey(r => r.GameId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Winner).WithMany().HasForeignKey(r => r.WinnerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Score>(e =>
        {
            e.HasIndex(s => new { s.GameId, s.PlayerId }).IsUnique();
            e.HasOne(s => s.Game).WithMany(g => g.Scores).HasForeignKey(s => s.GameId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Player).WithMany(p => p.Scores).HasForeignKey(s => s.PlayerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlacePoints>(e =>
        {
            e.HasKey(pp => pp.Place);
            e.Property(pp => pp.Place).ValueGeneratedNever();
            e.HasData(DefaultPlacePoints());
        });
    }
}
