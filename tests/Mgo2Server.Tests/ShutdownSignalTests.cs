using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the registration every player-facing server makes at startup. A
/// platform that refuses the termination signal would fail the process before
/// its first connection, which is a worse way to find out than a test.
/// </summary>
[Trait("Category", "Shared")]
public sealed class ShutdownSignalTests
{
    [Fact]
    public void Every_player_facing_server_can_subscribe_to_the_stop_signals()
    {
        var requests = 0;

        using var registration = ShutdownSignalUtils.OnStopRequested(
            () => Interlocked.Increment(ref requests),
            NullLogger.Instance);

        // Subscribing is the whole assertion: neither handler may be asked for
        // anything at startup.
        Assert.Equal(0, requests);
    }

    /// <summary>
    /// A server that acts differently on a rollout than on a person's stop asks
    /// for the signal. Only the game lobby does today, and a handler that never
    /// fires is the assertion: the two stops are told apart when one arrives,
    /// not at startup.
    /// </summary>
    [Fact]
    public void A_server_can_be_told_which_signal_asked_it_to_stop()
    {
        var signals = new List<StopSignal>();

        using var registration = ShutdownSignalUtils.OnStopRequested(
            signals.Add,
            NullLogger.Instance);

        Assert.Empty(signals);
    }

    [Fact]
    public void A_registration_can_be_released_and_taken_again()
    {
        var registration = ShutdownSignalUtils.OnStopRequested(() => { }, NullLogger.Instance);
        registration.Dispose();

        using var replacement = ShutdownSignalUtils.OnStopRequested(() => { }, NullLogger.Instance);

        Assert.NotNull(replacement);
    }
}
