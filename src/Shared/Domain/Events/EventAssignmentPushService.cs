using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Tells both teams that their match was found and which room is hosting it.
/// <para>
/// The order within a recipient is not arbitrary. A ready acknowledgement opens
/// the sequence, because the client will not consume the snapshot while its team
/// operation is still pending; the snapshot reply then clears the
/// client's event caches, so the live state is restored immediately after it; the
/// event-game cache clears itself before reading its body, so it arrives before
/// the notification that names the two teams; a Tournament assignment's pairing
/// card seeds the shared record before the notification that fills it; and the
/// notification is written last because it is the one the client acts on.
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
    /// <summary>
    /// Pushes an assignment to both teams and to the room's host.
    /// <para>
    /// The burst is written as soon as the room is claimed, and the claim is the
    /// second matchmaking sweep: pairing named the two teams on its own interval
    /// and this sweep found them a room, so the team that entered has long since
    /// settled and the burst cannot be overwritten by the screen that answered
    /// its own request. The two sweeps are what separate "opponent found" from
    /// "game found" — the earlier design waited the interval out here instead,
    /// which left the pairing and the room arriving as one step.
    /// </para>
    /// </summary>
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
            // The host receives the same ready acknowledgement the teams do, so
            // its own pending operation is completed before the host cache lands.
            await sessionHelper.SendResultAsync(
                hostSession,
                CommandConstants.EventAssignmentReady,
                ErrorCodeConstants.ResultNone,
                cancellationToken);

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
    /// Announces the pairing itself: the two teams are named, with the opponent
    /// each is up against, and no room is mentioned because none has been chosen.
    /// <para>
    /// This is the record <c>0x4E20</c> is for — its ksy titles it the Survival
    /// match pairing, and every field it carries is the pair, not the room. It is
    /// written by the pairing sweep, which is what "opponent found" is on the
    /// client: the room burst that follows, on its own slower sweep, re-writes the
    /// same record with the assigned phase and the room.
    /// </para>
    /// <para>
    /// The state carried is the entered phase rather than the assigned one, so the
    /// client is not offered a Join Game row for a room that does not exist yet.
    /// </para>
    /// </summary>
    /// <param name="firstTeam">First team of the pairing.</param>
    /// <param name="secondTeam">Second team of the pairing.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Number of sessions the announcement reached.</returns>
    public async Task<int> PushPairingAsync(
        EventSnapshot firstTeam,
        EventSnapshot secondTeam,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(firstTeam);
        ArgumentNullException.ThrowIfNull(secondTeam);

        var delivered = await PushMatchFoundToTeamAsync(
            firstTeam,
            secondTeam,
            EventConstants.TeamEnteredState,
            cancellationToken);
        delivered += await PushMatchFoundToTeamAsync(
            secondTeam,
            firstTeam,
            EventConstants.TeamEnteredState,
            cancellationToken);
        return delivered;
    }

    /// <summary>
    /// Writes the match-found record to one team's sessions. It is the shared
    /// half of the pairing announcement and the assignment burst, which differ
    /// only in the participation state they carry.
    /// </summary>
    /// <param name="team">Recipient's team.</param>
    /// <param name="opponent">Opposing team.</param>
    /// <param name="teamState">Participation state to carry.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Number of sessions the record reached.</returns>
    private async Task<int> PushMatchFoundToTeamAsync(
        EventSnapshot team,
        EventSnapshot opponent,
        int teamState,
        CancellationToken cancellationToken)
    {
        var delivered = 0;
        foreach (TcpSession session in sessionDirectory.TeamSessions(
            team.SnapshotIdentifier,
            excludedSession: null))
        {
            if (session.CharacterIdentifier is null)
            {
                continue;
            }

            var writer = new PacketWriter();
            EventAssignmentUtils.WriteMatchFound(
                writer,
                team.SnapshotIdentifier,
                team.Sequence,
                team,
                opponent,
                teamState);
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.EventMatchFound,
                writer.Build(),
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

        // The teardown is a team notification like the rest, so it quotes the
        // recipient's own team record: it is validated against that record's
        // identity and serial before its state byte is read, and an assignment
        // selector there is discarded instead of emptying the Survival record.
        var writer = new PacketWriter();
        EventAssignmentUtils.WriteStateUpdate(
            writer,
            team.SnapshotIdentifier,
            team.Sequence,
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
            if (session.CharacterIdentifier is null)
            {
                continue;
            }

            // The assignment opens with the ready acknowledgement. The client
            // does not consume the unsolicited snapshot below while its team
            // operation is still pending, and this zero result completes it; it
            // is also the team-record reset the snapshot itself performs, so it
            // is safe to write unconditionally (the reference's assignment
            // transition opens the same way).
            await sessionHelper.SendResultAsync(
                session,
                CommandConstants.EventAssignmentReady,
                ErrorCodeConstants.ResultNone,
                cancellationToken);

            var snapshotWriter = new PacketWriter();
            EventSnapshotUtils.WriteFull(snapshotWriter, team);
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.GetActiveGameSnapshotResult,
                snapshotWriter.Build(),
                cancellationToken);

            // The active-event record opens the client's event, and it quotes the
            // receiving team's own identity rather than the assignment's: the
            // client checks the record's leading pair against the team record the
            // snapshot above has just filled, and an assignment selector there is
            // rejected with the active-state-mismatch code before the event ever
            // opens — which is a client that ignores its assignment and never
            // enters the room. The same pair is what every later record for that
            // team carries, so the team's own is the only value that keeps the
            // exchange on one identity.
            var activeEventWriter = new PacketWriter();
            EventActiveEventUtils.WriteActiveEventState(
                activeEventWriter,
                team.SnapshotIdentifier,
                team.Sequence,
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

            // The next-match card (0x4A13) refills the same shared record the
            // pairing sweep filled, and it is what this burst uses to do so
            // rather than a second match-found notification.
            //
            // The burst's own 0x4A00 above clears all 356 bytes of that record
            // (0xD51280) and re-seeds only the lobby triple, so the two team
            // names the client renders are gone and have to be written again.
            // Both 0x4A13 and 0x4E20 write them — the same rec+0x14/+0x18 and
            // rec+0x4C/+0x50 slots — but only 0x4E20 ends in the UI event 36 that
            // raises the "your next opponent has been determined" modal, so
            // refilling with it announced the opponent a second time, once per
            // sweep. The card fires UI event 27 instead (0xD45174) and is
            // silent on that screen.
            //
            // It routes to Survival correctly: no 0x4Axx parser branches on
            // subtype — the split is consumer-side on rec+0x08, and this record
            // writes the team's own match type there, so subtype 4 goes to the
            // Survival screen family (0x8CB8FC-0x8CFF40) rather than the
            // in-game round manager. The earlier comment here claimed the card
            // would "hand a Survival client a Tournament pairing"; the routing
            // evidence says otherwise.
            //
            // The card echoes the identity the active-event record above was
            // opened with, because the client compares card_id against the
            // record's rec+0x00 and drops a card naming anything else with
            // -1106.
            var cardWriter = new PacketWriter();
            EventAssignmentUtils.WriteNextMatchCard(
                cardWriter,
                team.EventIdentifier,
                team,
                opponent,
                ReferenceEquals(team, assignment.FirstTeam)
                    ? assignment.FirstRoster
                    : assignment.SecondRoster,
                ReferenceEquals(team, assignment.FirstTeam)
                    ? assignment.SecondRoster
                    : assignment.FirstRoster);
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.EventNextMatchCard,
                cardWriter.Build(),
                cancellationToken);

            // The match-found notification is what raises the opponent-found
            // modal, and a Survival pair already had it from the sweep that
            // paired it — so it is written only for a Tournament pair, which has
            // no pairing sweep and no other record naming the pair.
            if (assignment.LobbySubtype != EventConstants.SurvivalSelector)
            {
                var matchWriter = new PacketWriter();
                EventAssignmentUtils.WriteMatchFound(
                    matchWriter,
                    team.SnapshotIdentifier,
                    team.Sequence,
                    team,
                    opponent,
                    EventConstants.ActiveEventAssignedState);
                await sessionHelper.SendPacketAsync(
                    session,
                    CommandConstants.EventMatchFound,
                    matchWriter.Build(),
                    cancellationToken);
            }

            delivered++;
        }

        return delivered;
    }
}
