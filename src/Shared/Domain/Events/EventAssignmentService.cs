using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Assigns a host to a paired match. It joins the pairing, the room the pairing
/// holds and the two teams into the view the assignment packets are written
/// from, and it is the only place a match moves from waiting to assigned.
/// <para>
/// Nothing here invents a host. A match that cannot lease a room stays waiting,
/// because a snapshot carrying a room that does not exist is worse than a client
/// that keeps waiting.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="matchService">Service that owns the pairings.</param>
/// <param name="teamService">Service that owns the paired teams, real and simulated.</param>
/// <param name="rewardService">Service that pays a completed match.</param>
/// <param name="roomPool">The rooms a host is chosen from.</param>
/// <param name="gameService">Service that owns the rooms, which carry the assignment lock.</param>
/// <param name="rosterService">Service that owns the frozen rosters a pairing names.</param>
/// <param name="pushService">Service that tells the teams their match was found.</param>
public sealed partial class EventAssignmentService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    EventMatchService matchService,
    EventTeamService teamService,
    EventRewardService rewardService,
    EventHostRoomPoolService roomPool,
    GameService gameService,
    TournamentRosterService rosterService,
    EventAssignmentPushService pushService)
    : DomainService(contextFactory)
{
    /// <summary>
    /// Assigns a host to every waiting match in a lobby that has one spare.
    /// <para>
    /// A match waits for a room rather than for a person: the room is a player's
    /// dedicated host, it has to be idle, and it has to be dedicated to the mode
    /// the match is. A match that finds no such room keeps waiting, which is the
    /// correct outcome — inventing one would tell two teams they were playing a
    /// game nobody is hosting.
    /// </para>
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby whose waiting matches are assigned.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The assignments made.</returns>
    public async Task<List<EventAssignment>> TryAssignWaitingAsync(
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        var waiting = (await matchService.FindActiveByLobbyAsync(lobbyIdentifier, cancellationToken))
            .Where(match => match.State == EventConstants.MatchPairedState)
            .ToList();
        if (waiting.Count == 0)
        {
            return [];
        }

        // A lock can outlive the match it was taken for: the process that would
        // have released it stopped, or a cancellation gave the room back without
        // clearing the marker. The lock names no match, so what settles it is the
        // claims that are still live: a locked room no live match holds is
        // holding a lock for a match that is over. It is cleared before the rooms
        // are listed, so the choice below is made from the rooms that are free.
        var liveRooms = await matchService.FindActiveGameIdentifiersAsync(cancellationToken);
        foreach (var lockedRoom in await gameService.FindLockedIdentifiersAsync(cancellationToken))
        {
            if (!liveRooms.Contains(lockedRoom))
            {
                await gameService.UnlockAsync(lockedRoom, cancellationToken);
            }
        }

        var games = await roomPool.ListAsync(cancellationToken);
        var assignments = new List<EventAssignment>();

        foreach (var match in waiting)
        {
            // The teams are read through the team service rather than straight
            // from the table, so a match whose second team exists only in memory
            // still finds its roster and can take a host when one is free.
            var first = await teamService.FindAsync(match.FirstTeamIdentifier, cancellationToken);
            var second = await teamService.FindAsync(match.SecondTeamIdentifier, cancellationToken);
            if (first is null || second is null)
            {
                continue;
            }

            var participants = EventTeamService.BuildSnapshot(first).OccupiedParticipantCount()
                + EventTeamService.BuildSnapshot(second).OccupiedParticipantCount();

            var host = await roomPool.FindHostAsync(games, match.MatchType, participants, cancellationToken);
            if (host is null)
            {
                continue;
            }

            var assignment = await AssignAsync(match.Identifier, host.Identifier, cancellationToken);
            if (assignment is null)
            {
                // The room was claimed by another match between the choice and
                // the claim, which the unique index settles.
                continue;
            }

            await pushService.PushAssignmentAsync(
                assignment,
                host.HostIdentifier,
                cancellationToken);
            assignments.Add(assignment);
        }

        return assignments;
    }
    /// <summary>Takes a room for a waiting match.</summary>
    /// <param name="matchIdentifier">Match to assign.</param>
    /// <param name="gameIdentifier">Room to take.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The assignment, or null when the match or the room was taken.</returns>
    public async Task<EventAssignment?> AssignAsync(
        int matchIdentifier,
        int gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Identifier == matchIdentifier, cancellationToken);
        if (match is null || match.State != EventConstants.MatchPairedState)
        {
            return null;
        }

        var firstTeam = await teamService.FindAsync(match.FirstTeamIdentifier, cancellationToken);
        var secondTeam = await teamService.FindAsync(match.SecondTeamIdentifier, cancellationToken);
        if (firstTeam is null || secondTeam is null)
        {
            return null;
        }

        // The claim is taken before anything is read back, and it is what settles
        // the race: a room another match took between the choice and here, or a
        // pairing that stopped waiting in the meantime, leaves this match waiting
        // rather than pointing at a room it does not hold.
        if (!await TryClaimAsync(matchIdentifier, gameIdentifier, cancellationToken))
        {
            return null;
        }

        var assignment = await LoadAsync(matchIdentifier, cancellationToken);
        if (assignment is null)
        {
            // The room cannot be reported — a team went away between the claim
            // and the read — so it is given back rather than held for a match
            // whose assignment packets cannot be written.
            await ReleaseAsync(matchIdentifier, gameIdentifier, cancellationToken);
        }

        return assignment;
    }

    /// <summary>Finds the assignment one team is in.</summary>
    /// <param name="teamIdentifier">Team to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventAssignment?> FindByTeamAsync(
        int teamIdentifier,
        CancellationToken cancellationToken = default)
    {
        var match = await matchService.FindActiveByTeamAsync(teamIdentifier, cancellationToken);

        // A match that holds no room has no assignment to report, which is what
        // the load asks for itself.
        return match is null ? null : await LoadAsync(match.Identifier, cancellationToken);
    }

    /// <summary>Finds the assignment of a room.</summary>
    /// <param name="gameIdentifier">Room to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventAssignment?> FindByGameAsync(
        int gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        var match = await matchService.FindActiveByGameAsync(gameIdentifier, cancellationToken);
        return match is null ? null : await LoadAsync(match.Identifier, cancellationToken);
    }

    /// <summary>Records an outcome and releases the room.</summary>
    /// <param name="matchIdentifier">Match that finished.</param>
    /// <param name="winningTeamIdentifier">Team that won, or zero for a draw.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>What each team was paid, or empty when the match was already finished.</returns>
    public async Task<List<EventPayout>> CompleteAsync(
        int matchIdentifier,
        int winningTeamIdentifier,
        CancellationToken cancellationToken = default)
    {
        var claim = await FindClaimAsync(matchIdentifier, cancellationToken);

        // The claim is the gate: a match holding no claim on a room has already
        // been completed, so a second report pays nothing. The reward ledger is a
        // second guard under the same rule rather than the first one.
        if (claim is null)
        {
            return [];
        }

        var completed = await matchService.SetStateAsync(
            matchIdentifier,
            EventConstants.MatchCompletedState,
            winningTeamIdentifier == 0 ? null : winningTeamIdentifier,
            cancellationToken);
        if (!completed)
        {
            return [];
        }

        await ReleaseAsync(matchIdentifier, claim.GameIdentifier!.Value, cancellationToken);
        return await rewardService.PayAsync(matchIdentifier, winningTeamIdentifier, cancellationToken);
    }

    /// <summary>Cancels a live match and returns its room to the pool.</summary>
    /// <param name="matchIdentifier">Match being cancelled.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether the match was still live.</returns>
    public async Task<bool> CancelAsync(
        int matchIdentifier,
        CancellationToken cancellationToken = default)
    {
        var claim = await FindClaimAsync(matchIdentifier, cancellationToken);
        var assignment = claim is null
            ? null
            : await LoadAsync(matchIdentifier, cancellationToken);
        var cancelled = await matchService.SetStateAsync(
            matchIdentifier,
            EventConstants.MatchCancelledState,
            cancellationToken: cancellationToken);

        if (claim is not null)
        {
            await ReleaseAsync(claim.Identifier, claim.GameIdentifier!.Value, cancellationToken);

            // A Survival match that was live is a pair of teams holding a
            // match-found screen; the teardown empties the event record on both
            // so neither is left waiting. Tournament teardown belongs to the
            // bracket family rather than to this record, so only a Survival
            // assignment is a target at all.
            var teardownTarget = assignment is not null
                && assignment.LobbySubtype == EventConstants.SurvivalSelector
                    ? assignment
                    : null;

            if (cancelled && teardownTarget is not null)
            {
                await pushService.PushTeardownAsync(teardownTarget, cancellationToken);
            }
        }

        return cancelled;
    }

    /// <summary>
    /// Reads an assignment: the pairing, the room it holds and the two teams it
    /// pairs. A match that holds no room has no assignment to read, which is the
    /// same gate the claim itself is.
    /// </summary>
    /// <param name="matchIdentifier">Match to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<EventAssignment?> LoadAsync(int matchIdentifier, CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Identifier == matchIdentifier, cancellationToken);
        if (match is null
            || match.GameIdentifier is not { } gameIdentifier
            || match.AssignedAt is not { } assignedAt)
        {
            return null;
        }

        var firstTeam = await teamService.FindAsync(match.FirstTeamIdentifier, cancellationToken);
        var secondTeam = await teamService.FindAsync(match.SecondTeamIdentifier, cancellationToken);
        if (firstTeam is null || secondTeam is null)
        {
            return null;
        }

        return new EventAssignment
        {
            MatchIdentifier = match.Identifier,
            GameIdentifier = gameIdentifier,
            // The active state the client correlates with is the match itself, and
            // the sequence it echoes back is the first team's: both were stored
            // beside the claim, and both are read from where they really live.
            ActiveStateIdentifier = match.Identifier,
            Sequence = firstTeam.Sequence,
            ActivationTimeSeconds = (int)assignedAt.ToUnixTimeSeconds(),
            LobbyIdentifier = match.LobbyIdentifier,
            LobbySubtype = match.MatchType,
            FirstTeam = EventTeamService.BuildSnapshot(firstTeam),
            SecondTeam = EventTeamService.BuildSnapshot(secondTeam),
            FirstRoster = await rosterService.LoadAsync(EventOf(firstTeam), firstTeam.Identifier, cancellationToken),
            SecondRoster = await rosterService.LoadAsync(EventOf(secondTeam), secondTeam.Identifier, cancellationToken),
        };
    }

    /// <summary>
    /// Returns the event a team's roster was frozen against. A team formed on
    /// the event screens carries the event it was formed for, and a team that
    /// carries none has no frozen roster to read.
    /// </summary>
    /// <param name="team">Team to read.</param>
    private static int EventOf(Persistence.Entities.EventTeam team) => team.EventIdentifier;

}
