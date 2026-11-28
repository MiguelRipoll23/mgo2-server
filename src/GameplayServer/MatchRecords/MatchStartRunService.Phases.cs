using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The room's own state record — state <c>0x00</c>'s field <c>0</c>, written as
/// the briefing ends and at the round's open and re-stated through the round —
/// and the round's clock record, re-stated through the round and its screens.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the field a joining client waits on.</b> The client's join machine
/// reads field 0 and treats <c>0</c> and <c>7</c> as "wait and ask again"; any
/// other value makes it write its lobby-slot echo and move on
/// (<see cref="Rooms.RoundStateBodiesUtils.RoomStateBody"/>). A host that never
/// writes it leaves the peer holding whatever its last room put there, and a
/// state transfer that is written once and never again is one a lost datagram
/// takes with it.
/// </para>
/// <para>
/// <b>It rides the briefing's own tick, because the reference does.</b> The
/// corpus' gaps between two copies of the record cluster on 2.35 s —
/// <c>joined-final-round…</c> 2.376 and 2.374, <c>failed-memoryviolation</c>
/// 2.352, <c>joined-game-in-progress…</c> 2.375, <c>red-team</c> 2.374 — which is
/// <see cref="MatchRoundStateService.BriefingTickMilliseconds"/>, the period the
/// countdown, the ready set and the room's rules pair already share. The zero
/// gaps in the same captures are the copies several peers took at one instant,
/// and the slower gaps (4.9 s, 10 s, 40 s) are the stretches where no round was
/// playing. <c>started-briefing-then-solo-game</c> also holds a run of 1.87 s
/// gaps through its round, so the reference is not perfectly regular; the tick
/// is the period it keeps most often.
/// </para>
/// <para>
/// <b>A failed send ends nothing.</b> The record is a courtesy to a peer's join
/// machine: one that cannot be delivered says the peer is gone, which the
/// watcher's next wait already handles, so it is logged and the tick goes on.
/// </para>
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// How often the room's phase is re-written through a round: the briefing's
    /// tick, which is the period the corpus keeps most often.
    /// </summary>
    public const int PhasePeriodMilliseconds = MatchRoundStateService.BriefingTickMilliseconds;

    /// <summary>
    /// Writes one peer the room's phase record every tick of the round it is
    /// playing, and returns when the round closes or the peer goes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It runs beside the watcher's wait for the close rather than inside it, the
    /// way the countdown runs beside the beat, so the wait is not held behind a
    /// tick. The round is named rather than read each time: a room that has moved
    /// to the next round has its own writer, and this one stopping is what keeps
    /// two of them from numbering one round twice.
    /// </para>
    /// <para>
    /// <b>It stops at the close, and the clock takes the screens.</b> The
    /// reference writes no phase record at a round's boundary —
    /// <c>rounded-ended-by-reaching-tickets-limit.pcapng</c> writes exactly one
    /// record at t+65.218, the result — and what holds its screens is the stopped
    /// clock record (<see cref="PublishRoundClockAsync"/>).
    /// </para>
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="round">The round the tick belongs to.</param>
    private async Task PublishRoundPhaseAsync(PeerContext context, int round)
    {
        // The peer's own round state outlives this round and is started once per
        // peer, not once per round (MatchStartRunService.PeerRoundState).
        StartPeerRoundState(context);

        while (roundState.RoundNumber == round && roundState.IsOpen)
        {
            await Task.Delay(PhasePeriodMilliseconds);

            if (roundState.RoundNumber != round || !roundState.IsOpen)
            {
                return;
            }

            if (!MatchRecordPeerUtils.IsAlive(context.Session))
            {
                logger.LogDebug(
                    "UDP {LocalPort}: the room's phase tick to {RemoteAddress} stopped because the peer is idle",
                    context.LocalPort,
                    context.Remote);
                return;
            }

            await WriteRoomStateAsync(context);
        }
    }

    /// <summary>
    /// Re-states the round's clock every
    /// <see cref="MatchRecordTimingUtils.ClockPeriodMilliseconds"/>, through the
    /// round and through the result screens that follow it, and returns when the
    /// room has moved past them or the peer goes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the record the screens are held on.</b> The reference writes the
    /// round's clock — <c>01</c> and a little-endian count of 125 ms units —
    /// every four seconds while the round plays and keeps writing it, unchanged,
    /// after the round closes: <c>rounded-ended-by-reaching-tickets-limit.pcapng</c>
    /// writes <c>01 c7 07</c> six times from t+65.497 to t+85.495, the clock its
    /// round stopped at, and then the next round's <c>01 60 09</c> at t+88.163.
    /// A client held on a result screen with no clock record is a client whose
    /// screen has nothing saying the room is still there.
    /// </para>
    /// <para>
    /// <b>The value is the room's clock and not a phase.</b> While the round plays
    /// it is <see cref="MatchRoundStateService.RemainingUnits"/>; through the
    /// screens it is the stopped value
    /// (<see cref="MatchRoundStateService.ScreensClockUnits"/>), so the last four
    /// seconds before the next round's clock are the same bytes as the ones before
    /// them.
    /// </para>
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="round">The round the clock belongs to.</param>
    private async Task PublishRoundClockAsync(PeerContext context, int round)
    {
        while (roundState.RoundNumber == round && HoldsClockTick())
        {
            await Task.Delay(MatchRecordTimingUtils.ClockPeriodMilliseconds);

            if (roundState.RoundNumber != round || !HoldsClockTick())
            {
                return;
            }

            if (!MatchRecordPeerUtils.IsAlive(context.Session))
            {
                logger.LogDebug(
                    "UDP {LocalPort}: the round clock to {RemoteAddress} stopped because the peer is idle",
                    context.LocalPort,
                    context.Remote);
                return;
            }

            await WriteClockAsync(
                context,
                roundState.IsOpen ? roundState.RemainingUnits : roundState.ScreensClockUnits);
        }
    }

    /// <summary>
    /// Whether the room still holds the round the clock belongs to: playing it,
    /// or sitting on the result screens that follow it.
    /// </summary>
    /// <returns><c>true</c> while the round's clock is still the room's.</returns>
    private bool HoldsClockTick() =>
        roundState.IsOpen || roundState.IsRoundEnding || roundState.IsStageEnding;

    /// <summary>
    /// Writes one peer the round's clock record on the round's own stream.
    /// </summary>
    /// <param name="context">Context the record is written through.</param>
    /// <param name="units">Units of <see cref="MatchRecordBodiesUtils.ClockUnitMilliseconds"/> the clock holds.</param>
    private async Task WriteClockAsync(PeerContext context, int units)
    {
        try
        {
            var generation = roundState.Generation;
            await MatchRecordSendUtils.SendAsync(
                context,
                [new(
                    generation.Operations,
                    MatchRecordBodiesUtils.RoundTimeLimitUnits(units),
                    sequences.Take(generation.Operations))]);
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the round clock to {RemoteAddress} was not sent",
                context.LocalPort,
                context.Remote);
        }
    }

    /// <summary>
    /// Writes one peer the round's opening state write: the first record of the
    /// round's operations stream, a second and a half ahead of its run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured at the open, and written by no briefing.</b>
    /// <c>briefing-then-solo-game</c> writes <c>0x1a54 02 2a 04 1e 1e 1e 1e</c> at
    /// t+146.007 — 1.62 s before its run at t+147.650, in the same pass as the two
    /// ten-byte <c>0b 00 01</c> records — and under the stream's first value,
    /// <c>00</c>. The run behind it then takes <c>01</c>, <c>02</c> and <c>03</c>,
    /// where the same host's briefing run takes <c>00</c> and <c>01</c> and writes
    /// no state write at all, and the beat goes on from the value after the run.
    /// </para>
    /// <para>
    /// <b>The record is not in every room's open, and the numbering is the
    /// invariant that holds either way.</b> The twelve captures in
    /// <c>.pcapngs/</c> open their rounds with the block at <c>00</c> and no
    /// <c>02</c> record in front of it (<c>started-briefing-then-solo</c>
    /// t+174.371, <c>started-on-briefing</c> t+198.371, <c>game1</c> t+194.042,
    /// <c>game3</c> t+59.596); <c>mgo2-game2</c> writes one of its own at the
    /// stream's first value too (<c>02 3f 18 3c …</c>, t+104.649). What every
    /// capture agrees on is that the open's records number consecutively from the
    /// stream's restart, so a host that writes this one must let the block follow
    /// it — which is what the shared counter does here, rather than a pinned
    /// value.
    /// </para>
    /// <para>
    /// <b>The four fields are the unclaimed holders and clocks.</b> The offset
    /// counts from the state base two bytes before the block's own vector — the
    /// port's builder reads <c>[this+0x84] + offset</c> while the <c>0x00</c>
    /// builder reads its vector from <c>[this+0x84] + 2</c>, and the captures
    /// agree (a write at <c>0x28</c> moved exactly <c>state[38..39]</c> of the next
    /// block in <c>failed-memoryviolation</c>). So <c>0x2a</c> is
    /// <c>state[40]</c>, and the four bytes are the two objective holders and the
    /// two capture clocks, holding
    /// <see cref="MatchStateBodiesUtils.StateUnclaimed"/> — the value this host's
    /// own block carries there.
    /// </para>
    /// <para>
    /// <b>A failed send ends nothing.</b> The record is the open's, and a peer
    /// that cannot be reached has no round to be told about, which the watcher's
    /// next wait already handles — so it is logged and the run goes on.
    /// </para>
    /// </remarks>
    /// <param name="context">Context the record is written through.</param>
    /// <param name="generation">Identifier set the round plays under.</param>
    /// <param name="round">The round being opened, whose clock the block carries.</param>
    private async Task WriteRoundStartStateAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation,
        MatchRotationEntry round)
    {
        try
        {
            // The value is taken here, where the record is built: the image's builders
            // stamp the fourth byte into the record and the send path only carries it
            // (docs/GHIDRA.md).
            await MatchRecordSendUtils.SendAsync(
                context,
                [new(
                    generation.Operations,
                    MatchStateBodiesUtils.RoundStartStateWrite(),
                    sequences.Take(generation.Operations))]);
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the round's opening state write to {RemoteAddress} was not sent",
                context.LocalPort,
                context.Remote);
        }
    }

    /// <summary>
    /// Writes one peer the two state-0 transfers a round's close leaves behind it.
    /// </summary>
    /// <remarks>
    /// <b>They follow the result write and are the same boundary's state.</b> The
    /// first clears field 19, the round's own state, which the capture writes with
    /// its round-over records (t+74.067, t+163.096); the second states field 0 at
    /// <c>6</c> — the result screen's phase — and field 20 at the round, beside the
    /// result write (t+74.571, t+163.597)
    /// (<see cref="Rooms.RoundStateBodiesUtils.RoundEndTransfers"/>).
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="roundNumber">The round that closed, which field 20 carries.</param>
    private async Task WriteRoundEndTransfersAsync(PeerContext context, int roundNumber)
    {
        foreach (var body in Rooms.RoundStateBodiesUtils.RoundEndTransfers(roundNumber))
        {
            try
            {
                await context.Send(
                    UdpCommandConstants.RoomRecord,
                    body,
                    roster.TakeRunOrdinal());
            }
            catch (Exception exception)
            {
                logger.LogDebug(
                    exception,
                    "UDP {LocalPort}: a round-close state transfer to {RemoteAddress} was not sent",
                    context.LocalPort,
                    context.Remote);
                return;
            }
        }
    }

    /// <summary>
    /// Writes one peer the two state-0 transfers the reference puts in front of a
    /// round's state record.
    /// </summary>
    /// <remarks>
    /// <b>They are the open's, and they are what a round's state record follows.</b>
    /// `rounded-ended-by-reaching-tickets-limit` writes them at t+96.112 and
    /// t+96.482, the pair the client answers at t+96.497 and the room's own state
    /// record in the same frame as the second; before this host wrote them the
    /// frame carried the room state alone
    /// (<see cref="Rooms.RoundStateBodiesUtils.RoundOpenTransfers"/>).
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="roundNumber">The round the open is writing, which field 51 carries.</param>
    private async Task WriteRoundOpenTransfersAsync(PeerContext context, int roundNumber)
    {
        foreach (var body in Rooms.RoundStateBodiesUtils.RoundOpenTransfers(roundNumber))
        {
            try
            {
                await context.Send(
                    UdpCommandConstants.RoomRecord,
                    body,
                    roster.TakeRunOrdinal());
            }
            catch (Exception exception)
            {
                logger.LogDebug(
                    exception,
                    "UDP {LocalPort}: a round-open state transfer to {RemoteAddress} was not sent",
                    context.LocalPort,
                    context.Remote);
                return;
            }
        }
    }

    /// <summary>
    /// Writes one peer the room's state record, read from the room's roster and
    /// round.
    /// </summary>
    /// <remarks>
    /// <b>The roster is noted here as well as read.</b> A second player joining a
    /// round of one moves the room off <see cref="Rooms.RoomState.RoundStartSolo"/>,
    /// and this is the writer that hears it while the round plays — the held clock
    /// is given back at that instant (<see cref="MatchRoundStateService.NoteSolo"/>).
    /// </remarks>
    /// <param name="context">Context the record is written through.</param>
    private async Task WriteRoomStateAsync(PeerContext context)
    {
        roundState.NoteSolo(roster.IsSolo);

        // **The screens between two rounds are held on the stopped clock and on no
        // room state at all.** `rounded-ended-by-reaching-tickets-limit` writes no
        // field 0 at its boundary: its result transfer states the result once
        // (`6`, t+74.571) and the screens behind it carry the round's stopped clock
        // every four seconds and nothing else until the next round's open writes
        // `1` (t+96.482). Field 0 at `1` is a transition and not a phase — no
        // capture holds a room on it for any stretch — so a phase tick that
        // re-stated it through the whole hold put the client on a value it waits to
        // leave, which is the fade it sat in through this room's screens (live
        // session of 2026-11-28 00:42:40). The open's own write is beside this one
        // and is unaffected: it states `1` once, in the frame that releases the
        // client from its hold.
        if (roundState.IsRoundEnding || roundState.IsStageEnding)
        {
            return;
        }

        await WriteRoomStateAsync(context, CurrentRoomState);
    }

    /// <summary>
    /// Writes one peer the room's state record on the round's stream.
    /// </summary>
    /// <param name="context">Context the record is written through.</param>
    /// <param name="state">The room state to state.</param>
    private async Task WriteRoomStateAsync(PeerContext context, Rooms.RoomState state)
    {
        try
        {
            await context.Send(
                UdpCommandConstants.RoomRecord,
                Rooms.RoundStateBodiesUtils.RoomStateBody(state),
                roster.TakeRunOrdinal());
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the room's phase record to {RemoteAddress} was not sent",
                context.LocalPort,
                context.Remote);
        }
    }
}
