using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the clock times the event command accepts. The window is stored in
/// epoch seconds but a moderator types "20:00", so the reading of what was
/// typed — and which day it lands on — is the part that has to be right.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventClockTimeTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 9, 26, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void A_clock_time_reads_as_that_second_of_today()
    {
        Assert.True(EventClockTimeUtils.TryParse(
            "20:00",
            At(9, 0),
            Utc,
            rollToTomorrow: false,
            out var epochSecond));

        Assert.Equal(At(20, 0).ToUnixTimeSeconds(), epochSecond);
    }

    [Fact]
    public void A_bare_hour_reads_as_that_hour_on_the_hour()
    {
        Assert.True(EventClockTimeUtils.TryParse("20", At(9, 0), Utc, false, out var epochSecond));

        Assert.Equal(At(20, 0).ToUnixTimeSeconds(), epochSecond);
    }

    [Fact]
    public void An_opening_time_that_has_passed_rolls_to_tomorrow()
    {
        Assert.True(EventClockTimeUtils.TryParse("20:00", At(21, 0), Utc, rollToTomorrow: true, out var epochSecond));

        Assert.Equal(At(20, 0).AddDays(1).ToUnixTimeSeconds(), epochSecond);
    }

    [Fact]
    public void A_closing_time_that_has_passed_stays_today()
    {
        // A closing time in the past is what makes a window already over, which
        // is a fact about the event rather than a reason to move it.
        Assert.True(EventClockTimeUtils.TryParse("20:00", At(21, 0), Utc, rollToTomorrow: false, out var epochSecond));

        Assert.Equal(At(20, 0).ToUnixTimeSeconds(), epochSecond);
    }

    [Theory]
    [InlineData("never")]
    [InlineData("open")]
    [InlineData("NONE")]
    public void A_word_for_an_event_that_never_closes_reads_as_zero(string text)
    {
        Assert.True(EventClockTimeUtils.TryParse(text, At(9, 0), Utc, false, out var epochSecond));

        Assert.Equal(0, epochSecond);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("half nine")]
    [InlineData("25:00")]
    [InlineData("20:61")]
    [InlineData("20:00:00")]
    public void Something_that_is_not_a_clock_time_is_refused(string text)
    {
        Assert.False(EventClockTimeUtils.TryParse(text, At(9, 0), Utc, false, out _));
    }

    [Fact]
    public void A_null_is_refused_rather_than_throwing()
    {
        Assert.False(EventClockTimeUtils.TryParse(null, At(9, 0), Utc, false, out _));
    }

    [Fact]
    public void A_stored_moment_is_written_back_as_the_hour_it_was_given()
    {
        Assert.Equal("20:00", EventClockTimeUtils.Describe(At(20, 0).ToUnixTimeSeconds(), Utc));
    }
}
