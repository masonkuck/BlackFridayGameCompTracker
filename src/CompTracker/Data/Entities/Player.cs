namespace CompTracker.Data.Entities;

public class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    // Inactive players are hidden from standings, the leaderboard and the todo list, but keep their scores.
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Score> Scores { get; set; } = [];
}
