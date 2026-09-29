using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The claim half of <see cref="EventAssignmentService"/>: taking a room for a
/// match, finding the one a match holds, and giving it back.
/// <para>
/// A real room's claim is an <see cref="Persistence.Entities.EventHostLease"/>
/// row, because the lobby that pairs the teams and the gameplay server that hosts
/// the game are separate processes and the row is what settles a race between
/// them. A room this process opened for a test has no row a lease could name —
/// the lease's room column is a foreign key onto the games table — so its claim
/// is kept where the room is, refusing the same two things the lease's unique
/// indexes refuse: one room plays one match, and one match plays in one room.
/// </para>
/// <para>
/// Both kinds are read back as an <see cref="EventAssignmentState"/>, so the
/// entry screen, the teardown and the outcome all ask the same question the same
/// way and none of them has to know which kind it is holding.
/// </para>
/// </summary>
public sealed partial class EventAssignmentService
{
    /// <summary>Claims a room for a match.</summary>
    /// <param name="matchIdentifier">Match the claim belongs to.</param>
    /// <param name="gameIdentifier">Room being claimed.</param>
    /// <param name="activeStateIdentifier">Active state the client will correlate with.</param>
    /// <param name="sequence">Initial sequence of the active state.</param>
    /// <param name="lobbyIdentifier">Lobby the room belongs to.</param>
    /// <param name="lobbySubtype">Game type of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The claim, or null when the match or the room was already claimed.</returns>
    private async Task<EventAssignmentState?> TryClaimAsync(
        int matchIdentifier,
        int gameIdentifier,
        int activeStateIdentifier,
        int sequence,
        int lobbyIdentifier,
        int lobbySubtype,
        CancellationToken cancellationToken)
    {
        if (TestIdentifierUtils.IsTest(gameIdentifier))
        {
            return memoryRooms.TryClaim(
                matchIdentifier,
                gameIdentifier,
                activeStateIdentifier,
                sequence);
        }

        var lease = await leaseService.TryCreateAsync(
            matchIdentifier,
            gameIdentifier,
            activeStateIdentifier,
            sequence,
            lobbyIdentifier,
            lobbySubtype,
            cancellationToken);

        return lease is null ? null : EventAssignmentState.From(lease);
    }

    /// <summary>Releases a room's claim.</summary>
    /// <param name="gameIdentifier">Room being released.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task ReleaseAsync(int gameIdentifier, CancellationToken cancellationToken)
    {
        if (TestIdentifierUtils.IsTest(gameIdentifier))
        {
            memoryRooms.Release(gameIdentifier);
            return;
        }

        await leaseService.ReleaseAsync(gameIdentifier, cancellationToken);
    }

    /// <summary>
    /// The claim a match holds, or null when it holds none. A match holding no
    /// claim on a room has already been completed or cancelled, which is what
    /// makes this the gate both of those read.
    /// </summary>
    /// <param name="matchIdentifier">Match to read the claim of.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<EventAssignmentState?> FindClaimAsync(
        int matchIdentifier,
        CancellationToken cancellationToken)
    {
        // A match is always a row, so its identifier cannot say which store holds
        // the claim on it. The in-memory half is asked first because it only ever
        // holds this process's own; a real match misses it and reads its lease.
        if (memoryRooms.FindClaimByMatch(matchIdentifier) is { } memoryClaim)
        {
            return memoryClaim;
        }

        var lease = await leaseService.FindActiveByMatchAsync(matchIdentifier, cancellationToken);
        return lease is null ? null : EventAssignmentState.From(lease);
    }

    /// <summary>
    /// The claim a room holds with the match it was taken for, or null when it
    /// holds none. The match comes back with the claim because a room identifier
    /// is all a caller has, and a claim alone does not say what to read.
    /// </summary>
    /// <param name="gameIdentifier">Room to read the claim of.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<(int MatchIdentifier, EventAssignmentState Claim)?> FindClaimByRoomAsync(
        int gameIdentifier,
        CancellationToken cancellationToken)
    {
        if (memoryRooms.FindClaimByRoom(gameIdentifier) is { } memoryClaim)
        {
            return memoryClaim;
        }

        var lease = await leaseService.FindActiveByGameAsync(gameIdentifier, cancellationToken);
        return lease is null ? null : (lease.MatchIdentifier, EventAssignmentState.From(lease));
    }
}
