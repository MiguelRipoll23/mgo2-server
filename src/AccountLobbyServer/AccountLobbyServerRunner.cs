using Mgo2Server.AccountLobbyServer.Commands;
using Mgo2Server.Infrastructure.Persistence;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Tcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.AccountLobbyServer;

/// <summary>
/// Starts the account server this process serves. Character creation,
/// selection and deletion are its own process, so it can be restarted without
/// disturbing the gameplay lobbies.
/// </summary>
/// <param name="serviceProvider">Container the services come from.</param>
/// <param name="logger">Logger of the runner.</param>
public sealed class AccountLobbyServerRunner(
    IServiceProvider serviceProvider,
    ILogger<AccountLobbyServerRunner> logger)
{
    private AccountServer? server;

    /// <summary>Initializes the database and starts the account listener.</summary>
    /// <param name="cancellationToken">Token that stops the listener.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        AccountCommandHandlerRegistration.RegisterCommandHandlers(serviceProvider.GetRequiredService<CommandRegistry>());

        await serviceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(cancellationToken);

        var lobbyService = serviceProvider.GetRequiredService<LobbyService>();
        await lobbyService.LoadCacheAsync(cancellationToken);

        // The port lives in the lobby row of type account, so it is known only
        // once the cache has been loaded.
        var port = lobbyService.GetCached().First(lobby => lobby.Type == LobbyType.Account).Port;
        logger.LogInformation("Hosting the account server on port {Port}", port);

        server = new AccountServer(serviceProvider, port);
        await server.StartAsync(cancellationToken);
    }

    /// <summary>Stops the listener.</summary>
    public void Stop()
    {
        server?.Stop();
        server = null;
    }
}
