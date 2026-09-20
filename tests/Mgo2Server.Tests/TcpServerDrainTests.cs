using System.Net;
using System.Net.Sockets;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// Exercises a rollout against a real listener: a stop closes the door and does
/// not hang up on the clients already inside, and hanging up waits until it is
/// the only thing left to do.
/// </summary>
public sealed class TcpServerDrainTests
{
    /// <summary>How long a step of a test waits before it gives up.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Stops_accepting_without_disconnecting_the_client_it_has()
    {
        var port = ReservePort();
        var server = new TestTcpServer(BuildServices(), port);
        using var stopRequested = new CancellationTokenSource();
        var serving = server.StartAsync(stopRequested.Token);

        using var player = new TcpClient();
        await player.ConnectAsync(IPAddress.Loopback, port);
        await WaitForAsync(() => server.LiveConnectionCount == 1);

        stopRequested.Cancel();
        await serving.WaitAsync(Patience);

        // Nobody new is let in: the listener is gone, not merely quiet.
        using var latecomer = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(
            () => latecomer.ConnectAsync(IPAddress.Loopback, port));

        // The player who was already in is still connected, and still counted.
        Assert.Equal(1, server.LiveConnectionCount);
        Assert.False(await ReadReturnsEndOfStreamAsync(player, TimeSpan.FromMilliseconds(250)));

        // And the drain is what the instance stops on, so it does not end while
        // somebody is still there.
        var drain = server.WaitForConnectionsToLeaveAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        Assert.False(drain.IsCompleted, "the drain ended while a client was still connected");

        player.Close();

        Assert.True(await drain.WaitAsync(Patience), "the drain did not end after the client left");
        await WaitForAsync(() => server.LiveConnectionCount == 0);
    }

    [Fact]
    public async Task Hangs_up_once_the_wait_for_a_client_is_over()
    {
        var port = ReservePort();
        var server = new TestTcpServer(BuildServices(), port);
        using var stopRequested = new CancellationTokenSource();
        var serving = server.StartAsync(stopRequested.Token);

        using var player = new TcpClient();
        await player.ConnectAsync(IPAddress.Loopback, port);
        await WaitForAsync(() => server.LiveConnectionCount == 1);

        stopRequested.Cancel();
        await serving.WaitAsync(Patience);

        // A client that never leaves is what a drained rollout cannot wait for
        // forever, so the connection is closed deliberately once the wait ends.
        server.CloseConnections();

        Assert.True(
            await ReadReturnsEndOfStreamAsync(player, Patience),
            "the client was never hung up on");
        await WaitForAsync(() => server.LiveConnectionCount == 0);
    }

    /// <summary>Reads once, and reports whether the read ended the stream.</summary>
    /// <param name="client">Client to read from.</param>
    /// <param name="timeout">How long the read is given.</param>
    private static async Task<bool> ReadReturnsEndOfStreamAsync(TcpClient client, TimeSpan timeout)
    {
        using var patience = new CancellationTokenSource(timeout);
        try
        {
            return await client.GetStream().ReadAsync(new byte[1], patience.Token) == 0;
        }
        catch (OperationCanceledException)
        {
            // Still connected: nothing arrived and nothing was closed.
            return false;
        }
    }

    /// <summary>Waits for a condition the server reaches on its own thread.</summary>
    /// <param name="condition">Condition to wait for.</param>
    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        Assert.True(condition(), "the condition was never met");
    }

    /// <summary>Builds the container a listener needs, which is only a logger factory.</summary>
    private static IServiceProvider BuildServices() =>
        new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .BuildServiceProvider();

    /// <summary>Binds a port and releases it, so the listener can have it.</summary>
    private static int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }

    /// <summary>A server with no commands, which is enough to accept and to count.</summary>
    /// <param name="serviceProvider">Container the server resolves from.</param>
    /// <param name="port">Port to listen on.</param>
    private sealed class TestTcpServer(IServiceProvider serviceProvider, int port)
        : TcpServerBase(serviceProvider, port)
    {
        /// <inheritdoc />
        protected override ServerType ServerType => ServerType.Gate;
    }
}
