using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Removes games whose host stopped sending pings. The host refreshes
/// updated_at through UpdatePingsAsync (player-hosted) or HeartbeatAsync
/// (gameplay-server), so an active game is never removed. A game whose host
/// disconnected without quitting lingers until the stale threshold is reached.
/// </summary>
/// <param name="gameService">Service that owns the game rows.</param>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class GameCleanupService(
    GameService gameService,
    IOptions<ServerOptions> options,
    ILogger<GameCleanupService> logger)
    : PeriodicWorker(TimeSpan.FromSeconds(options.Value.LobbyHeartbeatIntervalSeconds), logger)
{
    private readonly ServerOptions options = options.Value;

    /// <inheritdoc />
    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var removed = await gameService.RemoveStaleGamesAsync(
            TimeSpan.FromSeconds(options.LobbyStaleSeconds),
            cancellationToken);

        if (removed > 0)
        {
            logger.LogInformation("Removed {Count} stale game(s)", removed);
        }
    }
}
