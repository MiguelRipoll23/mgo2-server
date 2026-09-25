using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>Disbands a team the caller leads, or removes the caller from one.</summary>
public sealed class LeaveEventTeamHandler(
    EventTeamService teamService,
    EventTeamPushService pushService,
    EventInvitationService invitationService,
    EventMatchmakingService matchmakingService,
    SessionHelper sessionHelper) : ICommandHandler
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

        var outcome = await teamService.LeaveAsync(teamIdentifier, characterIdentifier, cancellationToken);

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

/// <summary>Records one member's entry decision and pushes it to the team.</summary>
public sealed class SetEventEntryDecisionHandler(
    EventTeamService teamService,
    EventTeamPushService pushService,
    EventMatchmakingService matchmakingService,
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

        var slot = await teamService.SetDecisionAsync(
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
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.SetEventEntryDecisionResult,
            ErrorCodeConstants.ResultGeneral,
            cancellationToken);
}
