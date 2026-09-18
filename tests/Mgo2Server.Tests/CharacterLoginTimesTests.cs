using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the login stamp pair: the gap the titles are evaluated against, and the
/// rotation that has to happen after it rather than before.
/// </summary>
public sealed class CharacterLoginTimesTests
{
    /// <summary>Seconds in a day, so the cases below can be spelled in days.</summary>
    private const int Day = 86_400;

    /// <summary>A character that has never logged in has no gap, not a gap of decades.</summary>
    [Fact]
    public void A_character_with_no_recorded_login_has_no_gap()
    {
        var times = new CharacterLoginTimes(null, null);

        Assert.Equal(0, times.DaysSinceLastLogin(1_700_000_000));
    }

    /// <summary>Whole days, counted down: a gap a second short of a day is a day less.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(Day, 1)]
    [InlineData(29 * Day, 29)]
    [InlineData((30 * Day) - 1, 29)]
    [InlineData(30 * Day, 30)]
    [InlineData(365 * Day, 365)]
    public void The_gap_is_measured_in_whole_days_from_the_recorded_login(int elapsed, int expected)
    {
        var now = 1_700_000_000;
        var times = new CharacterLoginTimes(null, now - elapsed);

        Assert.Equal(expected, times.DaysSinceLastLogin(now));
    }

    /// <summary>
    /// A stamp in the future reads as no gap rather than as a negative age: the clock of
    /// the machine that wrote it is not a reason to award an absence.
    /// </summary>
    [Fact]
    public void A_stamp_in_the_future_is_no_gap()
    {
        var times = new CharacterLoginTimes(null, 1_700_000_000);

        Assert.Equal(0, times.DaysSinceLastLogin(1_600_000_000));
    }

    /// <summary>
    /// The rotation is what makes the gap readable once and only once: after it, the login
    /// that was recorded is the previous one and the same stamps measure no gap at all.
    /// </summary>
    [Fact]
    public void The_rotation_moves_the_recorded_login_into_the_previous_one()
    {
        var times = new CharacterLoginTimes(1_600_000_000, 1_699_000_000);
        var rotated = times.RotatedTo(1_700_000_000);

        Assert.Equal(11, times.DaysSinceLastLogin(1_700_000_000));
        Assert.Equal(1_699_000_000, rotated.PreviousLoginTime);
        Assert.Equal(1_700_000_000, rotated.LastLoginTime);
        // The gap is readable once and only once: the stamps that produced it now say none.
        Assert.Equal(0, rotated.DaysSinceLastLogin(1_700_000_000));
    }

    /// <summary>
    /// The other end of the tie: the gap this pair reports is what the absence title is
    /// measured against, so a month away reaches it and a day short of one does not.
    /// </summary>
    [Fact]
    public void The_gap_reaches_the_rank_that_needs_an_absence()
    {
        // Five kills over ten rounds clears the passive-play rank that would otherwise be
        // returned before the absence is consulted.
        var statistics = new CharacterStatistics { CharacterIdentifier = 1, Rounds = 10, Kills = 5 };

        Assert.Equal(13, AnimalRankService.CalculateRank(statistics, 30));
        Assert.Equal(0, AnimalRankService.CalculateRank(statistics, 29));
    }
}
