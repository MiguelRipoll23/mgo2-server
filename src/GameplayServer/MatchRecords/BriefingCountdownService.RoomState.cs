using System.Runtime.CompilerServices;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The room-state half of the briefing clock: the stage records a briefing writes beside its
/// countdown once the room has a round behind it.
/// </summary>
/// <remarks>
/// A partial of <see cref="BriefingCountdownService"/>, split out to keep each file under
/// the repository's size limit. The countdown's loop is the writer that carries them, but
/// their cadence is the room's own tick rather than the countdown's second.
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
    /// `mgo2-started-briefing-then-solo-game` t+57.6, 60.0 — which is the room's own
    /// tick (<see cref="MatchRoundStateService.BriefingTickMilliseconds"/>), kept by a
    /// gate because the countdown it rides beside now writes once a second.
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
    /// <summary>
    /// When each peer was last sent the room-state pair, in milliseconds since boot.
    /// </summary>
    /// <remarks>
    /// The pair has its own 2.2 to 2.6 s cadence, which is the room's tick, while the
    /// countdown's tick is one second. Sharing the countdown's loop without this gate
    /// would write the pair twice as often as the captures do.
    /// </remarks>
    private readonly ConditionalWeakTable<PeerSession, RoomStateClock> roomStateClocks = [];

    /// <summary>Instant one peer was last sent the room-state pair.</summary>
    private sealed class RoomStateClock
    {
        /// <summary>Milliseconds since boot, the unit the write paths pace in.</summary>
        public long LastWrittenAt { get; set; } = long.MinValue;
    }

    /// <param name="context">Context the pair is written through.</param>
    private async Task PublishRoomStateAsync(PeerContext context)
    {
        if (!roundState.HasRoundEnded)
        {
            return;
        }

        // The countdown writes every second; the pair it rides beside is a 2.35 s
        // record, so it is only built when its own period has elapsed for this peer.
        var clock = roomStateClocks.GetOrCreateValue(context.Session);
        var now = Environment.TickCount64;
        if (now - clock.LastWrittenAt < MatchRoundStateService.BriefingTickMilliseconds)
        {
            return;
        }

        clock.LastWrittenAt = now;

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
