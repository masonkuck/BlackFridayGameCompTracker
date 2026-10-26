namespace CompTracker.Data.Entities;

// The winner of one bracket match. MatchKey names the match within the game ("W1-3", "L2-1", "GF1"...);
// who was actually playing is worked out from the game's seeding (BracketEngine), not stored.
public class BracketResult
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
    public string MatchKey { get; set; } = "";
    public int WinnerId { get; set; }
    public Player Winner { get; set; } = null!;
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
}
