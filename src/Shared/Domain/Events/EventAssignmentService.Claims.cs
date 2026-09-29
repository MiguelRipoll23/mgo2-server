namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The claim half of <see cref="EventAssignmentService"/>: taking a room for a
/// match, finding the one a match holds, and giving it back.
/// <para>
/// A room's claim is an <see cref="Persistence.Entities.EventHostLease"/> row,
/// because the lobby that pairs the teams and the gameplay server that hosts the
/// game are separate processes and the row is what settles a race between them.
/// There is one kind of room now — a games row — so there is one kind of claim,
/// and it is read back as an <see cref="EventAssignmentState"/> so the entry
/// screen, the teardown and the outcome all ask the same question the same way.
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
    private Task ReleaseAsync(int gameIdentifier, CancellationToken cancellationToken) =>
        leaseService.ReleaseAsync(gameIdentifier, cancellationToken);

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
        var lease = await leaseService.FindActiveByMatchAsync(matchIdentifier, cancellationToken);
        return lease is null ? null : EventAssignmentState.From(lease);
    }
}
