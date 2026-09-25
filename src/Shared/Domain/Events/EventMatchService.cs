using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Owns the pairings of two event teams. A pairing is durable so the battle
/// list, the assignment and the reward all agree on it across a restart and
/// across processes; the in-memory queue that produced it is only an ordering
/// detail.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class EventMatchService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Creates a pairing in the waiting-for-host state.</summary>
    /// <param name="lobbyIdentifier">Lobby both teams belong to.</param>
    /// <param name="matchType">Match type shared by both teams.</param>
    /// <param name="firstTeamIdentifier">First team.</param>
    /// <param name="secondTeamIdentifier">Second team.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventMatch> CreateAsync(
        int lobbyIdentifier,
        int matchType,
        int firstTeamIdentifier,
        int secondTeamIdentifier,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var match = new EventMatch
        {
            LobbyIdentifier = lobbyIdentifier,
            MatchType = matchType,
            FirstTeamIdentifier = firstTeamIdentifier,
            SecondTeamIdentifier = secondTeamIdentifier,
            State = EventConstants.MatchPairedState,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await using var context = await CreateContextAsync(cancellationToken);
        context.EventMatches.Add(match);
        await context.SaveChangesAsync(cancellationToken);
        return match;
    }

    /// <summary>Lists the matches of a lobby that are still live.</summary>
    /// <param name="lobbyIdentifier">Lobby to list.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<EventMatch>> FindActiveByLobbyAsync(
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventMatches
            .Where(match => match.LobbyIdentifier == lobbyIdentifier
                && (match.State == EventConstants.MatchPairedState
                    || match.State == EventConstants.MatchAssignedState))
            .OrderBy(match => match.Identifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Finds the live match one team is in.</summary>
    /// <param name="teamIdentifier">Team to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventMatch?> FindActiveByTeamAsync(
        int teamIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventMatches
            .Where(match => (match.State == EventConstants.MatchPairedState
                    || match.State == EventConstants.MatchAssignedState)
                && (match.FirstTeamIdentifier == teamIdentifier || match.SecondTeamIdentifier == teamIdentifier))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Moves a match to a new state.</summary>
    /// <param name="matchIdentifier">Match to change.</param>
    /// <param name="state">State to move to.</param>
    /// <param name="winnerTeamIdentifier">Winning team, when the state records one.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether the match existed.</returns>
    public async Task<bool> SetStateAsync(
        int matchIdentifier,
        int state,
        int? winnerTeamIdentifier = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
            .FirstOrDefaultAsync(candidate => candidate.Identifier == matchIdentifier, cancellationToken);

        if (match is null)
        {
            return false;
        }

        match.State = state;
        match.WinnerTeamIdentifier = winnerTeamIdentifier;
        match.UpdatedAt = DateTimeOffset.UtcNow;
        if (state is EventConstants.MatchCompletedState or EventConstants.MatchCancelledState)
        {
            match.CompletedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
