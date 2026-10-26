using System.Globalization;
using CompTracker.Data.Entities;

namespace CompTracker.Services;

// Converts between what people type/see and the stored decimal value.
// Times are stored as milliseconds and accept "ss(.fff)", "m:ss(.fff)" or "h:mm:ss(.fff)".
public static class ScoreFormat
{
    public static string Placeholder(ScoringType type) =>
        type == ScoringType.TimeLowest ? "m:ss.fff" : "score";

    public static bool TryParse(ScoringType type, string? input, out decimal value) =>
        type == ScoringType.TimeLowest
            ? TryParseTime(input, out value)
            : TryParseNumber(input, out value);

    public static string Format(ScoringType type, decimal value) => type switch
    {
        ScoringType.TimeLowest => FormatTime(value),
        ScoringType.Bracket or ScoringType.BracketSingle => Ordinal((int)value),   // a bracket "score" is the finishing place
        _ => FormatNumber(value),
    };

    // A difference between two results, e.g. "0.4s" or "1:02.5" for times, "1,250" for scores.
    public static string FormatGap(ScoringType type, decimal gap)
    {
        if (type != ScoringType.TimeLowest)
            return FormatNumber(gap);
        return gap < 60_000
            ? (gap / 1000m).ToString("0.###", CultureInfo.InvariantCulture) + "s"
            : FormatTime(gap);
    }

    // CSS class that colors the top three places.
    public static string PlaceClass(int place) => place switch
    {
        1 => "gold",
        2 => "silver",
        3 => "bronze",
        _ => "",
    };

    public static string Ordinal(int n) => (n % 100) switch
    {
        11 or 12 or 13 => $"{n}th",
        _ => (n % 10) switch { 1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th" },
    };

    public static bool TryParseNumber(string? input, out decimal value) =>
        decimal.TryParse(input?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    public static string FormatNumber(decimal value) =>
        value.ToString("#,0.###", CultureInfo.InvariantCulture);

    public static bool TryParseTime(string? input, out decimal milliseconds)
    {
        milliseconds = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var parts = input.Trim().Split(':');
        if (parts.Length > 3)
            return false;

        if (!decimal.TryParse(parts[^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds))
            return false;

        int minutes = 0, hours = 0;
        if (parts.Length >= 2 && !int.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture, out minutes))
            return false;
        if (parts.Length == 3 && !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hours))
            return false;

        // Lower units must stay in range once a higher unit is given (e.g. reject "1:75").
        if (parts.Length >= 2 && seconds >= 60)
            return false;
        if (parts.Length == 3 && minutes >= 60)
            return false;

        milliseconds = Math.Round(((hours * 60m + minutes) * 60m + seconds) * 1000m);
        return true;
    }

    public static string FormatTime(decimal milliseconds)
    {
        var total = (long)Math.Round(milliseconds);
        var ms = total % 1000;
        var totalSeconds = total / 1000;
        var s = totalSeconds % 60;
        var m = totalSeconds / 60 % 60;
        var h = totalSeconds / 3600;

        var text = h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
        return ms > 0 ? $"{text}.{ms:000}" : text;
    }
}
