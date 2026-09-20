using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// Exercises the retry a server runs its startup database work through. A
/// database that is briefly away has to cost a wait, not a process, and one that
/// is really broken still has to be reported.
/// </summary>
[Trait("Category", "Shared")]
public sealed class StartupUtilsTests
{
    /// <summary>Wait the tests give the retry, so a give-up path finishes in milliseconds.</summary>
    private static readonly TimeSpan TestDelay = TimeSpan.FromMilliseconds(10);

    [Fact]
    public async Task Retries_a_failure_and_returns_what_the_later_attempt_produced()
    {
        var attempts = 0;

        var result = await StartupUtils.RetryAsync(
            "a step of this test",
            _ =>
            {
                attempts++;
                return attempts < 2
                    ? Task.FromException<int>(new InvalidOperationException("the first attempt failed"))
                    : Task.FromResult(42);
            },
            NullLogger.Instance,
            initialDelay: TestDelay);

        Assert.Equal(2, attempts);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Gives_up_after_its_attempts_and_reports_the_failure()
    {
        var attempts = 0;

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StartupUtils.RetryAsync<int>(
                "a step that never works",
                _ =>
                {
                    attempts++;
                    return Task.FromException<int>(new InvalidOperationException("this step never works"));
                },
                NullLogger.Instance,
                initialDelay: TestDelay));

        // Five attempts, the first one included, and the reason it gave up is the
        // failure of the last one rather than something of its own.
        Assert.Equal(5, attempts);
        Assert.Equal("this step never works", failure.Message);
    }

    [Fact]
    public async Task Stops_retrying_when_the_startup_is_cancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;

        var retrying = StartupUtils.RetryAsync<int>(
            "a step of a startup that was cancelled",
            _ =>
            {
                attempts++;
                cancellation.Cancel();
                return Task.FromException<int>(new InvalidOperationException("this step failed"));
            },
            NullLogger.Instance,
            cancellation.Token,
            TestDelay);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retrying);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Best_effort_swallows_a_failure_and_lets_the_server_start()
    {
        var attempted = false;

        await StartupUtils.BestEffortAsync(
            "a step that does not matter",
            _ =>
            {
                attempted = true;
                return Task.FromException(new InvalidOperationException("this step failed"));
            },
            NullLogger.Instance);

        Assert.True(attempted);
    }

    [Fact]
    public async Task Best_effort_still_honours_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            StartupUtils.BestEffortAsync(
                "a step of a startup that was cancelled",
                token => Task.FromCanceled(token),
                NullLogger.Instance,
                cancellation.Token));
    }
}
