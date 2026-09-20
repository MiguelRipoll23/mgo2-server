namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Resolves the wait a once-a-day worker sleeps, which is the distance to the
/// next occurrence of the time it runs at rather than the length of a day.
/// <para>
/// Split out of <see cref="DailyWorker"/> so the arithmetic can be witnessed
/// without waiting for midnight: it is pure, takes the moment it is measured
/// from, and is the only part of the schedule that can be wrong.
/// </para>
/// </summary>
public static class DailyWorkerUtils
{
    /// <summary>
    /// How long it is until the clock next reads <paramref name="runAtUtc"/>,
    /// counted in UTC so that every instance of a role agrees on the moment
    /// whatever the host's own zone is.
    /// </summary>
    /// <param name="runAtUtc">Time of day the worker runs at, in UTC.</param>
    /// <param name="now">Moment the wait is measured from.</param>
    /// <returns>
    /// The wait before the next occurrence, which is always in the future: the
    /// occurrence of today if it has not happened yet, and of tomorrow if it has.
    /// </returns>
    public static TimeSpan TimeUntilNext(TimeOnly runAtUtc, DateTimeOffset now)
    {
        var current = now.UtcDateTime;
        var next = current.Date + runAtUtc.ToTimeSpan();

        // Today's occurrence is taken only while it is still ahead. A worker that
        // just ran at its own time would otherwise be due again in the same
        // instant, and would run in a loop for as long as it stayed on that time.
        if (next <= current)
        {
            next = next.AddDays(1);
        }

        return next - current;
    }
}
