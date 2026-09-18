using Mgo2Server.GameLobbyServer.Commands;
using Mgo2Server.GameLobbyServer.Coordination;
using Mgo2Server.GameLobbyServer.Maintenance;
using Mgo2Server.GameLobbyServer.Servers;
using Mgo2Server.Shared.Domain.Automatch;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Domain.Presence;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer;

/// <summary>
/// Starts the one gameplay lobby this instance hosts. The lobby is described by
/// the environment rather than by a seeded row, so the server registers it in
/// the database when it starts. Each lobby is its own process, so a lobby can
/// be restarted independently of the others.
/// </summary>
/// <param name="serviceProvider">Container the services come from.</param>
/// <param name="lobbyOptions">Identity of the lobby this process hosts.</param>
/// <param name="logger">Logger of the runner.</param>
public sealed class GameLobbyServerRunner(
    IServiceProvider serviceProvider,
    IOptions<LobbyOptions> lobbyOptions,
    ILogger<GameLobbyServerRunner> logger)
{
    private readonly LobbyOptions lobbyOptions = lobbyOptions.Value;
    private GameplayLobbyServer? server;
    private LobbyCoordinationClientService? coordination;
    private LobbyCacheRefreshService? refresh;
    private LobbyHeartbeatService? heartbeat;
    private LobbyCleanupService? cleanup;
    private GameCleanupService? gameCleanup;
    private AutomatchTickerService? automatch;
    private CharacterPresenceTickerService? presenceTicker;

    /// <summary>
    /// Registers this instance's lobby and starts it. The schema is not this
    /// process's business: the deployment applies the migrations before any
    /// server starts.
    /// </summary>
    /// <param name="cancellationToken">Token that stops the listener.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        GameCommandHandlerRegistration.RegisterCommandHandlers(serviceProvider.GetRequiredService<CommandRegistry>());

        var lobbyService = serviceProvider.GetRequiredService<LobbyService>();
        var lobby = await lobbyService.RegisterGameLobbyAsync(lobbyOptions, cancellationToken);

        logger.LogInformation(
            "Registered lobby {LobbyName} as {LobbyIdentifier} on port {Port}",
            lobby.Name,
            lobby.Identifier,
            lobby.Port);

        // The console host never starts the meter provider on its own, so it is
        // activated here and the population of this lobby is published once,
        // before any change can happen. Every join and leave publishes it again.
        serviceProvider.ActivateServerTelemetry();
        var lobbyTracker = serviceProvider.GetRequiredService<LobbyTrackerService>();
        await serviceProvider.GetRequiredService<ServerMetricsService>()
            .ReportLobbyTotalsAsync(
                lobby.Identifier,
                lobbyTracker.GetPlayerCount(lobby.Identifier),
                cancellationToken);

        // Nobody is connected to a process that has just started, so every
        // presence row naming this lobby is stale by definition: they are
        // cleared exactly, rather than left for the sweep to time out. A count
        // above zero means a previous run stopped without processing its
        // departures, which is worth knowing about rather than hiding.
        var clearedPresences = await serviceProvider.GetRequiredService<CharacterPresenceService>()
            .ClearLobbyAsync(lobby.Identifier, cancellationToken);
        if (clearedPresences > 0)
        {
            logger.LogWarning(
                "Cleared {PresenceCount} presence rows left behind in lobby {LobbyIdentifier}",
                clearedPresences,
                lobby.Identifier);
        }

        // The population the row publishes is stale for the same reason, and unlike
        // presence nothing clears it: the row is keyed by port, so a restart lands on
        // the previous instance's row carrying the count that instance published, and
        // that count only moves on a join or a leave — neither of which can happen
        // until a client is served, long after this line. Every connection to this
        // lobby died with the process it was talking to, so the population is zero,
        // and it is published before the listener opens rather than after the first
        // player arrives.
        await lobbyService.UpdatePlayerCountAsync(lobby.Identifier, 0, cancellationToken);

        // The cache decides which lobbies this instance serves, and the lobby
        // just registered has to be in it before the listener starts.
        await lobbyService.LoadCacheAsync(cancellationToken);

        // The matchmaker is bound to the lobby this instance registered before it
        // is started: it asks the game layer about rooms in that lobby, and it
        // pushes only to sessions that are in it.
        var automatchService = serviceProvider.GetRequiredService<AutomatchService>();
        automatchService.SetLobby(
            lobby.Identifier,
            serviceProvider.GetRequiredService<AutomatchHooksService>());

        // Started before the listener, so the coordination stream is open before
        // the first player can arrive and the snapshot it registers with is the
        // empty population the lobby actually starts from.
        coordination = serviceProvider.GetRequiredService<LobbyCoordinationClientService>();
        coordination.StartFor(lobby.Identifier, lobby.Name);

        refresh = serviceProvider.GetRequiredService<LobbyCacheRefreshService>();
        heartbeat = serviceProvider.GetRequiredService<LobbyHeartbeatService>();
        cleanup = serviceProvider.GetRequiredService<LobbyCleanupService>();
        gameCleanup = serviceProvider.GetRequiredService<GameCleanupService>();
        automatch = serviceProvider.GetRequiredService<AutomatchTickerService>();
        presenceTicker = serviceProvider.GetRequiredService<CharacterPresenceTickerService>();
        refresh.Start();
        heartbeat.StartFor(lobby.Identifier);
        cleanup.Start();
        gameCleanup.Start();
        automatch.StartFor(lobby.Identifier, lobby.SubtypeIdentifier);
        presenceTicker.Start();

        server = new GameplayLobbyServer(serviceProvider, lobby.Port, lobby.Name, lobby.Identifier);
        await server.StartAsync(cancellationToken);
    }

    /// <summary>Stops the listener and the workers of this instance.</summary>
    public async Task StopAsync()
    {
        server?.Stop();
        server = null;

        if (coordination is not null)
        {
            await coordination.StopAsync();
            coordination = null;
        }

        if (refresh is not null)
        {
            await refresh.StopAsync();
            refresh = null;
        }

        if (heartbeat is not null)
        {
            await heartbeat.StopAsync();
            heartbeat = null;
        }

        if (cleanup is not null)
        {
            await cleanup.StopAsync();
            cleanup = null;
        }

        if (gameCleanup is not null)
        {
            await gameCleanup.StopAsync();
            gameCleanup = null;
        }

        if (automatch is not null)
        {
            await automatch.StopAsync();
            automatch = null;
        }

        if (presenceTicker is not null)
        {
            await presenceTicker.StopAsync();
            presenceTicker = null;
        }
    }
}
