using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Assigns a host to a paired match. It joins three rows — the pairing, the room
/// it is leased and the two teams — into the view the assignment packets are
/// written from, and it is the only place a match moves from waiting to
/// assigned.
/// <para>
/// Nothing here invents a host. A match that cannot lease a room stays waiting,
/// because a snapshot carrying a room that does not exist is worse than a client
/// that keeps waiting.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="matchService">Service that owns the pairings.</param>
/// <param name="teamService">Service that owns the paired teams, real and simulated.</param>
/// <param name="leaseService">Service that owns the room claims.</param>
/// <param name="rewardService">Service that pays a completed match.</param>
/// <param name="roomPool">The rooms a host is chosen from.</param>
/// <param name="rosterService">Service that owns the frozen rosters a pairing names.</param>
/// <param name="pushService">Service that tells the teams their match was found.</param>
public sealed partial class EventAssignmentService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    EventMatchService matchService,
    EventTeamService teamService,
    EventHostLeaseService leaseService,
    EventRewardService rewardService,
    EventHostRoomPoolService roomPool,
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

        // A room that looks free may still be holding a lease whose match is
        // over: a cancellation that did not give the room back leaves the claim
        // active, and the choice below would be made from a room that refuses
        // the claim. The orphan is settled before the rooms are listed, so the
        // choice is made from the rooms that are actually free.
        await leaseService.ReleaseOrphansAsync(cancellationToken);

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
    /// <summary>Leases a room for a waiting match.</summary>
    /// <param name="matchIdentifier">Match to assign.</param>
    /// <param name="gameIdentifier">Room to lease.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The assignment, or null when the match or the room was taken.</returns>
    public async Task<EventAssignment?> AssignAsync(
        int matchIdentifier,
        int gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
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

        // The claim is taken before the match moves, and it is what settles the
        // race: a room another match took between the choice and here leaves this
        // match waiting rather than pointing at a room it does not hold.
        var state = await TryClaimAsync(
            matchIdentifier,
            gameIdentifier,
            matchIdentifier,
            firstTeam.Sequence,
            match.LobbyIdentifier,
            match.MatchType,
            cancellationToken);

        if (state is null)
        {
            return null;
        }

        if (!await matchService.SetStateAsync(
                matchIdentifier,
                EventConstants.MatchAssignedState,
                cancellationToken: cancellationToken))
        {
            // The pairing vanished between the two writes, so the claim it holds
            // is released rather than left pointing at a match that is gone.
            await ReleaseAsync(gameIdentifier, cancellationToken);
            return null;
        }

        return new EventAssignment
        {
            MatchIdentifier = matchIdentifier,
            GameIdentifier = gameIdentifier,
            ActiveStateIdentifier = state.Value.ActiveStateIdentifier,
            Sequence = state.Value.ActiveStateSequence,
            ActivationTimeSeconds = state.Value.BaseTimeSeconds(),
            LobbyIdentifier = match.LobbyIdentifier,
            LobbySubtype = match.MatchType,
            FirstTeam = EventTeamService.BuildSnapshot(firstTeam),
            SecondTeam = EventTeamService.BuildSnapshot(secondTeam),
            FirstRoster = await rosterService.LoadAsync(EventOf(firstTeam), firstTeam.Identifier, cancellationToken),
            SecondRoster = await rosterService.LoadAsync(EventOf(secondTeam), secondTeam.Identifier, cancellationToken),
        };
    }

    /// <summary>Finds the assignment one team is in.</summary>
    /// <param name="teamIdentifier">Team to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventAssignment?> FindByTeamAsync(
        int teamIdentifier,
        CancellationToken cancellationToken = default)
    {
        var match = await matchService.FindActiveByTeamAsync(teamIdentifier, cancellationToken);
        if (match is null)
        {
            return null;
        }

        var claim = await FindClaimAsync(match.Identifier, cancellationToken);
        if (claim is null)
        {
            return null;
        }

        return await LoadAsync(match.Identifier, claim.Value, cancellationToken);
    }

    /// <summary>Finds the assignment of a room.</summary>
    /// <param name="gameIdentifier">Room to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventAssignment?> FindByGameAsync(
        int gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        var lease = await leaseService.FindActiveByGameAsync(gameIdentifier, cancellationToken);
        if (lease is null)
        {
            return null;
        }

        return await LoadAsync(
            lease.MatchIdentifier,
            EventAssignmentState.From(lease),
            cancellationToken);
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

        await ReleaseAsync(claim.Value.GameIdentifier, cancellationToken);
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
            : await LoadAsync(matchIdentifier, claim.Value, cancellationToken);
        var cancelled = await matchService.SetStateAsync(
            matchIdentifier,
            EventConstants.MatchCancelledState,
            cancellationToken: cancellationToken);

        if (claim is not null)
        {
            await ReleaseAsync(claim.Value.GameIdentifier, cancellationToken);

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

    private async Task<EventAssignment?> LoadAsync(
        int matchIdentifier,
        EventAssignmentState claim,
        CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
            .FirstOrDefaultAsync(candidate => candidate.Identifier == matchIdentifier, cancellationToken);
        if (match is null)
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
            GameIdentifier = claim.GameIdentifier,
            ActiveStateIdentifier = claim.ActiveStateIdentifier,
            Sequence = claim.ActiveStateSequence,
            ActivationTimeSeconds = claim.BaseTimeSeconds(),
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
