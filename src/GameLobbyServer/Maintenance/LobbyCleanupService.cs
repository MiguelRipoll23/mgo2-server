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
/// and the gameplay servers own matches, not lobbies.
/// <para>
/// Once a day at midnight UTC, not on the beat. The rows it removes are already
/// invisible: every list read — the gate's, the HTTP API's — drops a gameplay
/// lobby whose <c>updated_at</c> is older than the stale window, so a client never
/// waits on this delete to stop seeing a lobby that is gone. What the delete
/// buys is the space, and reaping it once a day against nine lobby processes'
/// worth of the same statement is what the beat being hourly already made
/// sensible.
/// </para>
/// </summary>
/// <param name="lobbyService">Service that owns the lobby rows.</param>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class LobbyCleanupService(
    LobbyService lobbyService,
    IOptions<ServerOptions> options,
    ILogger<LobbyCleanupService> logger)
    : DailyWorker(DailyWorker.MidnightUtc, logger)
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
