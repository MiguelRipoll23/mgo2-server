using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Fans the team roster mutations out to the members of one team. The client has
/// no local echo, so a member that joined or changed its decision is told by the
/// server, and every other member's screen is updated by the same push rather
/// than by re-reading the team.
/// <para>
/// Recipients are found by the team identifier the session carries, which is
/// routing context only: a session that reconnected is included because it
/// re-attached to the same team, and a session that never joined is not.
/// </para>
/// </summary>
/// <param name="sessionDirectory">Lookup of the sessions this lobby is serving.</param>
/// <param name="sessionHelper">Helper used to write the pushes.</param>
public sealed class EventTeamPushService(
    EventSessionDirectoryService sessionDirectory,
    SessionHelper sessionHelper)
{
    /// <summary>Pushes an added participant to every other member.</summary>
    /// <param name="team">Team that changed.</param>
    /// <param name="slot">Slot that was filled.</param>
    /// <param name="excludedSession">Session that is already being answered, when there is one.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task PushParticipantAddedAsync(
        EventSnapshot team,
        int slot,
        TcpSession? excludedSession,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);
        if (slot < 0 || slot >= team.Participants.Length)
        {
            return;
        }

        var writer = new PacketWriter();
        writer.WriteInt32(team.SnapshotIdentifier);
        writer.WriteUInt16(team.Sequence);
        writer.WriteUInt8(slot);
        writer.WriteInt32(team.Participants[slot].CharacterIdentifier);
        writer.WriteFixedString(team.Participants[slot].Name, 16);
        writer.WriteUInt8(team.Participants[slot].State);

        await BroadcastAsync(
            team.SnapshotIdentifier,
            excludedSession,
            CommandConstants.EventParticipantAdded,
            writer.Build(),
            cancellationToken);
    }

    /// <summary>Pushes a removed participant to every other member.</summary>
    /// <param name="team">Team that changed.</param>
    /// <param name="slot">Slot that was vacated.</param>
    /// <param name="excludedSession">Session that is already being answered, when there is one.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task PushParticipantRemovedAsync(
        EventSnapshot team,
        int slot,
        TcpSession? excludedSession,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        var writer = new PacketWriter();
        writer.WriteInt32(team.SnapshotIdentifier);
        writer.WriteUInt16(team.Sequence);
        writer.WriteInt32(slot);

        await BroadcastAsync(
            team.SnapshotIdentifier,
            excludedSession,
            CommandConstants.EventParticipantRemoved,
            writer.Build(),
            cancellationToken);
    }

    /// <summary>Pushes a changed decision to every member, including its owner.</summary>
    /// <param name="team">Team that changed.</param>
    /// <param name="slot">Slot whose decision changed.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task PushParticipantDecisionAsync(
        EventSnapshot team,
        int slot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);
        if (slot < 0 || slot >= team.Participants.Length)
        {
            return;
        }

        var writer = new PacketWriter();
        writer.WriteInt32(team.SnapshotIdentifier);
        writer.WriteUInt16(team.Sequence);
        writer.WriteUInt8(slot);
        writer.WriteInt32(team.Participants[slot].CharacterIdentifier);
        writer.WriteUInt8(team.Participants[slot].State);

        await BroadcastAsync(
            team.SnapshotIdentifier,
            excludedSession: null,
            CommandConstants.EventParticipantDecision,
            writer.Build(),
            cancellationToken);
    }

    /// <summary>
    /// Re-opens the team's event with its changed lifecycle state and pushes it
    /// to every member.
    /// <para>
    /// The team screens are built for an event that was opened, and the one
    /// record that carries the team's event-participation state is the
    /// active-event start. A state change that is not pushed therefore leaves
    /// every member on the screen the state they held before — which for a team
    /// that has just entered is the difference between "Forming a team" and
    /// "Entry Complete". The record is written once and fanned out, because the
    /// state is the team's rather than the recipient's.
    /// </para>
    /// </summary>
    /// <param name="team">Team whose state changed.</param>
    /// <param name="teamState">State the team now holds.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task PushActiveEventStateAsync(
        EventSnapshot team,
        int teamState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        var sessions = Recipients(team.SnapshotIdentifier, excludedSession: null);
        if (sessions.Count == 0)
        {
            // A team whose members have all left has no screen to move; the
            // state is already recorded and the next read serves it.
            return;
        }

        var writer = new PacketWriter();
        EventActiveEventUtils.WriteActiveEventState(
            writer,
            team.SnapshotIdentifier,
            team.Sequence,
            team.EventIdentifier,
            teamState,
            EventActiveEventService.BuildParticipantStates(team),
            team.HostEnvironment,
            baseTimeSeconds: 0,
            flagByte: 0,
            detailByte: 0);
        var payload = writer.Build();

        foreach (var session in sessions)
        {
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.ActiveEventStart,
                payload,
                cancellationToken);
        }
    }

    /// <summary>Tells every remaining member that the team is gone.</summary>
    /// <param name="team">Team that ended.</param>
    /// <param name="excludedSession">Session that is already being answered, when there is one.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task PushTeamClearedAsync(
        EventSnapshot team,
        TcpSession? excludedSession,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(team);

        var writer = new PacketWriter();
        writer.WriteInt32(team.SnapshotIdentifier);
        writer.WriteUInt16(team.Sequence);

        await BroadcastAsync(
            team.SnapshotIdentifier,
            excludedSession,
            CommandConstants.EventTeamCleared,
            writer.Build(),
            cancellationToken);
    }

    private async Task BroadcastAsync(
        int teamIdentifier,
        TcpSession? excludedSession,
        ushort command,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        foreach (var session in Recipients(teamIdentifier, excludedSession))
        {
            await sessionHelper.SendPacketAsync(session, command, payload, cancellationToken);
        }
    }

    private List<TcpSession> Recipients(int teamIdentifier, TcpSession? excludedSession) =>
        sessionDirectory.TeamSessions(teamIdentifier, excludedSession);
}
