using CompTracker.Data.Entities;
using CompTracker.Services;

namespace CompTracker.Tests;

public class ScoreFormatTests
{
    [Theory]
    [InlineData("45", 45_000)]
    [InlineData("45.5", 45_500)]
    [InlineData("1:23", 83_000)]
    [InlineData("1:23.456", 83_456)]
    [InlineData("12:03.1", 723_100)]
    [InlineData("1:02:03", 3_723_000)]
    [InlineData(" 0:59.999 ", 59_999)]
    public void TryParseTime_Valid(string input, long expectedMs)
    {
        Assert.True(ScoreFormat.TryParseTime(input, out var ms));
        Assert.Equal(expectedMs, ms);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1:75")]
    [InlineData("1:60:00")]
    [InlineData("1:2:3:4")]
    [InlineData("-1:00")]
    [InlineData("1::00")]
    public void TryParseTime_Invalid(string input) =>
        Assert.False(ScoreFormat.TryParseTime(input, out _));

    [Theory]
    [InlineData(83_456, "1:23.456")]
    [InlineData(83_000, "1:23")]
    [InlineData(5_000, "0:05")]
    [InlineData(3_723_000, "1:02:03")]
    [InlineData(723_100, "12:03.100")]
    public void FormatTime(long ms, string expected) =>
        Assert.Equal(expected, ScoreFormat.FormatTime(ms));

    [Theory]
    [InlineData("1:23.456")]
    [InlineData("1:02:03.007")]
    [InlineData("0:42")]
    public void Time_RoundTrips(string text)
    {
        Assert.True(ScoreFormat.TryParse(ScoringType.TimeLowest, text, out var value));
        Assert.Equal(text, ScoreFormat.Format(ScoringType.TimeLowest, value));
    }

    [Theory]
    [InlineData("12345", 12345)]
    [InlineData("12,345", 12345)]
    [InlineData("99.5", 99.5)]
    public void TryParseNumber_Valid(string input, double expected)
    {
        Assert.True(ScoreFormat.TryParse(ScoringType.ScoreHighest, input, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Fact]
    public void FormatNumber_UsesThousandsSeparator() =>
        Assert.Equal("1,234,567", ScoreFormat.Format(ScoringType.ScoreHighest, 1234567));

    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(21, "21st")]
    public void Ordinal(int n, string expected) => Assert.Equal(expected, ScoreFormat.Ordinal(n));
}
