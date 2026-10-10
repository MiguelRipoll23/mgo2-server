using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The claim half of <see cref="EventMatchService"/>: the room a match holds, how
/// it is taken, and how it is given back.
/// <para>
/// A claim is a column of the pairing rather than a row of its own. It used to be
/// an <c>event_host_leases</c> row, because the lobby that pairs the teams and the
/// gameplay server that hosts the game are separate processes and something had
/// to settle a race between them; but everything that row held was either the
/// pairing's own column (the lobby, the mode, the active state, which is the match
/// identifier) or the one fact the pairing lacked: the room. So the room is stored
/// here, and the race is settled by this row's unique index — a room hosts one live
/// match, and the database is what refuses the second one.
/// </para>
/// <para>
/// The room's own lock is written beside this, by the game service's
/// <c>TryLockAsync</c>, and is what every reader outside this process sees. The
/// two move together: a claim without the lock is a room the pool still offers,
/// and a lock without a live claim is what the sweep clears.
/// </para>
/// </summary>
public sealed partial class EventMatchService
{
    /// <summary>
    /// Claims a room for a match: the room is named, the moment it was taken is
    /// recorded and the match moves to assigned.
    /// </summary>
    /// <param name="matchIdentifier">Match taking the room.</param>
    /// <param name="gameIdentifier">Room being taken.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>
    /// Whether the room was taken. False when the match is gone, is no longer
    /// waiting for a host, was changed by somebody else between the read and the
    /// write, or when another live match already holds that room.
    /// </returns>
    public async Task<bool> TryClaimAsync(
        int matchIdentifier,
        int gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
            .FirstOrDefaultAsync(candidate => candidate.Identifier == matchIdentifier, cancellationToken);

        if (match is null || match.State != EventConstants.MatchPairedState)
        {
            return false;
        }

        match.GameIdentifier = gameIdentifier;
        match.AssignedAt = now;
        match.State = EventConstants.MatchAssignedState;
        match.UpdatedAt = now;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique index refused it, so a live match already holds that
            // room, or the row moved under this one. Either way the claim was not
            // made, which is a race being settled rather than a failure to
            // report. The entity is detached so nothing carries the claim that
            // was refused.
            context.Entry(match).State = EntityState.Detached;
            return false;
        }

        return true;
    }

    /// <summary>
    /// The claim a match holds, or null when it holds none. A match holds a room
    /// while it is assigned to one; a match that has been completed or cancelled
    /// holds nothing, which is what makes this the gate both of those read.
    /// </summary>
    /// <param name="matchIdentifier">Match to read the claim of.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventMatch?> FindClaimAsync(
        int matchIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventMatches
            .AsNoTracking()
            .FirstOrDefaultAsync(
                match => match.Identifier == matchIdentifier
                    && match.State == EventConstants.MatchAssignedState
                    && match.GameIdentifier != null,
                cancellationToken);
    }

    /// <summary>Finds the live match a room is hosting, when it is hosting one.</summary>
    /// <param name="gameIdentifier">Room to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventMatch?> FindActiveByGameAsync(
        int gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventMatches
            .AsNoTracking()
            .FirstOrDefaultAsync(
                match => match.GameIdentifier == gameIdentifier
                    && (match.State == EventConstants.MatchPairedState
                        || match.State == EventConstants.MatchAssignedState),
                cancellationToken);
    }

    /// <summary>
    /// The rooms the live matches hold, as the set a sweep reads a locked room
    /// against. A room held here is one whose lock somebody will release; a
    /// locked room absent from this set has nobody left to release it.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Identifiers of the rooms live matches hold.</returns>
    public async Task<IReadOnlySet<int>> FindActiveGameIdentifiersAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var identifiers = await context.EventMatches
            .Where(match => match.GameIdentifier != null
                && (match.State == EventConstants.MatchPairedState
                    || match.State == EventConstants.MatchAssignedState))
            .Select(match => match.GameIdentifier!.Value)
            .ToListAsync(cancellationToken);

        return identifiers.ToHashSet();
    }

    /// <summary>
    /// Gives the room back: the match stops naming it, so the pairing keeps what
    /// it knows about the game it played and stops claiming a host for it.
    /// </summary>
    /// <param name="matchIdentifier">Match giving the room back.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether the match named a room and it was given back.</returns>
    public async Task<bool> ReleaseAsync(int matchIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
            .FirstOrDefaultAsync(candidate => candidate.Identifier == matchIdentifier, cancellationToken);

        if (match is null || match.GameIdentifier is null)
        {
            return false;
        }

        match.GameIdentifier = null;
        match.AssignedAt = null;
        match.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.Entry(match).State = EntityState.Detached;
            return false;
        }

        return true;
    }
}
