using Microsoft.Extensions.Logging;
using Mgo2Server.GameLobbyServer.Commands.Game.Chat;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>
/// Disbands a team the caller leads, or removes the caller from one.
/// <para>
/// The command has a second meaning once the team has been assigned a room. The
/// client sends the same empty record one more time as the last step of its own
/// move into the assigned game — after the match-found notification has taken its
/// team screen through the room's details — and what it is owed then is the ready
/// acknowledgement, not a team it has just left. Leaving on that record would
/// cancel the matched pair and hand its room back in the interval before the client
/// joins it, so a team that holds an assignment is answered and kept.
/// </para>
/// </summary>
/// <param name="teamService">Service that owns the teams.</param>
/// <param name="memberService">Service that owns the rosters.</param>
/// <param name="pushService">Service that pushes roster changes.</param>
/// <param name="invitationService">Service that owns the team invitations.</param>
/// <param name="matchmakingService">Service that pairs the teams.</param>
/// <param name="assignmentService">Service that knows which room a team holds.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class LeaveEventTeamHandler(
    EventTeamService teamService,
    EventTeamMemberService memberService,
    EventTeamPushService pushService,
    EventInvitationService invitationService,
    EventMatchmakingService matchmakingService,
    EventAssignmentService assignmentService,
    SessionHelper sessionHelper,
    ILogger<LeaveEventTeamHandler> logger) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // A leave comes from a character.
        if (session.CharacterIdentifier is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var characterIdentifier = session.CharacterIdentifier.Value;

        // It leaves the team the connection is attached to.
        if (session.EventTeamIdentifier is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var teamIdentifier = session.EventTeamIdentifier.Value;

        // The command carries no body.
        if (packet.Payload.Length != 0)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // A team the pairing has put in a room is not a team to leave on this
        // record: the same empty body is the client's own transition-complete
        // step, and it is owed the result the assignment sequence answers with.
        // The pair, its room and its lease are all left alone here, because the
        // client joins that room next.
        if (await assignmentService.FindByTeamAsync(teamIdentifier, cancellationToken) is not null)
        {
            logger.LogInformation(
                "Character {CharacterIdentifier} finished entering the room assigned to team {TeamIdentifier}; "
                + "the team and its assignment are kept",
                characterIdentifier,
                teamIdentifier);

            // The ready acknowledgement is the team-leave result record: one
            // command id, 0x4915, read as a bare result by the client.
            await sessionHelper.SendResultAsync(
                session,
                CommandConstants.EventAssignmentReady,
                ErrorCodeConstants.ResultNone,
                cancellationToken);
            return;
        }

        // The projection is captured before the row changes, so the push carries
        // the slot index and sequence the client can still reconcile.
        var before = await teamService.FindAsync(teamIdentifier, cancellationToken);
        if (before is null)
        {
            session.EventTeamIdentifier = null;
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var snapshot = EventTeamService.BuildSnapshot(before);
        var slot = snapshot.IndexOfParticipant(characterIdentifier);

        // The queue is released before the roster changes, so the team is never
        // waiting in the field with a roster that has a member missing.
        await matchmakingService.CancelAsync(teamIdentifier, cancellationToken);

        var outcome = await memberService.LeaveAsync(teamIdentifier, characterIdentifier, cancellationToken);

        await sessionHelper.SendResultAsync(
            session,
            CommandConstants.LeaveEventTeamResult,
            outcome == EventLeaveOutcome.NotAMember
                ? ErrorCodeConstants.ResultGeneral
                : ErrorCodeConstants.ResultNone,
            cancellationToken);

        if (outcome == EventLeaveOutcome.NotAMember)
        {
            return;
        }

        session.EventTeamIdentifier = null;
        session.SelectedEventIdentifier = null;
        invitationService.RemoveForCharacter(characterIdentifier);
        if (outcome == EventLeaveOutcome.TeamDisbanded)
        {
            invitationService.RemoveForTeam(teamIdentifier);
            await pushService.PushTeamClearedAsync(snapshot, session, cancellationToken);
        }
        else
        {
            await pushService.PushParticipantRemovedAsync(snapshot, slot, session, cancellationToken);
        }
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.LeaveEventTeamResult,
            ErrorCodeConstants.ResultGeneral,
            cancellationToken);
}

/// <summary>
/// Records one member's entry decision and pushes it to the team.
/// <para>
/// The decision is also the state change that puts a team into the field, so it
/// is where a scripted self-test run is completed: the moment the team is waiting
/// is the moment the simulated opponent it expects is formed.
/// </para>
/// </summary>
public sealed class SetEventEntryDecisionHandler(
    EventTeamService teamService,
    EventTeamMemberService memberService,
    EventTeamPushService pushService,
    EventMatchmakingService matchmakingService,
    SurvivalTestOpponentService survivalTestOpponent,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // A decision is recorded for a character.
        if (session.CharacterIdentifier is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var characterIdentifier = session.CharacterIdentifier.Value;

        // It decides for the team the connection is attached to.
        if (session.EventTeamIdentifier is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var teamIdentifier = session.EventTeamIdentifier.Value;

        // The request is one decision byte.
        if (packet.Payload.Length != 1)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // The byte is a decision the client can make: yes or no.
        var decision = packet.Payload[0];
        if (decision > 1)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var slot = await memberService.SetDecisionAsync(
            teamIdentifier,
            characterIdentifier,
            decision,
            cancellationToken);

        if (slot < 0)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        await sessionHelper.SendResultAsync(
            session,
            CommandConstants.SetEventEntryDecisionResult,
            ErrorCodeConstants.ResultNone,
            cancellationToken);

        var team = await teamService.FindAsync(teamIdentifier, cancellationToken);
        if (team is not null)
        {
            await pushService.PushParticipantDecisionAsync(
                EventTeamService.BuildSnapshot(team),
                slot,
                cancellationToken);
        }

        // The decision is what makes a team eligible, so it is also the moment
        // the queue is reconciled.
        await matchmakingService.ReconcileAsync(teamIdentifier, cancellationToken);

        // A team the self-test padded is waiting now rather than joinable, which
        // is the moment its simulated opponent is formed. For every other team
        // the call is inert: only a team a run armed and froze is served.
        await survivalTestOpponent.RunAsync(session, teamIdentifier, cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.SetEventEntryDecisionResult,
            ErrorCodeConstants.ResultGeneral,
            cancellationToken);
}
