using Microsoft.Extensions.Logging;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Runs one operation on a fixed interval until it is stopped. The first run
/// happens immediately, so a worker that keeps a row alive registers it before
/// the first interval elapses.
/// <para>
/// A failing run is logged and the loop continues, because a database that is
/// briefly unavailable must not end the process. What it must not do is keep
/// asking at the same rate: a failure is retried after a growing delay, so a
/// database that is away is not hammered by every worker of every instance, and
/// a successful run puts the worker back on its interval.
/// </para>
/// <para>
/// The wait between runs is measured from the end of the run, never scheduled by
/// a clock. A run that takes longer than the interval therefore delays the next
/// one instead of collapsing into a loop that runs back to back — which is what
/// a slow database would otherwise turn every worker into.
/// </para>
/// <para>
/// The wait is never shorter than the interval, and drifts upward by up to a
/// tenth of it. Every instance of a role starts together with the same interval,
/// and without the drift they would all reach the database in the same instant.
/// </para>
/// </summary>
/// <param name="interval">Time between two runs.</param>
/// <param name="logger">Logger of the worker.</param>
/// <param name="failureBackoffCeiling">
/// Longest the worker waits between two attempts while it keeps failing, and
/// never shorter than the interval. The default suits a worker whose work can be
/// late without being wrong; a worker whose cadence is itself the signal — a
/// heartbeat, or a tick somebody is waiting on — passes the ceiling its own
/// timing allows, or <see cref="NoBackoff"/> when no delay beyond the interval is
/// safe.
/// </param>
public abstract class PeriodicWorker(
    TimeSpan interval,
    ILogger logger,
    TimeSpan? failureBackoffCeiling = null)
{
    /// <summary>Longest a worker waits between attempts unless it says otherwise.</summary>
    private static readonly TimeSpan DefaultBackoffCeiling = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Ceiling that leaves the interval as the only wait, for a worker whose
    /// cadence is what it is for: the row a heartbeat stamps is removed once it
    /// goes quiet for twice the interval, so a heartbeat that backed off would
    /// delete the thing it keeps.
    /// </summary>
    public static TimeSpan NoBackoff => TimeSpan.Zero;

    /// <summary>Fraction of a wait a worker may drift upward by.</summary>
    private const double JitterFraction = 0.1;

    /// <summary>Failed runs between two reports of a failure that does not stop.</summary>
    private const int FailuresPerReport = 10;

    /// <summary>Longest this worker waits between two attempts while it keeps failing.</summary>
    private readonly TimeSpan backoffCeiling = ResolveCeiling(failureBackoffCeiling, interval);

    private CancellationTokenSource? cancellation;
    private Task? loop;

    /// <summary>Whether the worker is running.</summary>
    public bool IsRunning => loop is not null;

    /// <summary>Starts the loop, or does nothing when it already runs.</summary>
    public void Start()
    {
        if (loop is not null)
        {
            return;
        }

        cancellation = new CancellationTokenSource();
        loop = RunAsync(cancellation.Token);
    }

    /// <summary>Stops the loop and waits for the run that is in flight.</summary>
    public async Task StopAsync()
    {
        if (cancellation is null || loop is null)
        {
            return;
        }

        await cancellation.CancelAsync();

        try
        {
            await loop;
        }
        catch (OperationCanceledException)
        {
            // The loop was cancelled, which is how it stops.
        }

        cancellation.Dispose();
        cancellation = null;
        loop = null;
    }

    /// <summary>Performs one run of the worker.</summary>
    /// <param name="cancellationToken">Token that stops the run.</param>
    protected abstract Task RunOnceAsync(CancellationToken cancellationToken);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var failures = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(cancellationToken);

                if (failures > 0)
                {
                    logger.LogInformation(
                        "{Worker} recovered after {FailureCount} failed run(s)",
                        GetType().Name,
                        failures);
                    failures = 0;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                failures++;
                ReportFailure(exception, failures);
            }

            if (!await WaitBeforeNextRunAsync(failures, cancellationToken))
            {
                return;
            }
        }
    }

    /// <summary>Waits out the delay the run that just finished has earned.</summary>
    /// <param name="failures">Consecutive failures the run left behind.</param>
    /// <param name="cancellationToken">Token that stops the wait.</param>
    /// <returns><c>false</c> when the worker was stopped while waiting.</returns>
    private async Task<bool> WaitBeforeNextRunAsync(int failures, CancellationToken cancellationToken)
    {
        var wait = failures == 0 ? interval : DelayAfterFailure(failures);

        // Upward only, so the interval stays a floor: a worker that is keeping up
        // never runs more often than it was configured to.
        var jittered = wait + (wait * (Random.Shared.NextDouble() * JitterFraction));

        try
        {
            await Task.Delay(jittered, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>How long a worker waits after the given number of consecutive failures.</summary>
    /// <param name="failures">Consecutive failures the worker has had.</param>
    private TimeSpan DelayAfterFailure(int failures)
    {
        var delay = interval;
        for (var doubling = 1; doubling < failures && delay < backoffCeiling; doubling++)
        {
            delay += delay;
        }

        return delay > backoffCeiling ? backoffCeiling : delay;
    }

    /// <summary>
    /// Reports a failed run. The first failure carries the full detail, because it
    /// is the one that explains the rest; after that the line returns only every
    /// so often, and it says how many failures there have been and when the worker
    /// will be back, so a silent stretch of log is not mistaken for a stuck worker.
    /// </summary>
    /// <param name="exception">Failure the run ended with.</param>
    /// <param name="failures">Consecutive failures the worker has had.</param>
    private void ReportFailure(Exception exception, int failures)
    {
        var nextAttempt = DelayAfterFailure(failures).TotalSeconds;

        if (failures == 1)
        {
            logger.LogError(
                exception,
                "{Worker} run failed; the next attempt is in {NextAttempt}s",
                GetType().Name,
                nextAttempt);
            return;
        }

        if (failures % FailuresPerReport == 0)
        {
            logger.LogWarning(
                "{Worker} has failed {FailureCount} times in a row; the next attempt is in {NextAttempt}s",
                GetType().Name,
                failures,
                nextAttempt);
        }
    }

    /// <summary>
    /// Resolves the ceiling a worker backs off to, which is never below its own
    /// interval — a ceiling that did would make a failing worker run more often
    /// than a healthy one.
    /// </summary>
    /// <param name="requested">Ceiling the worker asked for, when it did.</param>
    /// <param name="interval">Interval of the worker.</param>
    private static TimeSpan ResolveCeiling(TimeSpan? requested, TimeSpan interval)
    {
        if (requested == NoBackoff)
        {
            return interval;
        }

        var ceiling = requested ?? DefaultBackoffCeiling;
        return ceiling > interval ? ceiling : interval;
    }
}
// no-op
