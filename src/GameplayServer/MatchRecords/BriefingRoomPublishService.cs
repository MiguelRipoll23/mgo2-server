using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Writes the room itself to a peer for as long as that peer is in a briefing:
/// the rules-roster pair, which the room's stream carries in every phase, and the
/// room's own count, which is written when a peer is answered and again whenever
/// the room's population moves.
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
/// <b>It runs for the whole briefing, and the peer's run does not end it.</b> Measured
/// again over the window behind that run — `mgo2-game-started-on-briefing`
/// t+68.0 to t+92.0, twenty-four seconds between the briefing's run and the
/// request it answers — the pair is the stream's every record that matters: 22
/// pairs at nine instants 1.9 to 4.4 s apart, with the <c>0xd001</c> controls
/// and <c>0x0002</c> blobs riding among them. Nothing of the room's stops
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
/// <param name="logger">Logger of this service.</param>
public sealed class BriefingRoomPublishService(
    RoomRosterService roster,
    MatchRoundStateService roundState,
    MatchRecordSequenceService sequences,
    LobbySlotService lobbySlots,
    ILogger<BriefingRoomPublishService> logger)
{
    /// <summary>
    /// Spacing between rules pairs, measured over the reference briefings rather
    /// than taken from the ready set's tick: 2.25 s, the median gap the pair is
    /// written at once a room is briefing.
    /// </summary>
    /// <remarks>
    /// <b>The pair is the one record every tick carries, and its gap is ~2.25 s.</b>
    /// Counted over the ten reference captures, a briefing's gaps between two pair
    /// instants have a median of 2.13 to 2.38 s; the body of the distribution sits
    /// between 1.5 and 2.6 s, the 4.4 to 5.1 s gaps are a missed tick and the
    /// 0.05 to 0.3 s ones a second write in the same pass. A briefing's tick is
    /// therefore ~2.25 s and not the 2.5 s this used to share with the countdown
    /// and the ready set, whose own streams measure differently (`0x1254`'s
    /// seven-byte countdown appears at 2.8 ticks there, `0x9254`'s clock at 1.3).
    /// During a round the pair is a beat frame's instead and goes every ~0.07 s,
    /// which the beat already writes (`MatchRecordFramesUtils.Frame`).
    /// </remarks>
    public const int PublishPeriodMilliseconds = 2250;

    /// <summary>
    /// Ticks of <see cref="PublishPeriodMilliseconds"/> between two state blobs:
    /// four, which is 9 s — the medians the reference briefings keep are 9.0, 9.7
    /// and 9.7 s.
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
    public const int StateBlobTickInterval = 4;

    /// <summary>Sent count of a peer nothing has been told yet.</summary>
    private const int NoCountSent = -1;

    /// <summary>
    /// Writes the room to one peer, on the briefing's tick, until the round opens
    /// or the peer goes.
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
        // (StateBlobTickInterval): the phase's steps are the recorded host's, and
        // a blob written among them on this counter would repeat ones the
        // recording already carries.
        var started = DateTimeOffset.UtcNow;
        var ticks = 0;

        while (MatchRecordPeerUtils.IsAlive(session) && !roundState.IsOpen)
        {
            try
            {
                // The pair goes out on the first pass as well and not only on a
                // tick: the captures write it in the roster run itself, so a peer
                // answered its roster is told the room's stream at once.
                foreach (var record in MatchRecordFramesUtils.RulesPair(roundState.Generation))
                {
                    await context.Send(record.Type, record.Body, record.FourthByte);
                }

                await PublishStateBlobAsync(context, started, ticks);
                sentCount = await PublishCountAsync(context, sentCount);
                ticks++;
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

            // The tick ends the moment the round opens: this task is awaited
            // before the beat, and a period slept out after the run was written
            // would hold the beat's first frame behind it.
            await roundState.WaitBriefingTickAsync(session, PublishPeriodMilliseconds);
        }

        logger.LogDebug(
            "UDP {LocalPort}: the room's rules and count to {RemoteAddress} ended",
            context.LocalPort,
            session.RemoteAddress);
    }

    /// <summary>Slot key the host's own player holds in the room's player table.</summary>
    private const byte HostOwnSlot = 0;

    /// <summary>
    /// Writes one state blob per player of the room on its own ten-second tick,
    /// once the sparse phase has written its last one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is one record per player and not one record for the room.</b> The
    /// identifier never travels alone in a room of more than one: every captured
    /// <c>0x0002</c> carries the slot byte the loadout records carry — the player's
    /// key plus one — and a capture's records for the room's players repeat on
    /// that tick. `mgo2-game1` writes seven of them, for classes `01`, `05`, `07`,
    /// `0c`, `10` and `15`, and its five joiners hold keys `04`, `06`, `0b`, `0f`
    /// and `14`; `mgo2-game-started-on-briefing` writes one for each of the six
    /// players it briefs. The class is therefore the player's own and the value is
    /// that player's own, which is what this writes.
    /// </para>
    /// <para>
    /// <b>The record is a one-field state transfer, and the class is its state.</b>
    /// The class byte is a slot state's index — one of the session's 24 slot states
    /// — and the body behind it is a 21-byte presence map plus the one field bit 150
    /// names, a single byte (`MatchStateBodiesUtils.StateBlobBody`);
    /// `docs/analysis/join-stall-after-mode2.md` §28.7 carries the parse and the
    /// client's handler that reads it.
    /// </para>
    /// <para>
    /// <b>This host measures nothing, so it writes the only two values it can name.</b>
    /// The field is the host's figure for that player: a peer is named with 50 the
    /// first time and sits at its own measured level afterwards (`mgo2-game1`'s
    /// class `0c` reads 50, 55, 73, 83, 77, 78, 83, 78, 73, 85), and the host's own
    /// slot reads 100 in every capture that holds it. 50 is the pre-measurement value
    /// — every peer in seven of the ten captures is named with it first, and it is
    /// the value the join-time burst carries — so it is what a peer's slot takes
    /// here, where 100 would be a measurement this host has not made; the host's own
    /// slot takes 100.
    /// A host that later reports pings over TCP (`UpdatePingsHandler`) has the
    /// measurement and can write it here instead.
    /// </para>
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="started">When this publication started, beside the sparse phase.</param>
    /// <param name="ticks">Passes this publication has made.</param>
    private async Task PublishStateBlobAsync(
        PeerContext context,
        DateTimeOffset started,
        int ticks)
    {
        if (ticks % StateBlobTickInterval != 0)
        {
            return;
        }

        // The sparse phase writes the recording's own blobs, so nothing here goes
        // out until it has closed: a blob beside a recorded one would be a second
        // record the capture does not carry.
        if (DateTimeOffset.UtcNow - started
            < TimeSpan.FromMilliseconds(SparsePhaseFramesUtils.SparsePhaseMilliseconds))
        {
            return;
        }

        foreach (var slot in await lobbySlots.ReadAsync())
        {
            var value = slot.Slot == HostOwnSlot
                ? MatchStateBodiesUtils.MeasuredStateBlobValue
                : MatchStateBodiesUtils.UnmeasuredStateBlobValue;
            await context.Send(
                UdpCommandConstants.GameStateBlob,
                MatchStateBodiesUtils.StateBlobBody(value),
                slot.Slot >= byte.MaxValue ? byte.MaxValue : (byte)(slot.Slot + 1));
        }
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
