namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The claim half of <see cref="EventAssignmentService"/>: taking a room for a
/// match, finding the one a match holds, and giving it back.
/// <para>
/// It is separated because the two kinds of room are claimed differently and
/// nothing else here should have to know. A real room is claimed by a lease row,
/// because the lobby that pairs the teams and the gameplay server that hosts the
/// game are separate processes and the row is what settles a race between them.
/// An in-memory room is claimed in process, because a foreign key names a row and
/// that room is not one — and because the process holding it is the only one
/// that could race for it.
/// </para>
/// <para>
/// Both are read back as an <see cref="EventAssignmentState"/>, so the entry
/// screen, the teardown and the outcome answer the same question the same way
/// whichever kind of room the match landed in.
/// </para>
/// </summary>
public sealed partial class EventAssignmentService
{
    /// <summary>
    /// Claims a room for a match, whichever kind of room it is.
    /// </summary>
    /// <param name="matchIdentifier">Match the claim belongs to.</param>
    /// <param name="gameIdentifier">Room being claimed, real or in-memory.</param>
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
        if (FakePlayerIdentifierUtils.IsFake(gameIdentifier))
        {
            var claim = fakeClaims.TryClaimAsync(
                matchIdentifier,
                gameIdentifier,
                activeStateIdentifier,
                sequence);

            return claim is null ? null : EventAssignmentState.From(claim);
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

    /// <summary>
    /// Releases a room's claim, whichever kind of room it is.
    /// </summary>
    /// <param name="gameIdentifier">Room being released.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private Task ReleaseAsync(int gameIdentifier, CancellationToken cancellationToken) =>
        FakePlayerIdentifierUtils.IsFake(gameIdentifier)
            ? Task.FromResult(fakeClaims.Release(gameIdentifier))
            : leaseService.ReleaseAsync(gameIdentifier, cancellationToken);

    /// <summary>
    /// The claim a match holds, of either kind, or null when it holds none.
    /// <para>
    /// A match is claimed against exactly one kind of room, so the two are asked
    /// in turn rather than the caller choosing. That is what keeps every reader
    /// — the entry screen, the teardown, the outcome — answering the same
    /// question the same way whichever room the match landed in.
    /// </para>
    /// </summary>
    /// <param name="matchIdentifier">Match to read the claim of.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<EventAssignmentState?> FindClaimAsync(
        int matchIdentifier,
        CancellationToken cancellationToken)
    {
        var inMemory = fakeClaims.FindByMatch(matchIdentifier);
        if (inMemory is not null)
        {
            return EventAssignmentState.From(inMemory);
        }

        var lease = await leaseService.FindActiveByMatchAsync(
            matchIdentifier, cancellationToken);
        return lease is null ? null : EventAssignmentState.From(lease);
    }
}
