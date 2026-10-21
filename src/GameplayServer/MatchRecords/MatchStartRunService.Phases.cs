using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The room's own phase record: state <c>0x00</c>'s field <c>0</c>, written at
/// the round's open, on every tick of the round, and at its close.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the field a joining client waits on.</b> The client's join machine
/// reads field 0 and treats <c>0</c> and <c>7</c> as "wait and ask again"; any
/// other value makes it write its lobby-slot echo and move on
/// (<see cref="Rooms.RoundStateBodiesUtils.RoundPhaseBody"/>). A host that never
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

            await WriteRoundPhaseAsync(context, (byte)round);
        }
    }

    /// <summary>
    /// Writes one peer the room's phase record on the round's stream.
    /// </summary>
    /// <param name="context">Context the record is written through.</param>
    /// <param name="round">
    /// The round the match is on, or <c>0</c> for the moment a round has closed
    /// and the room is back in its briefing.
    /// </param>
    private async Task WriteRoundPhaseAsync(PeerContext context, byte round)
    {
        try
        {
            await context.Send(
                UdpCommandConstants.PlayerProfile,
                Rooms.RoundStateBodiesUtils.RoundPhaseBody(round),
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
