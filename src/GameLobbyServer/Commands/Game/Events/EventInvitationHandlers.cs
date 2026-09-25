using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>Invites up to three characters to the sender's team.</summary>
public sealed class InviteEventTeamMembersHandler(
    EventInvitationService invitationService,
    EventTeamService teamService,
    EventSessionDirectoryService sessionDirectory,
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var leaderIdentifier = session.CharacterIdentifier;
        var teamIdentifier = session.EventTeamIdentifier;
        var lobbyIdentifier = session.LobbyIdentifier;

        if (leaderIdentifier is not { } leader
            || teamIdentifier is not { } team
            || lobbyIdentifier is not { } lobby
            || !TryParse(packet.Payload, out var mode, out var targets)
            || mode != EventInvitationService.SurvivalMode)
        {
            await ReplyAsync(session, ErrorCodeConstants.ResultGeneral, [], cancellationToken);
            return;
        }

        var projection = await teamService.FindAsync(team, cancellationToken);
        if (projection is null)
        {
            await ReplyAsync(session, ErrorCodeConstants.ResultGeneral, [], cancellationToken);
            return;
        }

        var snapshot = EventTeamService.BuildSnapshot(projection);
        if (snapshot.Participants[0].CharacterIdentifier != leader)
        {
            await ReplyAsync(session, ErrorCodeConstants.ResultGeneral, [], cancellationToken);
            return;
        }

        var character = await characterService.FindByIdAsync(leader, cancellationToken);
        var leaderName = character?.Name ?? snapshot.HostName;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var results = new List<(int Target, int Result)>();
        var created = new List<EventInvitation>();

        foreach (var target in targets.Distinct())
        {
            var result = ErrorCodeConstants.ResultGeneral;
            var targetSession = sessionDirectory.FindByCharacter(target);

            if (target != leader
                && targetSession is not null
                && targetSession.LobbyIdentifier == lobby
                && targetSession.EventTeamIdentifier is null
                && snapshot.OccupiedParticipantCount()
                    + invitationService.CountReservedForTeam(team, now) < EventConstants.TeamMemberLimit)
            {
                var targetCharacter = await characterService.FindByIdAsync(target, cancellationToken);
                var invitation = invitationService.Create(
                    leader,
                    leaderName,
                    target,
                    targetCharacter?.Name ?? string.Empty,
                    team,
                    lobby,
                    mode,
                    now);

                if (invitation is not null)
                {
                    result = ErrorCodeConstants.ResultNone;
                    created.Add(invitation);
                }
            }

            results.Add((target, (int)result));
        }

        await ReplyAsync(session, 0, results, cancellationToken);

        foreach (var invitation in created)
        {
            await SendNotificationAsync(
                invitation.TargetCharacterIdentifier,
                invitation.LobbyIdentifier,
                invitation.Identifier,
                invitation.CreatedAt,
                state: 1,
                invitation.Mode,
                invitation.LeaderName,
                cancellationToken);
        }
    }

    private Task ReplyAsync(
        TcpSession session,
        uint result,
        IReadOnlyList<(int Target, int Result)> items,
        CancellationToken cancellationToken)
    {
        var writer = new PacketWriter();
        EventInvitationUtils.WriteInviteResponse(writer, (int)result, items);
        return sessionHelper.SendPacketAsync(
            session,
            CommandConstants.InviteEventTeamMembers,
            writer.Build(),
            cancellationToken);
    }

    private Task SendNotificationAsync(
        int characterIdentifier,
        int lobbyIdentifier,
        int identifier,
        long timestamp,
        int state,
        int mode,
        string name,
        CancellationToken cancellationToken)
    {
        var targetSession = sessionDirectory.FindByCharacter(characterIdentifier);
        if (targetSession is null)
        {
            return Task.CompletedTask;
        }

        var writer = new PacketWriter();
        EventInvitationUtils.WriteNotification(writer, lobbyIdentifier, identifier, timestamp, state, mode, 0, name);
        return sessionHelper.SendPacketAsync(
            targetSession,
            CommandConstants.InviteEventTeamMembersResult,
            writer.Build(),
            cancellationToken);
    }

    /// <summary>
    /// A request is a target count, a mode byte and that many character
    /// identifiers. The count is checked against the payload length so a
    /// truncated request is refused rather than read past its end.
    /// </summary>
    private static bool TryParse(byte[] payload, out int mode, out List<int> targets)
    {
        mode = 0;
        targets = [];
        if (payload.Length < 5)
        {
            return false;
        }

        var reader = new PacketReader(payload);
        var count = (int)reader.ReadUInt32();
        if (count < 1 || count > EventInvitationService.MaximumTargets
            || payload.Length != 5 + count * 4)
        {
            return false;
        }

        mode = reader.ReadUInt8();
        for (var index = 0; index < count; index++)
        {
            targets.Add((int)reader.ReadUInt32());
        }

        return true;
    }
}

/// <summary>Answers an invitation and tells the sender.</summary>
public sealed class AnswerEventTeamInvitationHandler(
    EventInvitationService invitationService,
    EventSessionDirectoryService sessionDirectory,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var targetIdentifier = session.CharacterIdentifier;
        if (targetIdentifier is not { } target
            || packet.Payload.Length != 5)
        {
            await WriteAnswerAsync(session, ErrorCodeConstants.ResultGeneral, 0, 0, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var invitationIdentifier = (int)reader.ReadUInt32();
        var choice = reader.ReadUInt8();
        if (invitationIdentifier == 0
            || (choice != EventInvitationService.ChoiceYes && choice != EventInvitationService.ChoiceNo))
        {
            await WriteAnswerAsync(session, ErrorCodeConstants.ResultGeneral, invitationIdentifier, choice, cancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var invitation = invitationService.TakePending(invitationIdentifier, target, now);
        if (invitation is null)
        {
            await WriteAnswerAsync(session, ErrorCodeConstants.ResultGeneral, invitationIdentifier, choice, cancellationToken);
            return;
        }

        if (choice == EventInvitationService.ChoiceYes)
        {
            // The reservation holds the target's slot until they actually join;
            // membership is committed by 0x4912, not by the answer.
            invitationService.Accept(invitation, now);
        }

        await WriteAnswerAsync(session, ErrorCodeConstants.ResultNone, invitationIdentifier, choice, cancellationToken);
        await NotifyLeaderAsync(invitation, choice, cancellationToken);

        if (choice == EventInvitationService.ChoiceYes)
        {
            foreach (var cancelled in invitationService.CancelOthersForTarget(target, invitationIdentifier, now))
            {
                await NotifyLeaderAsync(cancelled, EventInvitationService.ChoiceNo, cancellationToken);
            }
        }
    }

    private Task WriteAnswerAsync(
        TcpSession session,
        uint result,
        int invitationIdentifier,
        int state,
        CancellationToken cancellationToken)
    {
        var writer = new PacketWriter();
        EventInvitationUtils.WriteAnswerResponse(writer, (int)result, invitationIdentifier, state);
        return sessionHelper.SendPacketAsync(
            session,
            CommandConstants.AnswerEventTeamInvitationResult,
            writer.Build(),
            cancellationToken);
    }

    private Task NotifyLeaderAsync(EventInvitation invitation, int state, CancellationToken cancellationToken)
    {
        var leaderSession = sessionDirectory.FindByCharacter(invitation.LeaderCharacterIdentifier);
        if (leaderSession is null)
        {
            return Task.CompletedTask;
        }

        var writer = new PacketWriter();
        EventInvitationUtils.WriteNotification(
            writer,
            invitation.LobbyIdentifier,
            invitation.TargetCharacterIdentifier,
            invitation.CreatedAt,
            state,
            invitation.Mode,
            0,
            invitation.TargetName);
        return sessionHelper.SendPacketAsync(
            leaderSession,
            CommandConstants.InviteEventTeamMembersResult,
            writer.Build(),
            cancellationToken);
    }
}

/// <summary>Acknowledges the invitation state the leader's screen is showing.</summary>
public sealed class ReportEventInvitationStatusHandler(
    EventTeamService teamService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Size of the status report the screen sends.</summary>
    private const int ReportWireSize = 17;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var result = ErrorCodeConstants.ResultGeneral;

        if (session.EventTeamIdentifier is { } teamIdentifier
            && packet.Payload.Length == ReportWireSize)
        {
            var team = await teamService.FindAsync(teamIdentifier, cancellationToken);

            // Only the leader's own screen may acknowledge the state it is showing.
            var isReportedByOwner = team is not null
                && session.CharacterIdentifier == team.OwnerCharacterIdentifier;

            if (isReportedByOwner)
            {
                result = ErrorCodeConstants.ResultNone;
            }
        }

        await sessionHelper.SendResultAsync(
            session,
            CommandConstants.ReportEventInvitationStatusResult,
            result,
            cancellationToken);
    }
}
