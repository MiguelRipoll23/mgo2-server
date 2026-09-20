using Microsoft.Extensions.Logging;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Runs one operation once a day, at the same time of day, rather than on an
/// interval measured from the end of the last run.
/// <para>
/// The time is UTC and it is the same for every instance of a role, so a
/// deployment that has to clean up after processes it does not own does it once
/// the whole cluster agrees it is due. That is the whole reason the cadence is a
/// clock reading rather than a length of time: an interval measured from the run
/// would let each container drift to its own hour, and a cleanup whose point is
/// to happen after everybody else's beat did not happen on a shared one.
/// </para>
/// <para>
/// Nothing runs at startup. A worker that keeps a row alive must run the moment
/// it starts, because the row it keeps is already ageing; a daily cleanup is
/// not holding anything up, so it waits for its hour, and nine containers
/// restarting do not turn into nine cleanups at nine different times.
/// </para>
/// <para>
/// A failed run is retried within the day rather than after a whole one: the
/// ceiling of the base worker's ladder is its interval, and a day is too long to
/// leave the rows of dead processes behind when the failure was a database that
/// was away for a moment. A run that succeeds waits out the rest of the day.
/// </para>
/// </summary>
public abstract class DailyWorker : PeriodicWorker
{
    /// <summary>Time of day, in UTC, the daily cleanups run at: midnight.</summary>
    public static readonly TimeOnly MidnightUtc = new(0, 0);

    /// <summary>First wait of the ladder a failed run climbs.</summary>
    private static readonly TimeSpan FailureRetryBase = TimeSpan.FromMinutes(5);

    /// <summary>Longest a failing daily worker waits, so it is tried again the same day.</summary>
    private static readonly TimeSpan FailureRetryCeiling = TimeSpan.FromHours(1);

    private readonly TimeOnly runAtUtc;

    /// <summary>Creates the worker.</summary>
    /// <param name="runAtUtc">Time of day, in UTC, the operation runs at.</param>
    /// <param name="logger">Logger of the worker.</param>
    protected DailyWorker(TimeOnly runAtUtc, ILogger logger)
        : base(TimeSpan.FromDays(1), logger)
    {
        this.runAtUtc = runAtUtc;
    }

    /// <inheritdoc />
    protected override bool RunsImmediately => false;

    /// <summary>Time of day, in UTC, this worker runs at.</summary>
    protected TimeOnly RunAtUtc => runAtUtc;

    /// <inheritdoc />
    protected override TimeSpan ResolveWait(int failures)
    {
        if (failures == 0)
        {
            return DailyWorkerUtils.TimeUntilNext(runAtUtc, DateTimeOffset.UtcNow);
        }

        var retry = FailureRetryBase;
        for (var doubling = 1; doubling < failures && retry < FailureRetryCeiling; doubling++)
        {
            retry += retry;
        }

        return retry > FailureRetryCeiling ? FailureRetryCeiling : retry;
    }
}
