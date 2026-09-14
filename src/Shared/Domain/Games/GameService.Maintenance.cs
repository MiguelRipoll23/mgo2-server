using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// The lifecycle half of the game service: the heartbeat a gameplay server
/// writes for its game, and the expiry of the games whose host stopped.
/// </summary>
public sealed partial class GameService
{
    /// <summary>Refreshes the heartbeat of a game, which keeps it published.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HeartbeatAsync(int gameIdentifier, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var context = await CreateContextAsync(cancellationToken);
        await context.Games
            .Where(game => game.Identifier == gameIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(game => game.UpdatedAt, now),
                cancellationToken);
    }

    /// <summary>
    /// Deletes games whose host stopped sending pings. The host refreshes
    /// updated_at through UpdatePingsAsync (player-hosted) or HeartbeatAsync
    /// (gameplay-server), so a game that is still active is never removed. A
    /// game whose host disconnected without quitting lingers until the stale
    /// threshold is reached.
    /// </summary>
    /// <param name="staleAfter">Age at which a game is considered abandoned.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>How many games were removed.</returns>
    public async Task<int> RemoveStaleGamesAsync(
        TimeSpan staleAfter,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - staleAfter;

        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Games
            .Where(game => game.UpdatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
