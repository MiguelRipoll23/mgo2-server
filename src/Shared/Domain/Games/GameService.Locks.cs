using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// The half of the game service that owns a room's occupancy lock: the marker an
/// event assignment writes into the room's own password field while the room is
/// taken, and the readers that ask which rooms carry it.
/// <para>
/// It is a lock rather than a second table because the room already has a field
/// every reader looks at: the client's room list draws a room with a password as
/// locked, and the host choice refuses one. The assignment writes this value
/// instead of a password of its own, so a room that is taken is taken visibly,
/// without anything having to read the event subsystem to find out.
/// </para>
/// <para>
/// Every write here is conditional on the value being what it expects, so the
/// lock is a comparison and a swap in one statement: two claims racing for one
/// room cannot both succeed, and only the value this server wrote is ever
/// cleared, so a host's own password survives.
/// </para>
/// </summary>
public sealed partial class GameService
{
    /// <summary>
    /// Value the assignment writes into a taken room's password. It is not a
    /// password a player is asked for: an event host is entered by the players
    /// the match named, and the join gate does not challenge them for it.
    /// </summary>
    public const string AssignmentLock = "assigned";

    /// <summary>
    /// Takes a room's lock, or reports that it is already held.
    /// <para>
    /// The write is conditional on the password being empty, which is what makes
    /// the claim a race that is settled rather than shared: a room another claim
    /// took between the choice and here leaves this one unlocked and this call
    /// reports it, and the caller picks a room again rather than both matches
    /// believing they hold the same host.
    /// </para>
    /// </summary>
    /// <param name="gameIdentifier">Room to lock.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether the room was free and is now locked.</returns>
    public async Task<bool> TryLockAsync(int gameIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var locked = await context.Games
            .Where(game => game.Identifier == gameIdentifier && game.Password == string.Empty)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(game => game.Password, AssignmentLock),
                cancellationToken);

        return locked > 0;
    }

    /// <summary>
    /// Clears a room's lock, or reports that it carried none.
    /// <para>
    /// Only the assignment's own value is cleared. A host that saved a password
    /// of their own keeps it, and a room that never carried the lock is left
    /// exactly as it was; a release that cleared the field unconditionally would
    /// open a room somebody had deliberately closed.
    /// </para>
    /// </summary>
    /// <param name="gameIdentifier">Room to unlock.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether the room carried the assignment's lock.</returns>
    public async Task<bool> UnlockAsync(int gameIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var unlocked = await context.Games
            .Where(game => game.Identifier == gameIdentifier && game.Password == AssignmentLock)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(game => game.Password, string.Empty),
                cancellationToken);

        return unlocked > 0;
    }

    /// <summary>
    /// The rooms that carry the assignment's lock, as the set a sweep checks
    /// against the claims that are still live.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Identifiers of the locked rooms.</returns>
    public async Task<IReadOnlySet<int>> FindLockedIdentifiersAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var identifiers = await context.Games
            .Where(game => game.Password == AssignmentLock)
            .Select(game => game.Identifier)
            .ToListAsync(cancellationToken);

        return identifiers.ToHashSet();
    }
}
