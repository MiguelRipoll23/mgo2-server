using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Removes the gameplay lobbies that stopped being heartbeated, so a lobby
/// container that was deleted or killed does not linger in the lobby list. Only
/// a game lobby server runs it: the gate and the account server are permanent
/// and the Gameplay servers own matches, not lobbies.
/// </summary>
/// <param name="lobbyService">Service that owns the lobby rows.</param>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class LobbyCleanupService(
    LobbyService lobbyService,
    IOptions<ServerOptions> options,
    ILogger<LobbyCleanupService> logger)
    : PeriodicWorker(TimeSpan.FromSeconds(options.Value.LobbyHeartbeatIntervalSeconds), logger)
{
    private readonly ServerOptions options = options.Value;

    /// <inheritdoc />
    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var removed = await lobbyService.RemoveStaleGameLobbiesAsync(
            TimeSpan.FromSeconds(options.LobbyStaleSeconds),
            cancellationToken);

        if (removed > 0)
        {
            logger.LogInformation("Removed {Count} stale gameplay lobby(ies)", removed);
        }
    }
}
