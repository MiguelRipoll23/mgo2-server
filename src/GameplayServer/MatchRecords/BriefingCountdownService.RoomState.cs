using System.Runtime.CompilerServices;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The room-state half of the briefing clock: the objective points a briefing
/// writes beside its countdown once the room has a round behind it.
/// </summary>
/// <remarks>
/// <para>
/// A partial of <see cref="BriefingCountdownService"/>, split out to keep each file
/// under the repository's size limit. The countdown's loop is the writer that
/// carries them, but their cadence is the room's own tick rather than the
/// countdown's second.
/// </para>
/// <para>
/// <b>The records are the next round's objectives, not a stage banner.</b> The
/// reference hosts write kind-<c>0x03</c> bodies through a briefing in groups 2.2
/// to 2.6 s apart — `mgo2-game1` t+466.0, 470.5, 472.6, `mgo2-game2` t+62.2, 64.4,
/// 224.6 and `mgo2-briefing-then-solo-game` t+9.780, 11.579, 13.379 — which is the
/// room's own tick (<see cref="MatchRoundStateService.BriefingTickMilliseconds"/>),
/// kept by a gate because the countdown it rides beside now writes once a second.
/// Each body is one of the stage's targets
/// (<see cref="CustomPointRecordBodiesUtils"/>, <see cref="Match.CustomPointUtils"/>),
/// so the peer is told where the round it is waiting for puts its objectives before
/// that round's run repeats them.
/// </para>
/// <para>
/// <b>The set belongs to the round in front of the room and not to the phase, so a
/// room with no round behind it writes none of it.</b> Every briefing named above
/// follows a round; a fresh room's first briefing is the case the captures
/// separate. The reference <c>mgo2-game-started-on-briefing</c> joins an unplayed
/// room and writes no kind-<c>0x03</c> body in its 48 s briefing, while this host
/// wrote the recorded pair from the join onward — telling the client a round exists
/// while it is still being briefed
/// (<c>docs/analysis/join-stall-after-mode2.md</c> §10). The condition is the
/// room's completed-round flag: its rotation index may already be nonzero before
/// the first round opens.
/// </para>
/// <para>
/// <b>And a rule with no objectives writes none, whatever the phase.</b> The old
/// pair was Blood Bath's Capture targets sent in every mode, which is how KEROTAN
/// and GA-KO appeared in Team Deathmatch; the count is the rule's now
/// (<see cref="Match.CustomPointUtils.For"/>).
/// </para>
/// </remarks>
public sealed partial class BriefingCountdownService
{
    /// <summary>
    /// When each peer was last sent the room's points, in milliseconds since boot.
    /// </summary>
    /// <remarks>
    /// The group has its own 2.2 to 2.6 s cadence, which is the room's tick, while
    /// the countdown's tick is one second. Sharing the countdown's loop without this
    /// gate would write the points twice as often as the captures do.
    /// </remarks>
    private readonly ConditionalWeakTable<PeerSession, RoomStateClock> roomStateClocks = [];

    /// <summary>Instant one peer was last sent the room's points.</summary>
    private sealed class RoomStateClock
    {
        /// <summary>Milliseconds since boot, the unit the write paths pace in.</summary>
        public long LastWrittenAt { get; set; } = long.MinValue;
    }

    /// <param name="context">Context the points are written through.</param>
    private async Task PublishCustomPointsAsync(PeerContext context)
    {
        if (!roundState.HasRoundEnded)
        {
            return;
        }

        // The round in front of the room: the briefing precedes it, so its rule and
        // map are the ones the points belong to — not the round that just ended.
        var round = matchSettings.FindPublished()?.Round(roundState.RoundNumber + 1);
        if (round is null)
        {
            return;
        }

        var points = Match.CustomPointUtils.For(round.Map, round.Rule);
        if (points.Count == 0)
        {
            return;
        }

        // The countdown writes every second; the points it rides beside are a 2.35 s
        // group, so it is only built when its own period has elapsed for this peer.
        var clock = roomStateClocks.GetOrCreateValue(context.Session);
        var now = Environment.TickCount64;
        if (now - clock.LastWrittenAt < MatchRoundStateService.BriefingTickMilliseconds)
        {
            return;
        }

        clock.LastWrittenAt = now;

        var task = Match.CustomPointUtils.TaskOf(round.Rule);
        var state = new List<Rooms.RosterRecord>(points.Count);
        for (var index = 0; index < points.Count; index++)
        {
            // Each value is taken here, in this order, so the records are
            // consecutive: the body is built with its byte, as the image's builders
            // build theirs.
            state.Add(new Rooms.RosterRecord(
                UdpCommandConstants.RoomRecord,
                CustomPointRecordBodiesUtils.Build(index, task, points[index]),
                sequences.Take(UdpCommandConstants.RoomRecord)));
        }

        await Rooms.RosterRunSendUtils.SendAsync(context, state);
    }
}
