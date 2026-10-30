using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Writes the room itself to a peer for as long as that peer is in a briefing:
/// the rules-roster pair, which the room's stream carries in every phase, the
/// lobby-slot array pair, which is the room's membership and the record a joining
/// client answers, and the room's own count, which is written when a peer is
/// answered and again whenever the room's population moves.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this closes.</b> The beat writes the rules pair once a round is
/// playing, and a briefing is not a round: a peer waiting to play was sent the
/// recorded host's sparse controls and state blobs and the room's ready set, and
/// nothing at all on the stream the room's own membership travels under. The
/// recorded host does not stop: <c>mgo2-game-started-on-briefing</c> writes
/// <c>0x0261 fe</c> under both classes 12 to 26 times per ten seconds from its
/// roster answer through its 198 s briefing and on into its round, and the two
/// captures that hold a join write it in the roster run itself
/// (<see cref="MatchRecordFramesUtils.RulesPair"/>).
/// </para>
/// <para>
/// <b>It runs for the whole briefing, and the peer's run does not end it.</b> The
/// pair is the one record every tick of a briefing carries: over the ten reference
/// captures it is written at every one of their 78 and 55 ticks, while the state
/// block, the settings headers and the close triple arrive two or three times a
/// capture and the <c>0xd001</c> controls and <c>0x0002</c> blobs ride among them
/// (`docs/analysis/join-stall-after-mode2.md` §30.2). Nothing of the room's stops
/// when the run is written, so ending the publication there left the briefing with
/// the countdown alone — 0.4 frames a second against the recording's 1.3 — which
/// is the gap `docs/analysis/join-stall-after-mode2.md` §25 records.
/// </para>
/// <para>
/// <b>The count is the room's and not the host's.</b> Every captured one carries
/// the same number the Lobby's own details state for that room — one player of a
/// limit of seventeen in <c>started-on-briefing</c>, five joiners in
/// <c>joined-when-red-team</c> — and it is written again when that number moves
/// and not on every tick, which is why a peer that joins into an unchanged room
/// sees it once (<see cref="MatchRecordBodiesUtils.RoomCount"/>).
/// </para>
/// <para>
/// <b>It does not fault.</b> It runs beside the waits for the round rather than
/// inside them, so a failed send says what ending it says anyway: the peer is
/// gone.
/// </para>
/// </remarks>
/// <param name="roster">The room's roster, whose count is what the count record carries.</param>
/// <param name="roundState">The room's round, whose opening ends the publication.</param>
/// <param name="sequences">
/// The stream counters a session record's fourth byte comes from: the room's
/// count is one of those and takes its own stream's next value.
/// </param>
/// <param name="lobbySlots">
/// The room's players, by the key each holds in its 24-entry player table: every
/// player is announced in a state blob of its own.
/// </param>
/// <param name="matchStart">
/// The run the briefing writes, whose due instant is the pair's first copy: a peer
/// that has not entered the room has no pair owed to it.
/// </param>
/// <param name="logger">Logger of this service.</param>
public sealed partial class BriefingRoomPublishService(
    RoomRosterService roster,
    MatchRoundStateService roundState,
    MatchRecordSequenceService sequences,
    LobbySlotService lobbySlots,
    MatchStartRunService matchStart,
    ILogger<BriefingRoomPublishService> logger)
{
    /// <summary>
    /// The room's frame through a briefing: one second, at which the rules pair is
    /// written twice — 1.8 records a second, against the reference's 1.9.
    /// </summary>
    /// <remarks>
    /// <b>The pair is written more often than the room's tick, and the captures
    /// say how often.</b> Over the quiet middle of `mgo2-game-started-on-briefing`
    /// — t+70 to t+110, no report and no join in it — the host writes 76 rules
    /// records in forty seconds, **1.9 a second**, in **1.43 datagrams a second**;
    /// one pass of this loop carries two records in one datagram, so a second
    /// between passes is 1.8 records and 1 datagram a second. The countdown and
    /// the ready set keep the room's own 2.35 s tick
    /// (<see cref="MatchRoundStateService.BriefingTickMilliseconds"/>); what moves
    /// here is the pair's rate. During a round the pair is a beat frame's and goes
    /// every ~0.07 s, which the beat already writes
    /// (<see cref="MatchRecordFramesUtils.BeatFrame"/>).
    /// </remarks>
    public const int PublishPeriodMilliseconds = 1000;

    /// <summary>
    /// Frames of <see cref="PublishPeriodMilliseconds"/> between two state blobs:
    /// nine, which is 9.0 s — the medians the reference briefings keep are 9.0,
    /// 9.7 and 9.7 s, and the quiet window of `mgo2-game-started-on-briefing`
    /// writes five blobs in forty seconds, 8.7 s apart.
    /// </summary>
    /// <remarks>
    /// <b>The blob does not stop where the sparse phase does.</b> Our sparse
    /// phase is a replay of the recorded host's own draw, so it ends at its last
    /// blob — 42.2 s after the roster answer — and this host wrote no
    /// <c>0x0002</c> record after it, while every reference keeps writing one for
    /// as long as the room waits. `mgo2-game-started-on-briefing` writes one from
    /// t+6.0 through its 198 s briefing and on into its round, 3.9 to 11.3 s
    /// apart, and `mgo2-started-briefing-then-solo-game` one every 5 to 9 s from
    /// t+9.0 to the round it opens
    /// (`docs/analysis/join-stall-after-mode2.md` §28).
    /// </remarks>
    public const int StateBlobFrameInterval = 9;

    /// <summary>
    /// Frames of <see cref="PublishPeriodMilliseconds"/> between two lobby-slot
    /// pairs: thirteen, which is one pair every 13 s — nine 26-byte records a
    /// minute, the rate the quiet reference windows keep.
    /// </summary>
    /// <remarks>
    /// <b>The pair is not a tick record.</b> The reference writes it when the room's
    /// players move — one pair, sometimes two copies in one instant, at a join —
    /// and not on the room's clock: the same forty seconds hold six records, all of
    /// them in the burst a player's join drew at t+86.0, and `mgo2-game2` holds six
    /// in the thirty seconds after its report. It is periodic here because this
    /// host has no join-time hook to hang it on and a peer that echoes the pair has
    /// to be answered at its sequence whatever moved; thirteen frames is that
    /// burst's average brought onto the frame loop.
    /// </remarks>
    public const int ArrayPairFrameInterval = 13;

    /// <summary>Sent count of a peer nothing has been told yet.</summary>
    private const int NoCountSent = -1;

    /// <summary>
    /// Writes the room to one peer, on the briefing's own one-second frame, until
    /// the round opens or the peer goes.
    /// </summary>
    /// <remarks>
    /// <b>The run does not end the pair, and the round does.</b> The peer's own run
    /// is written two to three seconds after its first report, and the reference
    /// keeps the pair on the same tick from there to its round —
    /// `mgo2-game-started-on-briefing` t+68.0 to t+92.0 is pairs and nothing else of
    /// the room's — so a publication that stopped at the run left the window the
    /// briefing's run opens with the countdown alone. The round opens the pair's
    /// own generation and the beat writes it from there.
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    public async Task PublishAsync(PeerContext context)
    {
        var session = context.Session;
        var sentCount = NoCountSent;

        // Counted from the same instant the sparse phase is, because the two are
        // started together and the blob's own tick is read off the phase's end
        // (StateBlobFrameInterval): the phase's steps are the recorded host's, and
        // a blob written among them on this counter would repeat ones the
        // recording already carries.
        var started = DateTimeOffset.UtcNow;
        var frames = 0;

        // The pair's own counter, started where the pair is due: the first copy is
        // written on the pass the peer's run is due on and every
        // ArrayPairFrameInterval passes after it.
        var pairFrames = ArrayPairFrameInterval;

        while (MatchRecordPeerUtils.IsAlive(session) && !roundState.IsOpen)
        {
            try
            {
                // The rules pair goes out on the first pass as well and not only
                // on a tick: the captures write it in the roster run itself, so a
                // peer answered its roster is told the room's stream at once.
                foreach (var record in MatchRecordFramesUtils.RulesPair(roundState.Generation))
                {
                    await context.Send(record.Type, record.Body, record.FourthByte);
                }

                // The pair waits for the peer's own entry and the run's dwell
                // behind it, which is the instant every reference host writes its
                // first copy at: the copy is the run's own opening pair there and
                // none of the four working joins holds one before it.
                if (matchStart.IsEntryAnswerDue(session)
                    && pairFrames >= ArrayPairFrameInterval)
                {
                    await PublishArraysAsync(context);
                    pairFrames = 0;
                }

                pairFrames++;

                await PublishStateBlobAsync(context, started, frames);
                sentCount = await PublishCountAsync(context, sentCount);
                frames++;
            }
            catch (Exception exception)
            {
                logger.LogDebug(
                    exception,
                    "UDP {LocalPort}: the room's rules and count to {RemoteAddress} stopped on a failed send",
                    context.LocalPort,
                    session.RemoteAddress);
                return;
            }

            // The frame ends the moment the round opens: this task runs beside the
            // waits for the round, and a period slept out after the round opened
            // would hold nothing of the beat — but it would keep writing the
            // briefing's set over a round that is playing.
            await roundState.WaitBriefingFrameAsync(session, PublishPeriodMilliseconds);
        }

        logger.LogDebug(
            "UDP {LocalPort}: the room's rules and count to {RemoteAddress} ended",
            context.LocalPort,
            session.RemoteAddress);
    }

    /// <summary>
    /// Writes the room's lobby-slot array pair — the record a joining client
    /// answers on its own clock.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The pair is published through the briefing and it is the one record the
    /// client asks for by name.</b> A client that has been placed in the room
    /// echoes the sequence it holds as <c>82 NN</c>/<c>84 NN</c>, and the host
    /// answers with the pair at that same sequence
    /// (<see cref="TeamArrayService.ConfirmAsync"/>). The reference writes the
    /// pair all through a briefing and not only beside the run: over the forty
    /// seconds of `mgo2-game-started-on-briefing` that follow its run, t+60 to
    /// t+100, it writes twelve 26-byte array records, while the run carries two
    /// (<c>docs/analysis/join-stall-after-mode2.md</c> §33). This host wrote none
    /// of them in a briefing at all, so `myserver29`'s client echoed
    /// <c>82 f3</c>/<c>84 f3</c> eleven times over thirty-five seconds and was
    /// answered nothing, while the room's rules pair, its blobs and its count went
    /// out on the tick beside it.
    /// </para>
    /// <para>
    /// <b>Its first copy is written after the peer's own entry and never before it.</b>
    /// Every reference host writes the pair for the first time inside its run, 0.9
    /// to 5.5 s after that client's report: <c>started-on-briefing</c>
    /// t+58.255, <c>started-briefing-then-solo-game</c> t+57.626, <c>game2</c>
    /// t+62.181 and <c>failed-memoryviolation</c> t+60.871, and none of the four
    /// writes one in the twenty seconds before it. A pair published from the join
    /// instead reaches a client that has not loaded its map, and it answers it:
    /// <c>myserver32</c>'s client echoed <c>82 f3</c>/<c>84 f3</c> at t+52.6 and
    /// reported its map at t+57.2, the reverse of the 0.4 and 0.5 s by which both
    /// working references report first and enter second.
    /// </para>
    /// <para>
    /// <b>And the briefing's own report is not what it waits for.</b> Waiting for
    /// the clock to arm started the pair at the report, which in <c>myserver33</c>
    /// was 5.4 s before the peer's entry: the host wrote <c>85</c>/<c>86</c> at
    /// t+58.6, the client echoed <c>82 f3</c>/<c>84 f3</c> at t+64.0 and nothing
    /// was written between, so the first copy of the room's membership reached a
    /// peer that had not been placed in the room and started a sequence of its own
    /// ahead of the run. The pair is owed from the entry and the run's dwell, which
    /// is <see cref="MatchStartRunService.IsEntryAnswerDue"/> and what the reference
    /// writes it inside.
    /// </para>
    /// <para>
    /// <b>It is published at the room's constant sequence and never confirmed
    /// here.</b> <see cref="TeamArrayFramesUtils.OpeningSequence"/> is the room's
    /// sequence and the one its client echoes, so a pair published under it is the
    /// answer to every echo the room draws; the confirmation belongs to the
    /// round's open, which is where the captures put it
    /// (<see cref="MatchStartRunService.ConfirmEntryAsync"/>). A confirmation
    /// beside the echo is what stopped `myserver20`'s client where it stood.
    /// </para>
    /// <para>
    /// <b>What it says is the room's, read per publication.</b> The peer that
    /// joins during a briefing must be on the arrays the room publishes: the pair
    /// is the room's occupancy and sides by player-table key, so a copy carried
    /// over from the join would name a room that has since changed.
    /// </para>
    /// </remarks>
    /// <param name="context">Context the pair is written through.</param>
    private async Task PublishArraysAsync(PeerContext context)
    {
        var (teams, occupancy) = TeamArrayFramesUtils.BuildPair(
            TeamArrayFramesUtils.OpeningSequence,
            await lobbySlots.ReadAsync());
        var ordinals = roster.TakeRunOrdinals(2);

        await RosterRunSendUtils.SendAsync(
            context,
            [
                // Both under the roster's own identifier, which is how the port
                // client's own briefings are written (`mgo2-started-on-game-round1`,
                // every 26-byte array it publishes): the joiner's 0x9001 opening tag
                // is not an identifier the host writes.
                new(UdpCommandConstants.PlayerProfile, teams, ordinals[0]),
                new(UdpCommandConstants.PlayerProfile, occupancy, ordinals[1]),
            ],
            compressed: true);
    }

    /// <summary>
    /// Writes the room's count when it has moved since this peer was last told,
    /// and returns the count the peer now holds.
    /// </summary>
    /// <remarks>
    /// The room's player table is 24 keys wide, so a room's count is one byte with
    /// room to spare (<see cref="RoomRosterService.SessionSlotCount"/>).
    /// </remarks>
    /// <param name="context">Context the record is written through.</param>
    /// <param name="sentCount">Count this peer was last told, or <see cref="NoCountSent"/>.</param>
    /// <returns>The count the peer has now been told.</returns>
    private async Task<int> PublishCountAsync(PeerContext context, int sentCount)
    {
        var count = roster.JoinerCount;
        if (count == sentCount)
        {
            return sentCount;
        }

        var generation = roundState.Generation;
        logger.LogDebug(
            "UDP {LocalPort}: telling {RemoteAddress} the room holds {PlayerCount} player(s) on 0x{Identifier:x4}",
            context.LocalPort,
            context.Remote,
            count,
            generation.RoomCount);

        await context.Send(
            generation.RoomCount,
            MatchRecordBodiesUtils.RoomCount((byte)count),
            sequences.Take(generation.RoomCount));
        return count;
    }
}
