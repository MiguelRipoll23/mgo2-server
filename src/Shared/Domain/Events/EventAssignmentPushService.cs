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
    /// How long the burst waits for the client's own screen transition to land.
    /// It is measured from the result that answers the client's entry request,
    /// which is written in the same server call this runs in, so the wait covers
    /// the client's own settling rather than any network delay.
    /// </summary>
    private const int AssignmentSettlingMilliseconds = 500;

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

        // The client moves to its asynchronous state only once the answer to its
        // own entry request has been consumed, and that answer is the result this
        // burst immediately follows. An assignment published into that settling
        // screen is overwritten by it — the client ends up holding no match at all
        // and never joins the room it was given — so the burst waits the interval
        // out before it is written. The wait belongs to the client that has just
        // changed state rather than to either team, so it happens once, and the
        // two teams' bursts then follow back-to-back the way the reference
        // publishes them.
        await Task.Delay(TimeSpan.FromMilliseconds(AssignmentSettlingMilliseconds), cancellationToken);
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

            var characterIdentifier = session.CharacterIdentifier.Value;

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

            // The next-match card (0x4A13) belongs to the Tournament draw: it
            // names the bracket's frozen rosters and the client routes it to the
            // Tournament screen family. A Survival pairing is announced by the
            // match-found notification below, which writes the same shared record
            // from the pair's own snapshot, so sending the card here would hand a
            // Survival client a Tournament pairing instead.
            if (assignment.LobbySubtype == EventConstants.TournamentSelector)
            {
                // The card echoes the identity the active-event record above was
                // opened with, which is the event the team record now carries at
                // its entry slot; the client compares the two and drops a card
                // that names anything else.
                var cardWriter = new PacketWriter();
                EventAssignmentUtils.WriteNextMatchCard(
                    cardWriter,
                    team.EventIdentifier,
                    team,
                    opponent,
                    characterIdentifier,
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
            }

            // The match-found notification is the record the client acts on, and
            // it is validated against the team record exactly as the active-event
            // record above is: it quotes the receiving team's own identity, so the
            // two agree with the snapshot that opened the exchange.
            var matchWriter = new PacketWriter();
            EventAssignmentUtils.WriteMatchFound(
                matchWriter,
                team.SnapshotIdentifier,
                team.Sequence,
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
