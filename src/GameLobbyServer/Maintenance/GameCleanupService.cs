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
/// <para>
/// Once a day at midnight UTC. A stale room is already gone as far as a client is
/// concerned — the room browser and the HTTP game list both drop a game whose
/// <c>updated_at</c> is older than the stale window — so this pass is the one that
/// reclaims the rows, the rosters and the training time they were holding, and it
/// does it in one batch rather than every beat.
/// </para>
/// </summary>
/// <param name="gameService">Service that owns the game rows.</param>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class GameCleanupService(
    GameService gameService,
    IOptions<ServerOptions> options,
    ILogger<GameCleanupService> logger)
    : DailyWorker(DailyWorker.MidnightUtc, logger)
{
    private readonly ServerOptions options = options.Value;

    /// <inheritdoc />
    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var removed = await gameService.RemoveStaleGamesAsync(
            TimeSpan.FromSeconds(options.GameStaleSeconds),
            cancellationToken);

        if (removed > 0)
        {
            logger.LogInformation("Removed {Count} stale game(s)", removed);
        }
    }
}
