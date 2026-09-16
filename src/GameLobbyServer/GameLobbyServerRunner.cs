using Mgo2Server.GameLobbyServer.Commands;
using Mgo2Server.GameLobbyServer.Maintenance;
using Mgo2Server.GameLobbyServer.Servers;
using Mgo2Server.Shared.Domain.Automatch;
using Mgo2Server.Shared.Domain.Lobbies;
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
    private LobbyCacheRefreshService? refresh;
    private LobbyHeartbeatService? heartbeat;
    private LobbyCleanupService? cleanup;
    private GameCleanupService? gameCleanup;
    private AutomatchTickerService? automatch;

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

        refresh = serviceProvider.GetRequiredService<LobbyCacheRefreshService>();
        heartbeat = serviceProvider.GetRequiredService<LobbyHeartbeatService>();
        cleanup = serviceProvider.GetRequiredService<LobbyCleanupService>();
        gameCleanup = serviceProvider.GetRequiredService<GameCleanupService>();
        automatch = serviceProvider.GetRequiredService<AutomatchTickerService>();
        refresh.Start();
        heartbeat.StartFor(lobby.Identifier);
        cleanup.Start();
        gameCleanup.Start();
        automatch.StartFor(lobby.Identifier, lobby.SubtypeIdentifier);

        server = new GameplayLobbyServer(serviceProvider, lobby.Port, lobby.Name, lobby.Identifier);
        await server.StartAsync(cancellationToken);
    }

    /// <summary>Stops the listener and the workers of this instance.</summary>
    public async Task StopAsync()
    {
        server?.Stop();
        server = null;

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
    }
}
