using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the arithmetic the once-a-day workers sleep on. It is the only part of a
/// daily schedule that can be wrong without waiting for midnight to find out, and
/// the two ways it goes wrong are opposite: running again in the instant it just
/// ran, or waiting nearly two days because today's time was taken for tomorrow's.
/// </summary>
[Trait("Category", "Shared")]
public sealed class DailyWorkerUtilsTests
{
    /// <summary>Time of day the daily cleanups run at, as the services pass it.</summary>
    private static readonly TimeOnly Midnight = DailyWorker.MidnightUtc;

    [Fact]
    public void Waits_until_todays_midnight_while_it_is_still_ahead()
    {
        var now = new DateTimeOffset(2026, 9, 19, 23, 30, 0, TimeSpan.Zero);

        // Half an hour before the run, the wait is half an hour and not a day: the
        // occurrence of today is taken while it is still in the future.
        Assert.Equal(TimeSpan.FromMinutes(30), DailyWorkerUtils.TimeUntilNext(Midnight, now));
    }

    [Fact]
    public void Waits_until_tomorrows_midnight_once_todays_has_passed()
    {
        var now = new DateTimeOffset(2026, 9, 20, 13, 45, 0, TimeSpan.Zero);

        // Ten and a quarter hours to tomorrow, not a negative distance into today:
        // a wait of the wrong sign is a worker that runs in a loop.
        Assert.Equal(
            TimeSpan.FromHours(10) + TimeSpan.FromMinutes(15),
            DailyWorkerUtils.TimeUntilNext(Midnight, now));
    }

    [Fact]
    public void Waits_a_whole_day_when_it_is_measured_from_the_run_itself()
    {
        var now = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);

        // The moment it just ran is not still ahead of it, so the next one is a day
        // out. Taking today's occurrence here would schedule the run for the instant
        // it had already happened.
        Assert.Equal(TimeSpan.FromDays(1), DailyWorkerUtils.TimeUntilNext(Midnight, now));
    }

    [Fact]
    public void Measures_to_the_time_it_is_given_rather_than_to_midnight()
    {
        var now = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);

        // A daily worker is not necessarily a midnight one, and the wait is the
        // distance to the time it names.
        Assert.Equal(
            TimeSpan.FromHours(3),
            DailyWorkerUtils.TimeUntilNext(new TimeOnly(3, 0), now));
    }

    [Fact]
    public void Counts_in_utc_however_the_reader_is_zoned()
    {
        // The same instant, written in a zone eight hours ahead. The schedule is a
        // UTC one, so the answer is the same half hour, not one measured against a
        // host clock that happens to read a different date.
        var now = new DateTimeOffset(2026, 9, 20, 7, 30, 0, TimeSpan.FromHours(8));

        Assert.Equal(TimeSpan.FromMinutes(30), DailyWorkerUtils.TimeUntilNext(Midnight, now));
    }
}
