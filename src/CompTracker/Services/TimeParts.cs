namespace CompTracker.Services;

// The separate [h] : [min] : [sec] . [fraction] boxes used to enter speedrun times.
// Fraction is the digits after the decimal point, so "5" = .500, "15" = .150, "150" = .150.
public class TimeParts
{
    public string Hours { get; set; } = "";
    public string Minutes { get; set; } = "";
    public string Seconds { get; set; } = "";
    public string Fraction { get; set; } = "";
    public bool ShowHours { get; set; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Hours) && string.IsNullOrWhiteSpace(Minutes) &&
        string.IsNullOrWhiteSpace(Seconds) && string.IsNullOrWhiteSpace(Fraction);

    public static TimeParts FromMilliseconds(decimal milliseconds)
    {
        var total = (long)Math.Round(milliseconds);
        var h = total / 3_600_000;
        var m = total / 60_000 % 60;
        var s = total / 1000 % 60;
        var ms = total % 1000;
        return new TimeParts
        {
            ShowHours = h > 0,
            Hours = h > 0 ? h.ToString() : "",
            // Without an hours box, minutes carry the whole total.
            Minutes = (h > 0 ? m : total / 60_000).ToString(),
            Seconds = s.ToString("00"),
            Fraction = ms > 0 ? ms.ToString("000") : "",
        };
    }

    public bool TryGetMilliseconds(out decimal milliseconds, out string? error)
    {
        milliseconds = 0;
        error = null;
        if (IsEmpty)
        {
            error = "Enter a time.";
            return false;
        }

        if (!TryPart(Hours, out var h) || !TryPart(Minutes, out var m) || !TryPart(Seconds, out var s) ||
            !TryPart(Fraction, out _) || Fraction.Trim().Length > 3)
        {
            error = "Use digits only (up to 3 after the dot).";
            return false;
        }
        // Seconds can only overflow when nothing bigger was entered (e.g. "95" sec on its own is fine).
        var hasBigger = !string.IsNullOrWhiteSpace(Hours) || !string.IsNullOrWhiteSpace(Minutes);
        if (hasBigger && s >= 60)
        {
            error = "Seconds must be 0–59.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(Hours) && m >= 60)
        {
            error = "Minutes must be 0–59 when hours are entered.";
            return false;
        }

        var fraction = Fraction.Trim().PadRight(3, '0');
        var ms = fraction.Length == 0 ? 0 : int.Parse(fraction);
        milliseconds = ((h * 60m + m) * 60m + s) * 1000m + ms;
        return true;
    }

    private static bool TryPart(string text, out long value)
    {
        value = 0;
        var t = text.Trim();
        return t.Length == 0 || (t.All(char.IsAsciiDigit) && long.TryParse(t, out value));
    }
}
