using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Owns the claim a match holds on a gameplay room. The claim is a row, not
/// process state, because the lobby that pairs the teams and the gameplay server
/// that hosts the game are separate processes: the room identifier is the
/// serialization point and the unique index is what makes two lobbies racing for
/// one room a refusal rather than a shared host.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class EventHostLeaseService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Claims a room for a match.</summary>
    /// <param name="matchIdentifier">Match the lease belongs to.</param>
    /// <param name="gameIdentifier">Room being claimed.</param>
    /// <param name="activeStateIdentifier">Active state the client will correlate with.</param>
    /// <param name="sequence">Initial sequence of the active state.</param>
    /// <param name="lobbyIdentifier">Lobby the room belongs to.</param>
    /// <param name="lobbySubtype">Game type of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The lease, or null when the room or the match is already claimed.</returns>
    public async Task<EventHostLease?> TryCreateAsync(
        int matchIdentifier,
        int gameIdentifier,
        int activeStateIdentifier,
        int sequence,
        int lobbyIdentifier,
        int lobbySubtype,
        CancellationToken cancellationToken = default)
    {
        if (matchIdentifier == 0 || gameIdentifier == 0)
        {
            throw new ArgumentException("A lease requires both a match and a room.");
        }

        var now = DateTimeOffset.UtcNow;
        var lease = new EventHostLease
        {
            MatchIdentifier = matchIdentifier,
            GameIdentifier = gameIdentifier,
            ActiveStateIdentifier = activeStateIdentifier,
            ActiveStateSequence = sequence,
            LobbyIdentifier = lobbyIdentifier,
            LobbySubtype = lobbySubtype,
            Status = EventConstants.LeaseActiveState,
            LeasedAt = now,
            UpdatedAt = now,
        };

        await using var context = await CreateContextAsync(cancellationToken);
        context.EventHostLeases.Add(lease);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique index refused it, which is the race being resolved
            // rather than a failure to report. The winner's lease stands.
            context.Entry(lease).State = EntityState.Detached;
            return null;
        }

        return lease;
    }

    /// <summary>Finds the active lease of a room.</summary>
    /// <param name="gameIdentifier">Room to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventHostLease?> FindActiveByGameAsync(
        int gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventHostLeases
            .FirstOrDefaultAsync(
                lease => lease.GameIdentifier == gameIdentifier
                    && lease.Status == EventConstants.LeaseActiveState,
                cancellationToken);
    }

    /// <summary>Finds the active lease of a match.</summary>
    /// <param name="matchIdentifier">Match to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventHostLease?> FindActiveByMatchAsync(
        int matchIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventHostLeases
            .FirstOrDefaultAsync(
                lease => lease.MatchIdentifier == matchIdentifier
                    && lease.Status == EventConstants.LeaseActiveState,
                cancellationToken);
    }

    /// <summary>Returns a room to the pool by releasing its lease.</summary>
    /// <param name="gameIdentifier">Room being released.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether an active lease was released.</returns>
    public async Task<bool> ReleaseAsync(
        int gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var lease = await context.EventHostLeases
            .FirstOrDefaultAsync(
                candidate => candidate.GameIdentifier == gameIdentifier
                    && candidate.Status == EventConstants.LeaseActiveState,
                cancellationToken);

        if (lease is null)
        {
            return false;
        }

        lease.Status = EventConstants.LeaseReleasedState;
        lease.ReleasedAt = DateTimeOffset.UtcNow;
        lease.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Advances the active-state sequence a lease reports.</summary>
    /// <param name="leaseIdentifier">Lease to change.</param>
    /// <param name="sequence">Sequence to record.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether the lease existed.</returns>
    public async Task<bool> SetSequenceAsync(
        int leaseIdentifier,
        int sequence,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var lease = await context.EventHostLeases
            .FirstOrDefaultAsync(candidate => candidate.Identifier == leaseIdentifier, cancellationToken);

        if (lease is null)
        {
            return false;
        }

        lease.ActiveStateSequence = sequence;
        lease.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
