using System.Diagnostics;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// Exercises the wait a rollout leans on. A server that is being replaced keeps
/// the connections it has until they leave, so the two failures that matter are
/// stopping while somebody is still connected and never stopping at all.
/// </summary>
[Trait("Category", "Shared")]
public sealed class ConnectionDrainTests
{
    /// <summary>How long the drain is given to notice a change in the count.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>Connections still being served, as the wait reads them.</summary>
    private int liveConnections;

    [Fact]
    public async Task Returns_at_once_when_nothing_is_connected()
    {
        var elapsed = Stopwatch.StartNew();

        var drained = await ConnectionDrainUtils.WaitForConnectionsToLeaveAsync(
            "tcp:test",
            () => Volatile.Read(ref liveConnections),
            NullLogger.Instance);

        elapsed.Stop();

        Assert.True(drained);

        // A lobby that was rolled out empty must not hold the rollout for a poll.
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), $"waited {elapsed.Elapsed}");
    }

    [Fact]
    public async Task Waits_until_the_last_connection_leaves()
    {
        Volatile.Write(ref liveConnections, 2);

        var leaving = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            Volatile.Write(ref liveConnections, 1);
            await Task.Delay(TimeSpan.FromSeconds(1));
            Volatile.Write(ref liveConnections, 0);
        });

        var elapsed = Stopwatch.StartNew();
        var drained = await ConnectionDrainUtils.WaitForConnectionsToLeaveAsync(
            "tcp:test",
            () => Volatile.Read(ref liveConnections),
            NullLogger.Instance);
        elapsed.Stop();
        await leaving;

        Assert.True(drained);

        // Both connections left, so the drain could not have returned before the
        // second one did.
        Assert.True(
            elapsed.Elapsed >= TimeSpan.FromSeconds(2),
            $"stopped after {elapsed.Elapsed}, before the connections had left");
    }

    [Fact]
    public async Task Abandons_the_wait_when_it_is_cancelled()
    {
        Volatile.Write(ref liveConnections, 1);
        using var cancellation = new CancellationTokenSource();

        var waiting = ConnectionDrainUtils.WaitForConnectionsToLeaveAsync(
            "tcp:test",
            () => Volatile.Read(ref liveConnections),
            NullLogger.Instance,
            cancellation.Token);

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        cancellation.Cancel();

        var drained = await waiting.WaitAsync(Patience);

        Assert.False(drained);
    }
}
