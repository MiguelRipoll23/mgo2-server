using System.Net;
using System.Net.Sockets;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Udp;
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
    private readonly PeerSessionService sessions = new(TimeSpan.FromSeconds(60));
    private readonly int port;
    private UdpClient? socket;

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
    /// <param name="cancellationToken">Token that stops the host.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // The account owns the character the host plays as, and the registered
        // endpoint references it, so it has to exist before anything is written.
        await accountService.EnsureAccountAsync((int)hostIdentity.PeerIdentifier, cancellationToken);
        await RegisterConnectionAsync(cancellationToken);

        socket = new UdpClient(new IPEndPoint(IPAddress.Parse(options.ListeningIpAddress), port));
        sessions.Start();
        matchService.Start();
        logger.LogInformation("Listening on {Address}:{Port}", options.ListeningIpAddress, port);

        await ReceiveLoopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await matchService.StopAsync();
        sessions.Stop();
        socket?.Dispose();
        socket = null;
    }

    private async Task RegisterConnectionAsync(CancellationToken cancellationToken)
    {
        var advertisedHost = options.PublicHostAddress ?? "127.0.0.1";
        await gameService.SaveConnectionInformationAsync(
            (int)hostIdentity.PeerIdentifier,
            new ConnectionInformation(advertisedHost, port, advertisedHost, port),
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
                logger.LogDebug("Receive failed: {Message}", exception.Message);
                continue;
            }

            await HandleDatagramAsync(received.Buffer, received.RemoteEndPoint);
        }
    }
}
