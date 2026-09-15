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

        // The lobbies of the games that are about to go are read first, so the
        // new total of each one is published after the delete.
        var lobbyIdentifiers = await context.Games
            .Where(game => game.UpdatedAt < cutoff)
            .Select(game => game.LobbyIdentifier)
            .Distinct()
            .ToListAsync(cancellationToken);

        // The rosters go with the rooms, so the presence they hold is credited
        // first. A reaped room is a crashed session rather than one that ended, and
        // its interval is the only record left of it.
        var gameIdentifiers = await context.Games
            .Where(game => game.UpdatedAt < cutoff)
            .Select(game => game.Identifier)
            .ToListAsync(cancellationToken);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await CreditTrainingTimeAsync(context, gameIdentifiers, 0, cancellationToken);

        var removed = await context.Games
            .Where(game => game.UpdatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        foreach (var lobbyIdentifier in lobbyIdentifiers)
        {
            await ReportLobbyMatchesAsync(lobbyIdentifier, cancellationToken);
        }

        return removed;
    }
}
