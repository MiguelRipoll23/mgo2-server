using System.Net;
using System.Net.Sockets;
using Mgo2Server.GameplayServer.Gameplay;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Udp;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The standalone Gameplay server: it owns one UDP port, decodes the peer-to-peer
/// frames, keeps a session per peer and dispatches the messages to their
/// handlers. Handshake acceptance and keep-alive handling are ordinary command
/// handlers, so this class stays transport-only. The frame pipeline it
/// feeds lives in the dispatch half of this class.
/// </summary>
public sealed partial class GameplayServerService : IAsyncDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly GameService gameService;
    private readonly CharacterService characterService;
    private readonly AccountService accountService;
    private readonly MatchService matchService;
    private readonly RoomRosterService roster;
    private readonly MatchRecords.MatchStartRunService matchStart;
    private readonly BriefingStateService briefingStates;
    private readonly PlayerVitalsService vitals;
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
        sessions = new PeerSessionService(
            TimeSpan.FromSeconds(60),
            logger,
            $"udp:{port}",
            DropReapedPeer);
        gameService = serviceProvider.GetRequiredService<GameService>();
        characterService = serviceProvider.GetRequiredService<CharacterService>();
        accountService = serviceProvider.GetRequiredService<AccountService>();
        matchService = serviceProvider.GetRequiredService<MatchService>();
        roster = serviceProvider.GetRequiredService<RoomRosterService>();
        matchStart = serviceProvider.GetRequiredService<MatchRecords.MatchStartRunService>();
        briefingStates = serviceProvider.GetRequiredService<BriefingStateService>();
        vitals = serviceProvider.GetRequiredService<PlayerVitalsService>();
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
        logger.LogDebug(
            "[{LogPrefix}] Preparing gameplay host account, character and connection endpoint",
            LogPrefix);

        // The account owns the character the host plays as, and the registered
        // endpoint references it, so it has to exist before anything is written.
        await StartupUtils.RetryAsync(
            "publish the host account and character settings",
            async token =>
            {
                var characterIdentifier = await accountService.GetOrCreateCharacterIdentifierAsync(token);
                hostIdentity.SetCharacterIdentifier(characterIdentifier);
                await characterService.EnsureDedicatedHostSettingsAsync(characterIdentifier, token);
            },
            logger,
            cancellationToken);
        logger.LogDebug(
            "[{LogPrefix}] Host character {CharacterIdentifier} ({CharacterName}) is ready",
            LogPrefix,
            hostIdentity.PeerIdentifier,
            hostIdentity.CharacterName);
        await StartupUtils.RetryAsync("register the host endpoint", RegisterConnectionAsync, logger, cancellationToken);

        socket = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        logger.LogDebug(
            "[{LogPrefix}] UDP socket bound to 0.0.0.0:{Port}",
            LogPrefix,
            port);
        sessions.Start();
        logger.LogDebug("[{LogPrefix}] Peer idle-session reaper started", LogPrefix);
        matchService.Start();
        logger.LogDebug(
            "[{LogPrefix}] Match publisher started; match heartbeats are intentionally not logged",
            LogPrefix);
        logger.LogInformation("[{LogPrefix}] Listening on port {Port}", LogPrefix, port);

        // Receiving does not run on the token that asks for the stop. A stop ends
        // the matches that are playing when they end, not where they are, and a
        // datagram nobody reads is a match that has already broken. The loop is
        // held open until the peers have gone, and closed below instead.
        var receiveLoop = ReceiveLoopAsync(receiveLifetime.Token);
        await WaitForStopAsync(cancellationToken);

        draining = true;
        logger.LogDebug(
            "[{LogPrefix}] Stop requested; entering drain mode with {SessionCount} peer sessions",
            LogPrefix,
            sessions.Count);
        await ConnectionDrainUtils.WaitForConnectionsToLeaveAsync(LogPrefix, () => sessions.Count, logger);

        receiveLifetime.Cancel();
        await receiveLoop;
        logger.LogDebug("[{LogPrefix}] Receive loop stopped after peer drain", LogPrefix);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        logger.LogDebug("[{LogPrefix}] Disposing gameplay server services", LogPrefix);
        await matchService.StopAsync();
        sessions.Stop();
        socket?.Dispose();
        socket = null;
        receiveLifetime.Dispose();
        logger.LogDebug("[{LogPrefix}] Gameplay server services disposed", LogPrefix);
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
        hostIdentity.LogAdvertisedEndpoints(logger);

        // The address published here is the same one the host puts in its own
        // handshake, read from the same property, so a joining client is never
        // handed one endpoint and given another to answer on.
        logger.LogDebug(
            "[{LogPrefix}] Registering host endpoint {Address}:{Port} for character {PeerIdentifier}",
            LogPrefix,
            hostIdentity.AdvertisedAddress,
            port,
            hostIdentity.PeerIdentifier);
        await gameService.SaveConnectionInformationAsync(
            (int)hostIdentity.PeerIdentifier,
            new ConnectionInformation(
                hostIdentity.AdvertisedAddress,
                port,
                hostIdentity.AdvertisedAddress,
                port),
            cancellationToken);
        logger.LogDebug(
            "[{LogPrefix}] Host endpoint registered for character {PeerIdentifier}",
            LogPrefix,
            hostIdentity.PeerIdentifier);
    }

    /// <summary>
    /// Takes a peer off the room roster when its session is reaped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reaper is the only place a peer is let go, so it is the only place
    /// the roster can hear that one has left. Without this the roster keeps
    /// every peer that ever sent a profile, and hands each one to every later
    /// joiner in the roster run - with the endpoint the peer announced, which is
    /// a real address a real client will dial.
    /// </para>
    /// <para>
    /// Both endpoints are cleared. The roster is filed under the endpoint the
    /// profile arrived from, which is the observed source, while the session
    /// is keyed on the one its handshake arrived from and answers at the one the
    /// peer advertised; those three are the same for a peer out on the internet
    /// and differ wherever something rewrites the source in between.
    /// </para>
    /// </remarks>
    /// <param name="session">Session the reaper has dropped.</param>
    private void DropReapedPeer(PeerSession session)
    {
        // What the peer did with what it was sent, read back at the one moment
        // the session is known to be over. A roster that was never acknowledged
        // is the stalled join: the client keeps asking for the room roster and
        // never reaches its data phase (`P2P_CONNECT_FSM.md` §7.1b, and the
        // measurement in `docs/protocol/UDP_GAME_CAPTURE.md` §8.6 that the
        // recorded host's roster draws an answer 56 ms after it arrives and
        // this one draws none). Nothing on the wire says so while the join is
        // still in progress, which is why it is reported here.
        logger.LogInformation(
            "[{LogPrefix}] Peer {RemoteAddress} acknowledged {AcknowledgedCount} record(s) before going "
            + "quiet; roster acknowledged: {RosterAcknowledged}",
            LogPrefix,
            session.RemoteAddress,
            session.AcknowledgedRecords.Count,
            session.RosterAcknowledged ? "yes" : "no");

        // The briefing state the peer reported goes with it. A ready state kept
        // for a player who has left would hold a round shut on somebody who is
        // never going to report, which is a room waiting for nothing.
        if (briefingStates.Forget(session.RemoteAddress) | briefingStates.Forget(session.DialBack.ToString()))
        {
            logger.LogDebug(
                "[{LogPrefix}] Cleared the briefing state of reaped peer {RemoteAddress}",
                LogPrefix,
                session.RemoteAddress);
        }

        // The vitals it last reported go with it, for the same reason: a health
        // kept for a player who has left is a value about nobody, and a reader
        // that took it for a live one would be reading the round before.
        if (vitals.Forget(session.RemoteAddress) | vitals.Forget(session.DialBack.ToString()))
        {
            logger.LogDebug(
                "[{LogPrefix}] Cleared the vitals of reaped peer {RemoteAddress}",
                LogPrefix,
                session.RemoteAddress);
        }

        // Both lookups are made: '|' and not '||', because the second endpoint
        // has to be tried even when the first one found the entry.
        var removedObservedEndpoint = roster.Remove(session.RemoteAddress);
        var removedDialBackEndpoint = roster.Remove(session.DialBack.ToString());
        if (removedObservedEndpoint | removedDialBackEndpoint)
        {
            logger.LogInformation(
                "[{LogPrefix}] Took {RemoteAddress} off the room roster: its session was reaped",
                LogPrefix,
                session.RemoteAddress);
        }
        else
        {
            logger.LogDebug(
                "[{LogPrefix}] Reaped peer {RemoteAddress} had no roster entry to remove",
                LogPrefix,
                session.RemoteAddress);
        }

        // The room's round belongs to the clients in it, so the last client out is
        // the one that ends the match: with the room empty the round count, its
        // clock and its tickets start over, and the next client into the room
        // arrives at a first round rather than at the one that emptied. A leaver
        // among others changes nothing (`MatchStartRunService.ResetWhenEmpty`, and
        // the room's own rule in `docs/analysis/empty-and-solo-room-clocks.md`).
        if (matchStart.ResetWhenEmpty())
        {
            logger.LogInformation(
                "[{LogPrefix}] {RemoteAddress} was the room's last client; the room's round, clock and tickets "
                + "start over for the next player to join",
                LogPrefix,
                session.RemoteAddress);
        }
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
