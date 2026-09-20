using Mgo2Server.GateLobbyServer.Commands;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GateLobbyServer;

/// <summary>
/// Starts the gate this process serves. The gate is the first connection point
/// of the game, so it runs as its own process and can be restarted on its own.
/// Its lobby row is described by the environment rather than by a seeded row, so
/// the server publishes the row when it starts, exactly like a game lobby server.
/// </summary>
/// <param name="serviceProvider">Container the services come from.</param>
/// <param name="lobbyOptions">Identity of the endpoint this process serves.</param>
/// <param name="logger">Logger of the runner.</param>
public sealed class GateLobbyServerRunner(
    IServiceProvider serviceProvider,
    IOptions<LobbyOptions> lobbyOptions,
    ILogger<GateLobbyServerRunner> logger)
{
    private readonly LobbyOptions lobbyOptions = lobbyOptions.Value;
    private GateServer? server;
    private LobbyCacheRefreshService? refresh;

    /// <summary>
    /// Registers the gate and starts its listener. The schema is not this
    /// process's business: the deployment applies the migrations before any
    /// server starts.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token that stops the listener. The call returns once the connections that
    /// listener was serving have left.
    /// </param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        GateCommandHandlerRegistration.RegisterCommandHandlers(serviceProvider.GetRequiredService<CommandRegistry>());

        var lobbyService = serviceProvider.GetRequiredService<LobbyService>();
        var lobby = await lobbyService.RegisterEndpointLobbyAsync(LobbyType.Gate, lobbyOptions, cancellationToken);

        logger.LogInformation(
            "Registered the gate as {LobbyIdentifier} on port {Port}",
            lobby.Identifier,
            lobby.Port);

        // The console host never starts the meter provider on its own, so it is
        // activated here and the totals of this lobby are published once, before
        // any change can happen. The gate tracks no players of its own.
        serviceProvider.ActivateServerTelemetry();
        await serviceProvider.GetRequiredService<ServerMetricsService>()
            .ReportLobbyTotalsAsync(lobby.Identifier, 0, cancellationToken);

        // The gate serves the lobby list, so its own row has to be in the cache
        // before the listener starts.
        await lobbyService.LoadCacheAsync(cancellationToken);

        // Gameplay lobbies register and expire while the gate runs, so the list
        // it serves has to be rebuilt instead of being fixed at startup.
        refresh = serviceProvider.GetRequiredService<LobbyCacheRefreshService>();
        refresh.Start();

        server = new GateServer(serviceProvider, lobby.Port);
        await server.StartAsync(cancellationToken);

        // The listener is closed and nobody new can arrive, so what is left is to
        // let the players still connected leave on their own. That wait is what the
        // deployment's termination grace period is for: a rollout replaces this pod
        // on the same port, and ending it here would hang up on everyone it is
        // waiting to replace.
        await server.WaitForConnectionsToLeaveAsync();
        server.CloseConnections();
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
