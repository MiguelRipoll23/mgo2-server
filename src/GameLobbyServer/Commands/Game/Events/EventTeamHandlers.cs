using System.Text;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>Validation of the text fields the team commands carry.</summary>
internal static class EventTeamTextUtils
{
    /// <summary>
    /// Retail accepts three to sixteen encoded bytes. A shorter or longer string
    /// is refused rather than padded, because the slot is the client's own field
    /// and padding would change what was typed.
    /// </summary>
    /// <param name="value">Text to test.</param>
    public static bool IsValidTeamName(string value) => IsValidText(value, 3, 16);

    /// <summary>Validates the password of a protected team.</summary>
    /// <param name="flagBits">Option bits of the team.</param>
    /// <param name="password">Password to test.</param>
    public static bool IsValidPassword(int flagBits, string password) =>
        (flagBits & EventTeamService.PasswordProtectedFlag) == 0 || IsValidText(password, 3, 16);

    private static bool IsValidText(string value, int minimum, int maximum)
    {
        if (value is null)
        {
            return false;
        }

        // Encoded length, because the client measures the wire field rather than
        // the character count, and the control-byte rule is about encoded bytes.
        var encoded = Encoding.Latin1.GetBytes(value);
        if (encoded.Length < minimum || encoded.Length > maximum)
        {
            return false;
        }

        foreach (var item in encoded)
        {
            if (item is >= 0x01 and <= 0x1f)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>Creates an event team and answers with its active-game snapshot.</summary>
public sealed class CreateEventTeamHandler(
    EventTeamService teamService,
    CharacterService characterService,
    LobbyService lobbyService,
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Logical size of a create request.</summary>
    private const int LogicalWireSize = 178;

    /// <summary>Logical size plus the transport padding the client may append.</summary>
    private const int PaddedWireSize = 184;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled
            || session.CharacterIdentifier is not { } characterIdentifier
            || session.LobbyIdentifier is not { } lobbyIdentifier
            || (packet.Payload.Length != LogicalWireSize && packet.Payload.Length != PaddedWireSize))
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var name = reader.ReadFixedString(16);
        var comment = reader.ReadFixedString(128);
        var flagBits = reader.ReadUInt8();
        var password = reader.ReadFixedString(16);
        var matchType = reader.ReadUInt8();
        var field0a8 = reader.ReadUInt8();
        var field0a9 = reader.ReadUInt8();
        var field0ac = (int)reader.ReadUInt32();
        var eventIdentifier = (int)reader.ReadUInt32();
        var field2e0 = (int)reader.ReadUInt32();
        var field2e8 = reader.ReadUInt16();

        if (!EventTeamTextUtils.IsValidTeamName(name)
            || !EventTeamTextUtils.IsValidPassword(flagBits, password))
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // The team's match type is the lobby it forms in; a mismatch means a
        // Survival team is being created in a Tournament lobby.
        var lobby = await lobbyService.FindByIdAsync(lobbyIdentifier, cancellationToken);
        if (matchType != lobby.SubtypeIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
        var team = await teamService.CreateAsync(
            characterIdentifier,
            character?.Name ?? string.Empty,
            character?.Experience ?? 0,
            name,
            comment,
            flagBits,
            password,
            matchType,
            lobbyIdentifier,
            eventIdentifier == 0 ? EventConstants.TransientEventIdentifier : eventIdentifier,
            cancellationToken);

        session.EventTeamIdentifier = team.Identifier;

        // A team that has just been formed supersedes any event the connection
        // had merely looked at. Leaving the selection in place would let the
        // shared entry request read an older screen than the one the player is
        // on, and cancel a team instead of entering an event.
        session.SelectedEventIdentifier = null;

        var snapshot = EventTeamService.BuildSnapshot(team);
        snapshot.Field0A8 = field0a8;
        snapshot.Field0A9 = field0a9;
        snapshot.Field0AC = field0ac;
        snapshot.Field2E0 = field2e0;
        snapshot.Field2E8 = field2e8;

        var writer = new PacketWriter();
        EventSnapshotUtils.WriteCompact(writer, snapshot);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.CreateEventTeamResult,
            writer.Build(),
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.CreateEventTeamResult,
            ErrorCodeConstants.ResultGeneral,
            cancellationToken);
}

/// <summary>Joins a team, commits the membership and pushes the new roster slot.</summary>
public sealed class JoinEventTeamHandler(
    EventTeamService teamService,
    EventTeamPushService pushService,
    EventInvitationService invitationService,
    EventMatchmakingService matchmakingService,
    CharacterService characterService,
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Logical size of a join request.</summary>
    private const int LogicalWireSize = 20;

    /// <summary>Logical size plus the transport padding the client may append.</summary>
    private const int PaddedWireSize = 24;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled
            || session.CharacterIdentifier is not { } characterIdentifier
            || session.LobbyIdentifier is not { } lobbyIdentifier
            || (packet.Payload.Length != LogicalWireSize && packet.Payload.Length != PaddedWireSize))
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var teamIdentifier = (int)reader.ReadUInt32();
        var password = reader.ReadFixedString(16);
        if (teamIdentifier == 0)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
        var (outcome, team) = await teamService.JoinAsync(
            teamIdentifier,
            lobbyIdentifier,
            characterIdentifier,
            character?.Name ?? string.Empty,
            character?.Experience ?? 0,
            password,
            cancellationToken);

        if (outcome != EventJoinOutcome.Joined || team is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // Membership is committed here, so the reservation an accepted
        // invitation made for this character is released with it.
        invitationService.ClearAccepted(characterIdentifier);
        session.EventTeamIdentifier = team.Identifier;
        session.SelectedEventIdentifier = null;
        var snapshot = EventTeamService.BuildSnapshot(team);

        var writer = new PacketWriter();
        EventSnapshotUtils.WriteCompact(writer, snapshot);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.JoinEventTeamResult,
            writer.Build(),
            cancellationToken);

        var slot = snapshot.IndexOfParticipant(characterIdentifier);
        await pushService.PushParticipantAddedAsync(snapshot, slot, session, cancellationToken);

        // A roster change can make a queued team no longer ready, which is
        // exactly what the reconcile turns into a cancellation.
        await matchmakingService.ReconcileAsync(team.Identifier, cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.JoinEventTeamResult,
            ErrorCodeConstants.ResultGeneral,
            cancellationToken);
}

/// <summary>Disbands a team the caller leads, or removes the caller from one.</summary>
public sealed class LeaveEventTeamHandler(
    EventTeamService teamService,
    EventTeamPushService pushService,
    EventInvitationService invitationService,
    EventMatchmakingService matchmakingService,
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled
            || session.CharacterIdentifier is not { } characterIdentifier
            || session.EventTeamIdentifier is not { } teamIdentifier
            || packet.Payload.Length != 0)
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
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled
            || session.CharacterIdentifier is not { } characterIdentifier
            || session.EventTeamIdentifier is not { } teamIdentifier
            || packet.Payload.Length != 1
            || packet.Payload[0] > 1)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var decision = packet.Payload[0];
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

/// <summary>Streams the joinable teams of the lobby the caller is in.</summary>
public sealed class GetEventTeamListHandler(
    EventTeamService teamService,
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled
            || session.LobbyIdentifier is not { } lobbyIdentifier
            || packet.Payload.Length != 0)
        {
            await sessionHelper.SendResultAsync(
                session,
                CommandConstants.GetEventTeamListStart,
                ErrorCodeConstants.ResultGeneral,
                cancellationToken);
            return;
        }

        var teams = await teamService.FindJoinableAsync(lobbyIdentifier, cancellationToken);

        await sessionHelper.SendStartEndPacketAsync(
            session,
            CommandConstants.GetEventTeamListStart,
            cancellationToken);

        foreach (var team in teams)
        {
            var writer = new PacketWriter();
            EventTeamListUtils.WriteItem(writer, EventTeamService.BuildSnapshot(team));
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.GetEventTeamListPage,
                writer.Build(),
                cancellationToken);
        }

        await sessionHelper.SendStartEndPacketAsync(
            session,
            CommandConstants.GetEventTeamListEnd,
            cancellationToken);
    }
}

/// <summary>Returns the detail of one joinable team.</summary>
public sealed class GetEventTeamDetailsHandler(
    EventTeamService teamService,
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var teamIdentifier = options.Value.Enabled && packet.Payload.Length == 4
            ? (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(packet.Payload)
            : 0;

        var team = teamIdentifier > 0
            ? await teamService.FindAsync(teamIdentifier, cancellationToken)
            : null;

        if (team is null)
        {
            await sessionHelper.SendResultAsync(
                session,
                CommandConstants.GetEventTeamDetailsResult,
                ErrorCodeConstants.ResultGeneral,
                cancellationToken);
            return;
        }

        var writer = new PacketWriter();
        EventSnapshotUtils.WriteCompact(writer, EventTeamService.BuildSnapshot(team));
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetEventTeamDetailsResult,
            writer.Build(),
            cancellationToken);
    }
}
