using Mgo2Server.AccountLobbyServer.Commands;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.AccountLobbyServer;

/// <summary>
/// Starts the account server this process serves. Character creation,
/// selection and deletion are its own process, so it can be restarted without
/// disturbing the gameplay lobbies. Its lobby row is described by the
/// environment rather than by a seeded row, so the server publishes the row when
/// it starts, exactly like a game lobby server.
/// </summary>
/// <param name="serviceProvider">Container the services come from.</param>
/// <param name="lobbyOptions">Identity of the endpoint this process serves.</param>
/// <param name="logger">Logger of the runner.</param>
public sealed class AccountLobbyServerRunner(
    IServiceProvider serviceProvider,
    IOptions<LobbyOptions> lobbyOptions,
    ILogger<AccountLobbyServerRunner> logger)
{
    private readonly LobbyOptions lobbyOptions = lobbyOptions.Value;
    private AccountServer? server;

    /// <summary>
    /// Registers the account server and starts its listener. The schema is not
    /// this process's business: the deployment applies the migrations before any
    /// server starts.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token that stops the listener. The call returns once the connections that
    /// listener was serving have left.
    /// </param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        AccountCommandHandlerRegistration.RegisterCommandHandlers(serviceProvider.GetRequiredService<CommandRegistry>());

        var lobbyService = serviceProvider.GetRequiredService<LobbyService>();
        var lobby = await lobbyService.RegisterEndpointLobbyAsync(
            LobbyType.Account,
            lobbyOptions,
            cancellationToken);

        logger.LogInformation(
            "Registered the account server as {LobbyIdentifier} on port {Port}",
            lobby.Identifier,
            lobby.Port);

        // The console host never starts the meter provider on its own, so it is
        // activated here and the totals of this lobby are published once, before
        // any change can happen. The account server tracks no players of its own.
        serviceProvider.ActivateServerTelemetry();
        await serviceProvider.GetRequiredService<ServerMetricsService>()
            .ReportLobbyTotalsAsync(lobby.Identifier, 0, cancellationToken);

        server = new AccountServer(serviceProvider, lobby.Port);
        await server.StartAsync(cancellationToken);

        // The listener is closed and nobody new can arrive, so what is left is to
        // let the players still connected leave on their own. Creation and deletion
        // are long screens to be cut out of mid-step, and a rollout replaces this
        // pod on the same port rather than taking anyone away from it.
        await server.WaitForConnectionsToLeaveAsync();
        server.CloseConnections();
    }

    /// <summary>Stops the listener.</summary>
    public void Stop()
    {
        server?.Stop();
        server = null;
    }
}
