using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The claim half of <see cref="EventAssignmentService"/>: taking a room for a
/// match, and giving it back.
/// <para>
/// The claim is written twice, and the two written together are what it is. The
/// room's own password field carries the lock, because that is the marker every
/// other reader looks at — the room list draws it and the host choice refuses it —
/// and the pairing carries the room itself, because that is the link a result
/// report and a join both have to follow back to the match. Either one alone is
/// half a claim: a lock nobody can attribute to a match, or a match pointing at a
/// room the pool still offers.
/// </para>
/// <para>
/// Both writes are conditional, so the race is settled rather than shared. The
/// lock is taken only if the field is empty and the pairing names the room only
/// while it is still waiting, so two matches that chose the same room, or the same
/// match chosen twice, end with one claimant and one refusal.
/// </para>
/// </summary>
public sealed partial class EventAssignmentService
{
    /// <summary>
    /// Takes a room for a match: the lock on the room, then the room on the match.
    /// </summary>
    /// <param name="matchIdentifier">Match taking the room.</param>
    /// <param name="gameIdentifier">Room being taken.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether the match now holds the room.</returns>
    private async Task<bool> TryClaimAsync(
        int matchIdentifier,
        int gameIdentifier,
        CancellationToken cancellationToken)
    {
        // The lock first, because it is the write that keeps the room out of the
        // pool and the one a reader outside this process sees. A room another
        // claim took between the choice and here is refused here rather than
        // shared, and the caller waits for the next pass.
        if (!await gameService.TryLockAsync(gameIdentifier, cancellationToken))
        {
            return false;
        }

        if (await matchService.TryClaimAsync(matchIdentifier, gameIdentifier, cancellationToken))
        {
            return true;
        }

        // The match was gone, was no longer waiting, or lost the room to another
        // match. The lock is this claim's own, so it is given back with it, or
        // the room would sit locked for a match that never took it.
        await gameService.UnlockAsync(gameIdentifier, cancellationToken);
        return false;
    }

    /// <summary>
    /// Gives a room back: the match stops naming it and the room stops carrying
    /// the lock. The two together are what returns the room to the pool.
    /// </summary>
    /// <param name="matchIdentifier">Match giving the room back.</param>
    /// <param name="gameIdentifier">Room being given back.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task ReleaseAsync(
        int matchIdentifier,
        int gameIdentifier,
        CancellationToken cancellationToken)
    {
        await matchService.ReleaseAsync(matchIdentifier, cancellationToken);
        await gameService.UnlockAsync(gameIdentifier, cancellationToken);
    }

    /// <summary>
    /// The claim a match holds, or null when it holds none. A match holding no
    /// room has already been completed or cancelled, which is what makes this the
    /// gate both of those read.
    /// </summary>
    /// <param name="matchIdentifier">Match to read the claim of.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private Task<EventMatch?> FindClaimAsync(int matchIdentifier, CancellationToken cancellationToken) =>
        matchService.FindClaimAsync(matchIdentifier, cancellationToken);
}
