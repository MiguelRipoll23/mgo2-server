using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Tells both teams that their match was found and which room is hosting it.
/// <para>
/// The order within a recipient is not arbitrary. The snapshot reply clears the
/// client's event caches, so the live state is restored immediately after it; the
/// event-game cache clears itself before reading its body, so it arrives before
/// the notification that names the two teams; the next-match card seeds the
/// shared ladder record the notification fills; and the notification is written
/// last because it is the one the client acts on.
/// </para>
/// <para>
/// The notification is written per recipient, not once per team: its first
/// identity is the receiving character, and the client compares that with the
/// local character to decide which of the two cached names is the opponent.
/// </para>
/// </summary>
/// <param name="sessionDirectory">Lookup of the sessions this lobby is serving.</param>
/// <param name="sessionHelper">Helper used to write the pushes.</param>
public sealed class EventAssignmentPushService(
    EventSessionDirectoryService sessionDirectory,
    SessionHelper sessionHelper)
{
    /// <summary>Pushes an assignment to both teams and to the room's host.</summary>
    /// <param name="assignment">Assignment to publish.</param>
    /// <param name="hostCharacterIdentifier">Character hosting the room.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Sessions the assignment reached.</returns>
    public async Task<int> PushAssignmentAsync(
        EventAssignment assignment,
        int hostCharacterIdentifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var delivered = await PushToTeamAsync(
            assignment,
            assignment.FirstTeam,
            assignment.SecondTeam,
            cancellationToken);
        delivered += await PushToTeamAsync(
            assignment,
            assignment.SecondTeam,
            assignment.FirstTeam,
            cancellationToken);

        // The dedicated host is a player sitting in the room to host, not a
        // participant, so it is told separately and only when it is connected
        // here; a host in another lobby's process is that process's to tell.
        if (hostCharacterIdentifier > 0
            && sessionDirectory.FindByCharacter(hostCharacterIdentifier) is { } hostSession)
        {
            var hostWriter = new PacketWriter();
            EventAssignmentUtils.WriteEventGameHostInitialize(
                hostWriter,
                hostCharacterIdentifier,
                assignment.FirstTeam,
                assignment.SecondTeam,
                assignment.FirstTeam.HostEnvironment);
            await sessionHelper.SendPacketAsync(
                hostSession,
                CommandConstants.EventGameHostInitialize,
                hostWriter.Build(),
                cancellationToken);
            delivered++;
        }

        return delivered;
    }

    /// <summary>
    /// Tears a live assignment down on both teams. Each team is told with its own
    /// state-update record under the teardown command, which empties the Survival
    /// event record and sends the client back to the battle list.
    /// </summary>
    /// <param name="assignment">Assignment that was cancelled.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Sessions the teardown reached.</returns>
    public async Task<int> PushTeardownAsync(
        EventAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var delivered = 0;
        delivered += await PushTeardownToTeamAsync(assignment, assignment.FirstTeam, cancellationToken);
        delivered += await PushTeardownToTeamAsync(assignment, assignment.SecondTeam, cancellationToken);
        return delivered;
    }

    private async Task<int> PushTeardownToTeamAsync(
        EventAssignment assignment,
        EventSnapshot team,
        CancellationToken cancellationToken)
    {
        var sessions = sessionDirectory.TeamSessions(team.SnapshotIdentifier, excludedSession: null);
        if (sessions.Count == 0)
        {
            // A team whose members have all left needs no teardown; the match is
            // already cancelled, and nothing is owed to a disconnected screen.
            return 0;
        }

        var writer = new PacketWriter();
        EventAssignmentUtils.WriteStateUpdate(
            writer,
            assignment.ActiveStateIdentifier,
            assignment.Sequence,
            team);
        var payload = writer.Build();

        foreach (TcpSession session in sessions)
        {
            await sessionHelper.SendPacketAsync(session, CommandConstants.EventStateUpdate, payload, cancellationToken);
        }

        return sessions.Count;
    }

    private async Task<int> PushToTeamAsync(
        EventAssignment assignment,
        EventSnapshot team,
        EventSnapshot opponent,
        CancellationToken cancellationToken)
    {
        var sessions = sessionDirectory.TeamSessions(team.SnapshotIdentifier, excludedSession: null);
        if (sessions.Count == 0)
        {
            // Nobody is connected to receive it. The assignment stands, because
            // the client asks for its snapshot when it comes back.
            return 0;
        }

        var delivered = 0;
        foreach (TcpSession session in sessions)
        {
            if (session.CharacterIdentifier is not { } characterIdentifier)
            {
                continue;
            }

            var snapshotWriter = new PacketWriter();
            EventSnapshotUtils.WriteFull(snapshotWriter, team);
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.GetActiveGameSnapshotResult,
                snapshotWriter.Build(),
                cancellationToken);

            var activeEventWriter = new PacketWriter();
            EventActiveEventUtils.WriteActiveEventState(
                activeEventWriter,
                assignment.ActiveStateIdentifier,
                assignment.Sequence,
                team.EventIdentifier,
                EventConstants.ActiveEventAssignedState,
                EventActiveEventService.BuildParticipantStates(team),
                team.HostEnvironment,
                assignment.ActivationTimeSeconds,
                flagByte: 0,
                detailByte: 0);
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.ActiveEventStart,
                activeEventWriter.Build(),
                cancellationToken);

            var gameWriter = new PacketWriter();
            EventAssignmentUtils.WriteEventGameInitialize(gameWriter, team, opponent);
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.EventGameInitialize,
                gameWriter.Build(),
                cancellationToken);

            // The next-match card seeds the shared ladder record the notification
            // below names from, so it lands before the client acts on the pairing.
            var cardWriter = new PacketWriter();
            EventAssignmentUtils.WriteNextMatchCard(
                cardWriter,
                assignment.ActiveStateIdentifier,
                team,
                opponent,
                characterIdentifier);
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.EventNextMatchCard,
                cardWriter.Build(),
                cancellationToken);

            var matchWriter = new PacketWriter();
            EventAssignmentUtils.WriteMatchFound(
                matchWriter,
                assignment.ActiveStateIdentifier,
                assignment.Sequence,
                team,
                opponent,
                characterIdentifier);
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.EventMatchFound,
                matchWriter.Build(),
                cancellationToken);

            delivered++;
        }

        return delivered;
    }
}
