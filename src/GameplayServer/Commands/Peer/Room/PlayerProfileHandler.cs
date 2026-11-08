using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

/// <summary>
/// Handles a player-profile record. The joiner sends its own profile once the
/// session is keyed and then re-sends it, byte for byte, until the host answers;
/// this handler answers with the whole room roster and announces the new player
/// to peers already in the room.
/// </summary>
/// <remarks>
/// <para>
/// The type is shared. A joining client also sends one-byte <c>0x1001</c>
/// messages, which are not profiles and are not answered with a roster.
/// </para>
/// <para>
/// <b>The copy answered is the one the client re-sends.</b> The client sends its
/// profile once the session is keyed, and re-sends the same body byte for byte on
/// its own 1.93 s timer; the recorded host's roster run follows the <em>second</em>
/// copy — 1.934 s between the joiner's two copies and 0.542 s from the second to
/// the run in `mgo2-game1`, and the same shape in `mgo2-started-on-solo-game` and
/// `mgo2-game-started-on-briefing` (`UDP_JOIN_FLOW.md` §7,
/// `docs/analysis/join-stall-after-mode2.md` §5). The first copy is therefore held
/// and the re-send is answered in the pass that receives it, through
/// <see cref="RosterAnswerService"/>; a copy that arrives anyway is answered the
/// same way, and the member is filed either way, so a re-send lands in the slot
/// the client already has.
/// </para>
/// <para>
/// <b>What the host did before.</b> It answered the first copy, in the pass that
/// received it, on the reasoning that this keeps the re-send off the wire. It
/// does — and that is the problem: both live captures hold <em>one</em> profile
/// copy and no re-send at all, where every reference client re-sends and is
/// answered on the second. What the earlier reading could not say is whether a
/// client answered 2.4 s earlier is worse off; the two live captures say it does
/// not join, which is why the recorded host's copy is the one answered now.
/// </para>
/// </remarks>
/// <param name="roster">Roster of the room this host is playing.</param>
/// <param name="rosterAnswers">
/// Decider of which copy of a peer's profile is answered: the re-sent one in the
/// pass it arrives in, or the first one when the client never re-sends.
/// </param>
/// <param name="burst">
/// Builder of the answered peer's settings payload, which closes the roster run
/// (<see cref="SendRosterAsync"/>).
/// </param>
/// <param name="loadoutReader">Reader of the room's own players and their loadouts.</param>
/// <param name="burstScheduler">Scheduler of the one-shot settings burst after roster exchange.</param>
/// <param name="matchRecords">Records the recorded host keeps writing after the burst.</param>
/// <param name="matchStart">Writer of the run the room's next round opens with.</param>
/// <param name="teamArrays">The lobby-slot array handshake, which answers under two of these identifiers.</param>
/// <param name="score">
/// The host's own score, which a scoreboard delta arriving on this identifier
/// is merged into. The identifier carries the scoreboard as well as the roster,
/// and the two are told apart by their length (<see cref="MatchScoreboardFramesUtils"/>).
/// </param>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerProfileHandler(
    RoomRosterService roster,
    RosterAnswerService rosterAnswers,
    PlayerSettingsBurstService burst,
    LobbyLoadoutService loadoutReader,
    PlayerSettingsBurstSchedulerService burstScheduler,
    MatchRecordService matchRecords,
    MatchStartRunService matchStart,
    TeamArrayService teamArrays,
    MatchScoreService score,
    ILogger<PlayerProfileHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        // The lobby-slot array handshake answers the pair under these two
        // identifiers and no other, and a two-byte body naming the pair's
        // sequence byte is that answer and not a profile. It is read here
        // because the registry binds one handler to an identifier and this is
        // the one already bound to both.
        if (TeamArrayFramesUtils.IsArrayEcho(context.Message.Body))
        {
            // The echo is the client's own request to be placed in the room, and
            // the room's state is what answers it: the run the peer is owed is
            // written for the entry and not for its report
            // (<see cref="MatchStartRunService.OnRoomEntry"/>), which arrives
            // while the client is still loading its map.
            //
            // The lobby-slot confirmation is not written here. Every capture
            // writes it far behind the entry and none beside it, and a client
            // answered with it in this pass stops where it stands
            // (`myserver20.pcapng`: it acknowledged the run and then sent nothing
            // but its heartbeat). The entry is noted with its sequence, and the
            // confirmation is written when the room's round opens
            // (<see cref="MatchStartRunService.ConfirmEntryAsync"/>).
            // The byte the client answers under is its own sequence, and the
            // arrays this host publishes are owed back under it: every reference
            // host answers `82 8c` with `85 8c`, and a host that answers with a
            // captured client's byte leaves the peer's exchange unconfirmed.
            var echoed = TeamArrayFramesUtils.SequenceOf(context.Message.Body);
            roster.NotePairSequence(context.Remote.ToString(), echoed);
            var first = matchStart.OnRoomEntry(context);

            // <b>A re-sent echo is a pair this host has not answered, and the
            // pair is all it is answered with.</b> A client repeats `82 NN`·`84 NN`
            // on its own timer until the room's arrays come back, advancing the
            // sequence as each pair is read. The first echo's pass writes nothing
            // because every capture's is silent (`myserver20.pcapng`), and a
            // repeat is the client saying the pair never arrived — the case
            // `myserver22.pcapng` records, where it is answered nowhere, the
            // session is torn down 32 s after the report and the modal follows.
            // What answers it is the pair under its own sequence and nothing more
            // (`TeamArrayService.AnswerEchoAsync`); the confirmation belongs to the
            // entry and is written when the round opens
            // (`MatchStartRunService.ConfirmEntryAsync`).
            if (!first && matchStart.IsEntryAnswerDue(context.Session))
            {
                await teamArrays.AnswerEchoAsync(context, echoed);
            }

            return;
        }

        // The same identifier carries the client's own request to be placed in
        // the room: four bytes opening 0x83, the array family's third type and
        // the only one of it this host does not answer. Every capture answers
        // it with the room's state, and both live captures hold one copy and no
        // answer, which is why it is read here rather than falling through to
        // the profile parse below - a four-byte body carries no name and that
        // path drops it as "no profile".
        if (TeamArrayFramesUtils.IsRoomEntryRequest(context.Message.Body))
        {
            // The request names the same sequence the answers do, and it can be
            // the first record of the exchange a peer sends.
            roster.NotePairSequence(
                context.Remote.ToString(),
                TeamArrayFramesUtils.RequestSequenceOf(context.Message.Body));
            await teamArrays.EnterRoomAsync(
                context,
                TeamArrayFramesUtils.RequestSequenceOf(context.Message.Body));
            return;
        }

        // The same identifier carries the game's own scoreboard, and its body is
        // the only other shape on it: the settings discriminator, a player's slot
        // and the values that changed. A peer that writes one is telling the room
        // a score this host has not counted itself, so it is merged and passed on
        // rather than read as a profile the peer never sent.
        if (MatchScoreboardFramesUtils.TryParse(context.Message.Body, out var delta))
        {
            MergeScoreboard(context, delta);
            var relay = context.Relay ?? context.Broadcast;
            await relay(context.Message.Type, context.Message.Body, context.Message.Flags);
            return;
        }

        var profile = PlayerProfileRecordParseUtils.Parse(context.Message.Body);
        if (profile is not { Name.Length: > 0 })
        {
            logger.LogDebug(
                "UDP {LocalPort}: {MessageType:x4} of {BodyLength} bytes from {RemoteAddress} carries no profile; not answered",
                context.LocalPort,
                context.Message.Type,
                context.Message.Body.Length,
                context.Remote);
            return;
        }

        var member = roster.Register(
            context.Remote,
            profile,
            context.Session.PeerIdentifier,
            context.Session.PeerAddressData);
        logger.LogDebug(
            "UDP {LocalPort}: registered roster entry for {RemoteAddress}: character {CharacterId}, "
            + "slot {RosterIndex}, current joiner count {JoinerCount}",
            context.LocalPort,
            context.Remote,
            member.CharacterId,
            member.RosterIndex,
            roster.JoinerCount);

        if (context.Session.PeerAddressData.Length != PlayerProfileRecordUtility.AddressDataLength)
        {
            logger.LogWarning(
                "UDP {LocalPort}: profile from {RemoteAddress} has no valid pair of handshake endpoints; "
                + "its roster address fields cannot be echoed faithfully",
                context.LocalPort,
                context.Remote);
        }

        logger.LogInformation(
            "UDP {LocalPort}: profile from {RemoteAddress}: character {CharacterId} name {Name}, roster slot {RosterIndex}",
            context.LocalPort,
            context.Remote,
            member.CharacterId,
            profile.Name,
            member.RosterIndex);

        // The copy answered is the copy the client re-sends, in the pass it
        // arrives in: a client answers its own 1.93 s retry timer by sending the
        // profile again, and the recorded host's roster run follows that second
        // copy (`RosterAnswerService`). The first copy is therefore held, and a
        // client that does not re-send is answered when the hold's deadline
        // passes rather than never.
        var answer = () => SendRosterAsync(context);

        if (rosterAnswers.AnswerNowOrHold(context.Remote.ToString(), context, answer))
        {
            await answer();
        }
    }

    /// <summary>
    /// Merges a scoreboard delta a peer wrote into the host's own score: the bits
    /// this file names, and nothing for the ones it does not.
    /// </summary>
    /// <param name="context">Context the delta arrived through.</param>
    /// <param name="delta">Slot and values the delta carried.</param>
    private void MergeScoreboard(PeerContext context, MatchScoreboardFramesUtils.ScoreboardDelta delta)
    {
        logger.LogDebug(
            "UDP {LocalPort}: {RemoteAddress} reports a scoreboard delta for slot {Slot} with "
            + "{ValueCount} value(s)",
            context.LocalPort,
            context.Remote,
            delta.Slot,
            delta.Values.Count);

        foreach (var value in delta.Values)
        {
            switch (value.Bit)
            {
                case MatchScoreboardFramesUtils.KillsBit:
                    score.NoteKills(delta.Slot, value.Value);
                    break;
                case MatchScoreboardFramesUtils.DeathsBit:
                    score.NoteDeaths(delta.Slot, value.Value);
                    break;
                case MatchScoreboardFramesUtils.ScoreBit:
                    score.NoteScore(delta.Slot, value.Value);
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Writes the roster run and announces it to the room's other peers.
    /// </summary>
    /// <remarks>
    /// <b>One frame, and not three.</b> This wrote the run and then a second copy
    /// of it 0.1 ms later and a bare <c>0x5001</c> head behind that, on the reading
    /// that the recorded host repeats a roster run. `briefing-then-solo-game` — the
    /// port client's own join — writes the run once, in one frame, and nothing
    /// between it and the settings burst 3.0 s later: no second copy, no trailing
    /// head. A repeat of the same records inside one pass is not the measured
    /// repeat either, which is a whole set written again seconds later on the
    /// tick (`MatchStartRunService.RepeatRunAsync`); the two extra frames only put
    /// three copies of one roster in front of a client that has not answered yet.
    /// </remarks>
    /// <remarks>
    /// <b>The run closes on the answered peer's own settings payload.</b> The
    /// reference host's roster answer carries it at ordinal 4, ahead of the burst
    /// that writes the same body again 2.1 s later
    /// (<see cref="PlayerSettingsBurstService.AnsweringLoadoutBody"/>), so the
    /// loadouts are read here — in the pass that answers, as the burst's own read
    /// is — and the run is built with the peer the answer is addressed to.
    /// </remarks>
    /// <param name="context">Context of the profile this roster answers.</param>
    private async Task SendRosterAsync(PeerContext context)
    {
        var answeringSlot = roster.Find(context.Session.RemoteAddress) is { } answered
            ? (byte)(answered.SessionSlot + 1)
            : (byte)0;

        // The room's rows are read here, and a read that fails must not cost the
        // join its answer: the roster run is the one message a joiner cannot be
        // left without, so a failure leaves the payload out and the run closes on
        // the room's count, which is what this host wrote before the payload was
        // moved into the run at all.
        byte[]? answeringLoadout = null;
        try
        {
            answeringLoadout = burst.AnsweringLoadoutBody(
                await loadoutReader.ReadAsync(),
                answeringSlot);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "UDP {LocalPort}: the room's loadouts could not be read, so the roster answer to "
                + "{RemoteAddress} closes on the room's count without the peer's settings payload",
                context.LocalPort,
                context.Remote);
        }

        var run = roster.BuildRosterRun(
            answeringLoadout,
            includeEndpointPairs: context.Session.PeerHandshakeShape != HandshakeShape.Native);

        logger.LogDebug(
            "UDP {LocalPort}: built roster response for {RemoteAddress}: {RecordCount} records, "
            + "{JoinerCount} joiners, one outbound roster frame",
            context.LocalPort,
            context.Remote,
            run.Count,
            roster.JoinerCount);

        logger.LogDebug(
            "UDP {LocalPort}: sending roster response ({RecordCount} records), closing on "
            + "the answered peer's settings payload for slot byte {SlotByte}",
            context.LocalPort,
            run.Count,
            answeringSlot);
        await RosterRunSendUtils.SendAsync(context, run);

        // The recorded host does not stop at the roster. 2.52 s later it writes
        // the one burst of the round - the room's rotation, the joiner's loadout,
        // the two 54-byte second loadouts and a state blob - and then keeps the
        // channel moving with the match records under it, which is what a client
        // that has been answered the roster waits on. Both are scheduled here, in
        // the pass that answered the roster, and neither is awaited: the pause is
        // longer than the dispatch loop should be held.
        logger.LogDebug(
            "UDP {LocalPort}: scheduling settings burst and match records for {RemoteAddress}",
            context.LocalPort,
            context.Remote);
        burstScheduler.Schedule(context);

        // The peer that has just been answered the roster is in the room's
        // briefing, and the room's next round will open for it too. Its watcher
        // is armed here, in the pass that answered the roster, so the round's
        // opening run reaches this peer whether it readies first or last — and
        // armed before the match records, because that call begins the briefing's
        // writers with it: the clock a peer is sent counts against the briefing
        // this pass begins, and a writer that started first would find no briefing
        // armed and write the peer nothing.
        matchStart.Start(context);
        matchRecords.Start(context);

        logger.LogDebug(
            "UDP {LocalPort}: announcing {RecordCount} roster records from {RemoteAddress} to other peers",
            context.LocalPort,
            context.Remote,
            run.Count);
        foreach (var record in run)
        {
            await context.Broadcast(record.Type, record.Body, record.Ordinal);
        }
    }
}