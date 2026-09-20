using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the registration every player-facing server makes at startup. A
/// platform that refuses the termination signal would fail the process before
/// its first connection, which is a worse way to find out than a test.
/// </summary>
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

    [Fact]
    public void A_registration_can_be_released_and_taken_again()
    {
        var registration = ShutdownSignalUtils.OnStopRequested(() => { }, NullLogger.Instance);
        registration.Dispose();

        using var replacement = ShutdownSignalUtils.OnStopRequested(() => { }, NullLogger.Instance);

        Assert.NotNull(replacement);
    }
}
