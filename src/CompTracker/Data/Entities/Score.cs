namespace CompTracker.Data.Entities;

// One score per player per game (unique index on GameId + PlayerId); overwritten when they improve.
public class Score
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    // Milliseconds for TimeLowest games, raw score otherwise.
    public decimal Value { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
