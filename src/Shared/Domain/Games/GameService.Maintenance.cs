using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// The lifecycle half of the game service: the heartbeat a dedicated host
/// writes for its match, and the expiry of the matches whose host stopped.
/// </summary>
public sealed partial class GameService
{
    /// <summary>Refreshes the heartbeat of a room, which keeps it published.</summary>
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
    /// Deletes the rooms hosted by a dedicated host whose heartbeat stopped.
    /// Only the rooms of that host are considered: a room created by a player
    /// belongs to its session and is never expired by this cleanup, so an idle
    /// room that is still occupied keeps its current lifetime.
    /// </summary>
    /// <param name="hostCharacterIdentifier">Character the dedicated host plays as.</param>
    /// <param name="staleAfter">Age at which a room is considered abandoned.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>How many rooms were removed.</returns>
    public async Task<int> RemoveStaleHostedMatchesAsync(
        int hostCharacterIdentifier,
        TimeSpan staleAfter,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - staleAfter;

        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Games
            .Where(game => game.HostIdentifier == hostCharacterIdentifier && game.UpdatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
