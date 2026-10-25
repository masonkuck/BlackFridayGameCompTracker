namespace CompTracker.Data.Entities;

public enum ScoringType
{
    TimeLowest,   // speedrun: fastest time wins, value stored in milliseconds
    ScoreHighest,
    ScoreLowest,
}

public static class ScoringTypeExtensions
{
    public static string Label(this ScoringType type) => type switch
    {
        ScoringType.TimeLowest => "Speedrun (fastest time)",
        ScoringType.ScoreHighest => "High score (highest wins)",
        ScoringType.ScoreLowest => "Low score (lowest wins)",
        _ => type.ToString(),
    };

    public static string ShortLabel(this ScoringType type) => type switch
    {
        ScoringType.TimeLowest => "Speedrun",
        ScoringType.ScoreHighest => "High score",
        ScoringType.ScoreLowest => "Low score",
        _ => type.ToString(),
    };

    public static bool LowerIsBetter(this ScoringType type) => type != ScoringType.ScoreHighest;
}

public class Game
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public ScoringType ScoringType { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public List<Score> Scores { get; set; } = [];
}
