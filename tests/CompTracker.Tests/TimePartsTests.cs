using CompTracker.Services;

namespace CompTracker.Tests;

public class TimePartsTests
{
    private static TimeParts T(string h, string m, string s, string f) =>
        new() { Hours = h, Minutes = m, Seconds = s, Fraction = f, ShowHours = h != "" };

    [Theory]
    [InlineData("", "4", "32", "150", 272_150)]
    [InlineData("", "4", "32", "15", 272_150)]   // fraction is digits after the dot
    [InlineData("", "4", "32", "5", 272_500)]
    [InlineData("", "4", "32", "", 272_000)]
    [InlineData("", "4", "", "", 240_000)]
    [InlineData("", "", "45", "", 45_000)]
    [InlineData("", "", "95", "", 95_000)]       // seconds alone may exceed 59
    [InlineData("", "75", "00", "", 4_500_000)]  // minutes may exceed 59 without an hours box
    [InlineData("1", "02", "03", "007", 3_723_007)]
    [InlineData("", " 4 ", " 05 ", "", 245_000)]
    public void TryGetMilliseconds_Valid(string h, string m, string s, string f, long expected)
    {
        Assert.True(T(h, m, s, f).TryGetMilliseconds(out var ms, out var error), error);
        Assert.Equal(expected, ms);
    }

    [Theory]
    [InlineData("", "", "", "")]
    [InlineData("", "4", "60", "")]
    [InlineData("1", "60", "00", "")]
    [InlineData("", "4", "3a", "")]
    [InlineData("", "4", "30", "1234")]
    [InlineData("", "-1", "30", "")]
    public void TryGetMilliseconds_Invalid(string h, string m, string s, string f)
    {
        Assert.False(T(h, m, s, f).TryGetMilliseconds(out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void FromMilliseconds_UnderAnHour()
    {
        var p = TimeParts.FromMilliseconds(272_150);
        Assert.Equal(("", "4", "32", "150", false), (p.Hours, p.Minutes, p.Seconds, p.Fraction, p.ShowHours));
    }

    [Fact]
    public void FromMilliseconds_OverAnHour_ShowsHours()
    {
        var p = TimeParts.FromMilliseconds(3_723_007);
        Assert.Equal(("1", "2", "03", "007", true), (p.Hours, p.Minutes, p.Seconds, p.Fraction, p.ShowHours));
    }

    [Fact]
    public void FromMilliseconds_WholeSeconds_LeavesFractionEmpty()
    {
        var p = TimeParts.FromMilliseconds(5_000);
        Assert.Equal(("0", "05", ""), (p.Minutes, p.Seconds, p.Fraction));
    }

    [Theory]
    [InlineData(272_150)]
    [InlineData(3_723_007)]
    [InlineData(59_999)]
    [InlineData(0)]
    public void RoundTrips(long ms)
    {
        Assert.True(TimeParts.FromMilliseconds(ms).TryGetMilliseconds(out var back, out _));
        Assert.Equal(ms, back);
    }
}
