using Mgo2Server.GameLobbyServer.Commands;
using Mgo2Server.GameLobbyServer.Maintenance;
using Mgo2Server.GameLobbyServer.Servers;
using Mgo2Server.Infrastructure.Persistence;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Tcp;
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

    /// <summary>Initializes the database, registers this instance's lobby and starts it.</summary>
    /// <param name="cancellationToken">Token that stops the listener.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        GameCommandHandlerRegistration.RegisterCommandHandlers(serviceProvider.GetRequiredService<CommandRegistry>());

        await serviceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(cancellationToken);

        var lobbyService = serviceProvider.GetRequiredService<LobbyService>();
        var lobby = await lobbyService.RegisterGameLobbyAsync(lobbyOptions, cancellationToken);

        logger.LogInformation(
            "Registered lobby {LobbyName} as {LobbyIdentifier} on port {Port}",
            lobby.Name,
            lobby.Identifier,
            lobby.Port);

        // The cache decides which lobbies this instance serves, and the lobby
        // just registered has to be in it before the listener starts.
        await lobbyService.LoadCacheAsync(cancellationToken);

        refresh = serviceProvider.GetRequiredService<LobbyCacheRefreshService>();
        heartbeat = serviceProvider.GetRequiredService<LobbyHeartbeatService>();
        cleanup = serviceProvider.GetRequiredService<LobbyCleanupService>();
        refresh.Start();
        heartbeat.StartFor(lobby.Identifier);
        cleanup.Start();

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
    }
}
