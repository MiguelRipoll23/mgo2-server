using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The room's own state record: state <c>0x00</c>'s field <c>0</c>, written as the
/// briefing ends, at the round's open, on every tick of the round, and at its
/// close.
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
    /// It runs beside the watcher's wait for the close rather than inside it, the
    /// way the countdown runs beside the beat, so the wait is not held behind a
    /// tick. The round is named rather than read each time: a room that has moved
    /// to the next round has its own writer, and this one stopping is what keeps
    /// two of them from numbering one round twice.
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="round">The round the tick belongs to.</param>
    private async Task PublishRoundPhaseAsync(PeerContext context, int round)
    {
        while (roundState.IsOpen && roundState.RoundNumber == round)
        {
            await Task.Delay(PhasePeriodMilliseconds);

            if (!roundState.IsOpen || roundState.RoundNumber != round)
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
            await context.Send(
                generation.Operations,
                MatchStateBodiesUtils.RoundStartStateWrite(),
                sequences.Take(generation.Operations, 1)[0]);
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
                UdpCommandConstants.PlayerProfile,
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
