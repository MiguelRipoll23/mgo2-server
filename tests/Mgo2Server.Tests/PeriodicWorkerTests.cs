using System.Diagnostics;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// Exercises the pacing every maintenance worker runs on. What has to hold is
/// that a healthy worker keeps its interval, a failing one backs off instead of
/// hammering, and a worker whose run overruns its interval does not turn into a
/// loop that runs back to back.
/// </summary>
[Trait("Category", "Shared")]
public sealed class PeriodicWorkerTests
{
    /// <summary>Interval the probe worker runs at, short enough to measure.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    /// <summary>How long a test waits for the worker to reach the point it measures.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>How long a test watches a worker before judging how often it ran.</summary>
    private static readonly TimeSpan Stretch = TimeSpan.FromMilliseconds(2000);

    [Fact]
    public async Task Keeps_its_interval_while_it_succeeds()
    {
        var worker = new ProbeWorker(Interval);
        worker.Start();
        await WaitForRunsAsync(worker, 4);
        await worker.StopAsync();

        // The waits the worker resolved, not the gaps between its runs. A gap
        // carries however long the machine took to come back to the thread, so
        // bounding it bounds the test host rather than the worker.
        var waits = worker.Waits();
        Assert.NotEmpty(waits);

        // Never shorter than the interval, and never past it by more than the
        // drift, which may only go upward.
        Assert.All(waits, wait => Assert.True(wait >= Interval, $"a wait of {wait} is under the interval"));
        Assert.All(waits, wait => Assert.True(wait <= Interval * 1.15, $"a wait of {wait} is over the interval"));
    }

    [Fact]
    public async Task Backs_off_while_it_keeps_failing()
    {
        var worker = new ProbeWorker(Interval) { FailUntilRun = int.MaxValue };
        worker.Start();
        await Task.Delay(Stretch);
        await worker.StopAsync();

        // A failing worker that kept to its interval would ask about twelve times
        // in this stretch. Backing off is what keeps it to a handful — the count is
        // the claim, because one wait measured against another is at the mercy of
        // whatever else the machine is doing.
        Assert.True(worker.RunCount is >= 3 and <= 6, $"the failing worker ran {worker.RunCount} times");

        var waits = worker.Waits();
        Assert.True(
            waits[^1] > waits[0],
            $"the last wait of {waits[^1]} was not longer than the first of {waits[0]}");
    }

    [Fact]
    public async Task Stops_backing_off_at_the_ceiling_it_was_given()
    {
        var worker = new ProbeWorker(Interval, ceiling: Interval * 2) { FailUntilRun = int.MaxValue };
        worker.Start();
        await Task.Delay(Stretch);
        await worker.StopAsync();

        // The resolved waits, which are where the cap is visible: a worker that
        // doubled without end would resolve a wait of four intervals or more within
        // a few failures. Asserting on the waits rather than on how many runs fitted
        // into the stretch keeps the claim about the worker's pacing and not about
        // how much of the two seconds this machine gave it.
        var waits = worker.Waits();
        Assert.True(waits.Count >= 3, $"only {waits.Count} waits were resolved");
        Assert.All(waits, wait => Assert.True(wait <= Interval * 2.2, $"a wait of {wait} went past the ceiling"));
        Assert.True(waits[^1] >= Interval, $"the last wait of {waits[^1]} was under the interval");
    }

    [Fact]
    public async Task Returns_to_its_interval_once_it_succeeds()
    {
        var worker = new ProbeWorker(Interval) { FailUntilRun = 2 };
        worker.Start();
        await Task.Delay(Stretch);
        await worker.StopAsync();

        // The waits the worker resolved, not the gaps between its runs. A gap
        // carries however long the machine took to get back to the thread, so
        // asserting on it measures the test host under load rather than the
        // worker's pacing. The claim is about the pacing, so it is read from the
        // pacing.
        var waits = worker.Waits();

        // Two failures pushed a wait out, and the run that succeeded brought it
        // back: a worker that recovered is not left slow. Both claims are compared
        // against the interval rather than against each other.
        Assert.True(
            waits.Count >= 2,
            $"only {waits.Count} waits were resolved, so the failure was never reached");
        Assert.True(
            waits.Max() > Interval * 1.5,
            $"no wait of {string.Join(", ", waits)} grew while the worker was failing");

        // The wait after the successful run is the interval plus its drift, which
        // is a tenth, so this is a bound rather than an exact value.
        Assert.True(
            waits[^1] < Interval * 1.5,
            $"the wait after a successful run was {waits[^1]}, which is still the failure backoff");
    }

    [Fact]
    public async Task Waits_after_a_run_that_took_longer_than_its_interval()
    {
        var runDuration = Interval * 3;
        var worker = new ProbeWorker(Interval) { RunDuration = runDuration };
        worker.Start();
        await WaitForRunsAsync(worker, 3);
        await worker.StopAsync();

        var gaps = worker.Gaps();

        // The wait follows the run rather than a clock: a run that overran its
        // interval delays the next one instead of the two running back to back, so
        // a gap carries the whole run plus the interval it waited afterwards.
        Assert.All(
            gaps,
            gap => Assert.True(
                gap >= runDuration + (Interval * 0.9),
                $"a gap of {gap} means the worker ran again as soon as its run finished"));
    }

    [Fact]
    public async Task Stops_while_it_is_waiting_out_a_backoff()
    {
        var worker = new ProbeWorker(Interval) { FailUntilRun = int.MaxValue };
        worker.Start();
        await WaitForRunsAsync(worker, 3);

        var elapsed = Stopwatch.StartNew();
        await worker.StopAsync();
        elapsed.Stop();

        Assert.False(worker.IsRunning);

        // The wait it was in is abandoned rather than slept through: a long
        // backoff must not hold a rollout.
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2), $"stopping took {elapsed.Elapsed}");
    }

    /// <summary>Waits until the worker has completed the given number of runs.</summary>
    /// <param name="worker">Worker being watched.</param>
    /// <param name="runs">Number of runs to wait for.</param>
    private static async Task WaitForRunsAsync(ProbeWorker worker, int runs)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (worker.RunCount >= runs)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        Assert.True(worker.RunCount >= runs, $"only {worker.RunCount} of {runs} runs happened");
    }

    /// <summary>
    /// A worker whose failures and duration are set by the test, so the pacing can
    /// be measured instead of guessed at.
    /// </summary>
    /// <param name="interval">Interval of the worker.</param>
    /// <param name="ceiling">Ceiling the worker backs off to, when the test sets one.</param>
    private sealed class ProbeWorker(TimeSpan interval, TimeSpan? ceiling = null)
        : PeriodicWorker(interval, NullLogger.Instance, ceiling)
    {
        private readonly Lock gate = new();
        private readonly List<DateTimeOffset> runs = [];

        /// <summary>Runs after which the worker stops failing, counting from one.</summary>
        public int FailUntilRun { get; init; }

        /// <summary>How long each run takes.</summary>
        public TimeSpan RunDuration { get; init; } = TimeSpan.Zero;

        /// <summary>Number of runs that have completed.</summary>
        public int RunCount
        {
            get
            {
                lock (gate)
                {
                    return runs.Count;
                }
            }
        }

        /// <inheritdoc />
        protected override async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            lock (gate)
            {
                runs.Add(DateTimeOffset.UtcNow);
            }

            if (RunDuration > TimeSpan.Zero)
            {
                await Task.Delay(RunDuration, cancellationToken);
            }

            if (runIndexForFailure++ < FailUntilRun)
            {
                throw new InvalidOperationException("the run this test asked for failed");
            }
        }

        private int runIndexForFailure;

        /// <summary>The waits the worker resolved, in the order it resolved them.</summary>
        public List<TimeSpan> Waits()
        {
            lock (gate)
            {
                return [.. waits];
            }
        }

        /// <inheritdoc />
        protected override TimeSpan ResolveWait(int failures)
        {
            var wait = base.ResolveWait(failures);
            lock (gate)
            {
                waits.Add(wait);
            }

            return wait;
        }

        private readonly List<TimeSpan> waits = [];

        /// <summary>Waits between each run and the one after it.</summary>
        public List<TimeSpan> Gaps()
        {
            lock (gate)
            {
                return [.. runs.Zip(runs.Skip(1), (previous, next) => next - previous)];
            }
        }
    }
}
