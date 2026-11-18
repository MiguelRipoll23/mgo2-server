using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The room-state half of the briefing clock: the stage records a briefing writes beside its
/// countdown once the room has a round behind it.
/// </summary>
/// <remarks>
/// A partial of <see cref="BriefingCountdownService"/>, split out to keep each file under
/// the repository's size limit. The records belong to the countdown's tick — that is what
/// writes them — so they stay in this service rather than in a writer of their own.
/// </remarks>
public sealed partial class BriefingCountdownService
{
    /// <summary>
    /// The room's own state, written beside the countdown on every tick of a room
    /// whose round has played: the stage records the reference host writes through
    /// a briefing that follows a round.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured, and carried by this tick because this is what a briefing
    /// sends.</b> The reference hosts write the two 22-byte room-state records
    /// through a briefing in groups 2.2 to 2.6 s apart —
    /// `mgo2-game1` t+466.0, 470.5, 472.6, `mgo2-game2` t+62.2, 64.4, 224.6 and
    /// `mgo2-started-briefing-then-solo-game` t+57.6, 60.0 — which is the cadence
    /// of this tick (<see cref="BriefingCountdownBodiesUtils.PublishPeriodMilliseconds"/>).
    /// The records themselves are the pair the run writes
    /// (<see cref="RoomStateRecordBodiesUtils.RoomStateRecord"/> and
    /// <see cref="RoomStateRecordBodiesUtils.RoomStateRecordSecond"/>), so a peer is told
    /// the room's stage both when a round opens and for as long as a briefing runs.
    /// </para>
    /// <para>
    /// <b>The group belongs to the room's state and not to the phase, so a room
    /// with no round behind it writes none of it.</b> Every briefing named above
    /// follows a round; a fresh room's first briefing is the case the captures
    /// separate. The reference <c>mgo2-game-started-on-briefing</c> joins an
    /// unplayed room and writes neither the 22-byte record nor its pair in its
    /// 48 s briefing, while this host wrote them from the join onward — telling
    /// the client a round exists while it is still being briefed
    /// (<c>docs/analysis/join-stall-after-mode2.md</c> §10). The condition is the
    /// room's completed-round flag: its rotation index may already be nonzero
    /// before the first round opens.
    /// </para>
    /// </remarks>
    /// <param name="context">Context the pair is written through.</param>
    private async Task PublishRoomStateAsync(PeerContext context)
    {
        if (!roundState.HasRoundEnded)
        {
            return;
        }

        // Both values are taken here, in this order, so the pair is consecutive: the
        // record is built with its byte, as the image's builders build theirs.
        var state = new List<Rooms.RosterRecord>
        {
            new(
                UdpCommandConstants.PlayerProfile,
                RoomStateRecordBodiesUtils.RoomStateRecord(),
                sequences.Take(UdpCommandConstants.PlayerProfile)),
            new(
                UdpCommandConstants.PlayerProfile,
                RoomStateRecordBodiesUtils.RoomStateRecordSecond(),
                sequences.Take(UdpCommandConstants.PlayerProfile)),
        };

        await Rooms.RosterRunSendUtils.SendAsync(context, state);
    }
}
