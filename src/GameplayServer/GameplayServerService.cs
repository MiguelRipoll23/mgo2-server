using System.Net;
using System.Net.Sockets;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Udp;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The standalone Gameplay server: it owns one UDP port, decodes the peer-to-peer
/// frames, keeps a session per peer and dispatches the messages to their
/// handlers. Handshake acceptance and keep-alive acknowledgements are ordinary
/// command handlers, so this class stays transport-only. The frame pipeline it
/// feeds lives in the dispatch half of this class.
/// </summary>
public sealed partial class GameplayServerService : IAsyncDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly GameService gameService;
    private readonly AccountService accountService;
    private readonly MatchService matchService;
    private readonly HostIdentityService hostIdentity;
    private readonly PeerCommandRegistry registry;
    private readonly ILogger<GameplayServerService> logger;
    private readonly ServerOptions options;
    private readonly PeerSessionService sessions;
    private readonly CancellationTokenSource receiveLifetime = new();
    private readonly int port;
    private UdpClient? socket;

    /// <summary>
    /// Set once a stop has been asked for. Peers already in a match keep being
    /// served, and no further peer is taken on: a handshake answered by an
    /// instance that is on its way out would be a joiner waiting on a host that
    /// is going to leave.
    /// </summary>
    private volatile bool draining;

    /// <summary>Creates the gameplay server of one port.</summary>
    /// <param name="serviceProvider">Container the handlers are resolved from.</param>
    /// <param name="options">Options of this instance.</param>
    /// <param name="logger">Logger of this host.</param>
    public GameplayServerService(
        IServiceProvider serviceProvider,
        IOptions<ServerOptions> options,
        ILogger<GameplayServerService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.options = options.Value;
        port = this.options.GameplayServerPort;
        sessions = new PeerSessionService(TimeSpan.FromSeconds(60), logger, $"udp:{port}");
        gameService = serviceProvider.GetRequiredService<GameService>();
        accountService = serviceProvider.GetRequiredService<AccountService>();
        matchService = serviceProvider.GetRequiredService<MatchService>();
        hostIdentity = serviceProvider.GetRequiredService<HostIdentityService>();
        registry = serviceProvider.GetRequiredService<PeerCommandRegistry>();
    }

    private string LogPrefix => $"udp:{port}";

    /// <summary>
    /// Publishes the host account, binds the port, registers the host endpoint
    /// and starts the match maintenance, then receives until it is stopped.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token that stops the host. The call returns once the peers it was serving
    /// have left.
    /// </param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // The account owns the character the host plays as, and the registered
        // endpoint references it, so it has to exist before anything is written.
        await StartupUtils.RetryAsync(
            "publish the host account",
            token => accountService.EnsureAccountAsync((int)hostIdentity.PeerIdentifier, token),
            logger,
            cancellationToken);
        await StartupUtils.RetryAsync("register the host endpoint", RegisterConnectionAsync, logger, cancellationToken);

        socket = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        sessions.Start();
        matchService.Start();
        logger.LogInformation("[{LogPrefix}] Listening on port {Port}", LogPrefix, port);

        // Receiving does not run on the token that asks for the stop. A stop ends
        // the matches that are playing when they end, not where they are, and a
        // datagram nobody reads is a match that has already broken. The loop is
        // held open until the peers have gone, and closed below instead.
        var receiveLoop = ReceiveLoopAsync(receiveLifetime.Token);
        await WaitForStopAsync(cancellationToken);

        draining = true;
        await ConnectionDrainUtils.WaitForConnectionsToLeaveAsync(LogPrefix, () => sessions.Count, logger);

        receiveLifetime.Cancel();
        await receiveLoop;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await matchService.StopAsync();
        sessions.Stop();
        socket?.Dispose();
        socket = null;
        receiveLifetime.Dispose();
    }

    /// <summary>Completes once a stop has been asked for.</summary>
    /// <param name="cancellationToken">Token that stops the host.</param>
    private static async Task WaitForStopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The stop that was waited for.
        }
    }

    private async Task RegisterConnectionAsync(CancellationToken cancellationToken)
    {
        // The address published here is the same one the host puts in its own
        // handshake, read from the same property, so a joining client is never
        // handed one endpoint and given another to answer on.
        await gameService.SaveConnectionInformationAsync(
            (int)hostIdentity.PeerIdentifier,
            new ConnectionInformation(
                hostIdentity.AdvertisedAddress,
                port,
                hostIdentity.AdvertisedAddress,
                port),
            cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && socket is not null)
        {
            UdpReceiveResult received;
            try
            {
                received = await socket.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException exception)
            {
                // A dial-back send to an unreachable endpoint surfaces as an
                // ICMP port-unreachable on the next receive.
                logger.LogDebug("[{LogPrefix}] Receive failed: {Message}", LogPrefix, exception.Message);
                continue;
            }

            await HandleDatagramAsync(received.Buffer, received.RemoteEndPoint);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
