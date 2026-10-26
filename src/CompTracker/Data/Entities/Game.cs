namespace CompTracker.Data.Entities;

public enum ScoringType
{
    TimeLowest,   // speedrun: fastest time wins, value stored in milliseconds
    ScoreHighest,
    ScoreLowest,
    Bracket,      // double elimination; places come from the bracket, stored as the place number (1 = champion)
}

public static class ScoringTypeExtensions
{
    public static string Label(this ScoringType type) => type switch
    {
        ScoringType.TimeLowest => "Speedrun (fastest time)",
        ScoringType.ScoreHighest => "High score (highest wins)",
        ScoringType.ScoreLowest => "Low score (lowest wins)",
        ScoringType.Bracket => "Bracket (double elimination)",
        _ => type.ToString(),
    };

    public static string ShortLabel(this ScoringType type) => type switch
    {
        ScoringType.TimeLowest => "Speedrun",
        ScoringType.ScoreHighest => "High score",
        ScoringType.ScoreLowest => "Low score",
        ScoringType.Bracket => "Bracket",
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

    // Bracket games only: the first-round slots as comma-separated player ids, 0 for a bye (see BracketEngine).
    public string? BracketSlots { get; set; }

    // Matchups are rearranged until the bracket is locked in; winners can only be picked after that.
    public bool BracketLocked { get; set; }

    public List<Score> Scores { get; set; } = [];
    public List<BracketResult> BracketResults { get; set; } = [];
}
