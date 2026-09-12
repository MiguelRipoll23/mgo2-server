using Mgo2Server.GateLobbyServer.Commands;
using Mgo2Server.Infrastructure.Persistence;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Tcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GateLobbyServer;

/// <summary>
/// Starts the gate this process serves. The gate is the first connection point
/// of the game, so it runs as its own process and can be restarted on its own.
/// </summary>
/// <param name="serviceProvider">Container the services come from.</param>
/// <param name="logger">Logger of the runner.</param>
public sealed class GateLobbyServerRunner(
    IServiceProvider serviceProvider,
    ILogger<GateLobbyServerRunner> logger)
{
    private GateServer? server;
    private LobbyCacheRefreshService? refresh;

    /// <summary>Initializes the database and starts the gate listener.</summary>
    /// <param name="cancellationToken">Token that stops the listener.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        GateCommandHandlerRegistration.RegisterCommandHandlers(serviceProvider.GetRequiredService<CommandRegistry>());

        await serviceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(cancellationToken);

        var lobbyService = serviceProvider.GetRequiredService<LobbyService>();
        await lobbyService.LoadCacheAsync(cancellationToken);

        // The port lives in the lobby row of type gate, so it is known only
        // once the cache has been loaded.
        var port = lobbyService.GetCached().First(lobby => lobby.Type == LobbyType.Gate).Port;
        logger.LogInformation("Hosting the gate on port {Port}", port);

        // Gameplay lobbies register and expire while the gate runs, so the list
        // it serves has to be rebuilt instead of being fixed at startup.
        refresh = serviceProvider.GetRequiredService<LobbyCacheRefreshService>();
        refresh.Start();

        server = new GateServer(serviceProvider, port);
        await server.StartAsync(cancellationToken);
    }

    /// <summary>Stops the listener and the cache refresh.</summary>
    public async Task StopAsync()
    {
        server?.Stop();
        server = null;

        if (refresh is not null)
        {
            await refresh.StopAsync();
            refresh = null;
        }
    }
}
