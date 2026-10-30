using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The run a briefing writes in answer to a peer's entry: the watcher that waits
/// for the briefing's set, the peer's entry and the entry's own dwell, and then
/// writes the briefing's run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Split out of <c>MatchStartRunService.Entries.cs</c> so neither file passes the
/// 400-line limit.</b> The entry note is there; the run it draws is here.
/// </para>
/// <para>
/// <b>The dwell is what the pair waits on as well.</b> The room's first
/// <c>85</c>/<c>86</c> copy is written inside this run in both reference briefings,
/// so the publication reads the same due instant
/// (<see cref="MatchStartRunService.IsEntryAnswerDue"/>,
/// <c>docs/analysis/join-stall-after-mode2.md</c> §55).
/// </para>
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Writes the briefing's own run to a peer once the briefing has heard a
    /// report and the dwell behind the peer's entry has been spent, and returns
    /// when the round it precedes opens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The run is the answer to the peer's entry, and the report only names
    /// the set.</b> A client reports while it is still loading its map and echoes
    /// the lobby-slot pair once it has been placed in the room; every capture
    /// writes the run behind the echo and no capture writes one behind the
    /// report (<see cref="MatchStartRunService.OnRoomEntry"/>,
    /// <c>docs/analysis/join-stall-after-mode2.md</c> §18).
    /// </para>
    /// <para>
    /// <b>The briefing's run is not its round's.</b> A client still being briefed
    /// is served the run the first report named, under that report's own set, and
    /// the round it is waiting for opens later and under the other set
    /// (<see cref="MatchStartRunService.OnJoinReport"/>, <c>docs/protocol/BRIEFING_FLOW.md</c>
    /// §1–§2). The run's clock stays whole: nothing of the round has been played
    /// while the client is still reading the briefing, and the clock the countdown
    /// shows it is the briefing's own seven-byte record rather than this one.
    /// </para>
    /// <para>
    /// <b>And it waits out the entry's dwell before it goes.</b> Both reference
    /// briefings answer the echo two seconds later and nothing else in between,
    /// and the clients use that gap to start their own operations stream
    /// (<see cref="EntryRunDwellMilliseconds"/>). A run written in the entry's own
    /// dwell overtakes that transition, which is <c>myserver23.pcapng</c>'s join.
    /// </para>
    /// </remarks>
    /// <param name="context">Context the run is written through.</param>
    private async Task WriteBriefingRunAsync(PeerContext context)
    {
        var session = context.Session;

        while (MatchRecordPeerUtils.IsAlive(session) && !roundState.IsOpen)
        {
            // No run is written before a briefing has heard a report: the run
            // echoes that report's byte, so a briefing that has heard from nobody
            // has nothing to write one from.
            if (BriefingGeneration is not { } generation)
            {
                await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
                continue;
            }

            // The run is the answer to the peer's entry and not to its report, and
            // the two are not the same instant: the report arrives while the client
            // is still loading its map, and the captures write the run behind the
            // entry it answered (OnRoomEntry). The echo is not a record this host
            // can require, though: one it never reads is a peer left with no room
            // state at all — the black screen a client shows while the map it
            // loaded has no briefing over it, which is the live session of
            // 2026-10-29 (`docs/analysis/join-stall-after-mode2.md` §60). So the
            // dwell below is what releases the run, counted from the later of the
            // entry and the report, and the entry only decides which opening pair
            // the run carries (EntryAnswersOf): a peer whose echo was heard gets
            // its answer back and one whose echo was not opens on the recorded
            // empty heads.

            if (ReadBriefingRound() is not { } round)
            {
                // The room is not published yet, so there is no round for the
                // briefing to precede and no time to hold its clock at.
                await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
                continue;
            }

            if (EntryRunDwellRemaining(session) is { } owed)
            {
                await Task.Delay(owed);
                if (roundState.IsOpen)
                {
                    // The briefing ended inside the dwell, so the round's own run
                    // is this peer's next record and this briefing's is stale.
                    return;
                }
            }

            await WriteRunCoreAsync(context, generation, round, round.TimeLimitUnits, briefing: true);
            return;
        }
    }
}
