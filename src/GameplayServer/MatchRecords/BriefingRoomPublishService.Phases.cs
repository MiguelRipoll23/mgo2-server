using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The room's phase, written through a briefing so a peer that never reaches a
/// round still receives the field its own state machine reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the one field the client's join waits on, and the round's own writer
/// cannot reach a briefing.</b> The state machine at <c>0x2882B4</c> reads state
/// <c>0x00</c>'s field <c>0</c> and treats <c>0</c> and <c>7</c> as "wait and ask
/// again"; any other value releases it. Every copy this host writes comes from
/// <see cref="MatchStartRunService"/>, which writes it behind the round's run and
/// on the round's tick — so a peer sitting in a briefing before its first round,
/// and the peer that joins between two rounds, is sent no copy at all and keeps
/// whatever its last room left in the field.
/// </para>
/// <para>
/// <b>The reference writes it when no round is playing.</b> Counted over the
/// fourteen captures, the gaps between two copies cluster on the briefing's own
/// 2.35 s and the slower ones — 4.9 s, 10 s, 40 s — are the stretches in which no
/// round was playing, which are copies a briefing's host wrote
/// (<c>docs/analysis/join-stall-after-mode2.md</c> §34.4). So the record belongs on
/// this service's frame and not only on the round's.
/// </para>
/// <para>
/// <b>The value is the room's round, <c>0</c> for a room with none open</b>, which
/// is the figure <see cref="MatchRoundStateService.RoundNumber"/> already carries
/// and the one <see cref="RoundStateBodiesUtils.RoundPhaseBody"/> is built for.
/// </para>
/// </remarks>
public sealed partial class BriefingRoomPublishService
{
    /// <summary>
    /// Frames of <see cref="PublishPeriodMilliseconds"/> between two phase
    /// records: two, which is 2.0 s against the reference's 2.35 s tick.
    /// </summary>
    /// <remarks>
    /// The frame is one second and the reference's period is
    /// <see cref="MatchRoundStateService.BriefingTickMilliseconds"/>; two frames is
    /// the nearest whole number to it, 0.5 records a second against the reference's
    /// 0.43. Three frames would be 3.0 s, further away on the other side.
    /// </remarks>
    public const int RoundPhaseFrameInterval = 2;

    /// <summary>
    /// Writes one peer the room's phase on the briefing's frame.
    /// </summary>
    /// <param name="context">Context the record is written through.</param>
    private async Task PublishRoundPhaseAsync(PeerContext context)
    {
        await context.Send(
            UdpCommandConstants.PlayerProfile,
            RoundStateBodiesUtils.RoundPhaseBody((byte)roundState.RoundNumber),
            roster.TakeRunOrdinal());
    }
}
