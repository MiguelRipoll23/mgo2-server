using Mgo2Server.GameplayServer.Match;
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
    /// The round the briefing now running precedes: the next round the room's own
    /// rotation names. It is <c>null</c> while the room is not published.
    /// </summary>
    /// <remarks>
    /// It reads the rotation directly rather than going through
    /// <see cref="ReadRound"/> because the briefing polls it and a warning per poll
    /// is noise; the round's own open logs it once.
    /// </remarks>
    private MatchRotationEntry? ReadBriefingRound() =>
        matchSettings.FindPublished()?.Round(roundState.RoundNumber + 1);

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
        var waitingOnStageOpening = 0;

        while (MatchRecordPeerUtils.IsAlive(session) && !roundState.IsOpen)
        {
            // A stage's result screens are not a briefing: the client sits on the
            // round and stage results before the next stage is announced, so no
            // briefing's run is written through them. The briefing is armed by the
            // stage end, and this wait is what keeps the played round's briefing
            // from being written again underneath the screens.
            if (roundState.IsStageEnding || stageBriefingPending)
            {
                await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
                continue;
            }

            // Between two rounds of one game there is no briefing to write: the
            // room plays straight on, the client re-readies on the sibling set and
            // the round's own run answers it (WriteRunAsync). A briefing's run here
            // would be the between-games shape — full round state, clock zero — and
            // the client would draw a briefing in the middle of the match
            // (IsBetweenRoundsOfGame).
            if (IsBetweenRoundsOfGame)
            {
                return;
            }

            // No run is written before a briefing has heard a report: the run
            // echoes that report's byte, so a briefing that has heard from nobody
            // has nothing to write one from.
            if (BriefingGeneration is not { } generation)
            {
                await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
                continue;
            }

            // **The stage-end briefing is paced by its opening and then follows
            // the initial briefing's own path.** The opening states the room and
            // holds the count before any peer reports; the report that arrives
            // while it is being paced is the client's map-loaded state and not the
            // round's request. Once the opening is out, the run, the room state
            // behind it and the count's own zero are the ones a room's first
            // briefing writes, and the round opens on that zero — never on the
            // report that beat the opening, which put the client into the next
            // stage's first round in the same second as the briefing screen (live
            // session of 2026-11-28 02:52:15).
            if (StageBriefingNumber > 0 && StageOpeningPublishedAt(session) is null)
            {
                if (waitingOnStageOpening != StageBriefingNumber)
                {
                    waitingOnStageOpening = StageBriefingNumber;
                    logger.LogInformation(
                        "UDP {LocalPort}: the stage-end briefing {BriefingNumber} is waiting for its "
                        + "opening to reach {RemoteAddress}",
                        context.LocalPort,
                        StageBriefingNumber,
                        context.Remote);
                }

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
            // entry and the report, and the entry decides nothing of the run's
            // shape: every capture opens it on the pair of empty 0x5001 heads
            // (`MatchStartRunFramesUtils.BuildRun`).

            if (ReadBriefingRound() is not { } round)
            {
                // The room is not published yet, so there is no round for the
                // briefing to precede and no time to hold its clock at.
                await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
                continue;
            }

            // The dwell is re-read after every wait because the entry can be
            // heard after it was first counted from the report alone. A peer that
            // reports first and enters half a second later must still get the
            // whole dwell behind that entry: reading it once before the echo
            // arrives is what let the run land 1.97 s after the entry, level
            // with the client's own operations rows, in the join of 30 October
            // 15:48 (`docs/analysis/join-stall-after-mode2.md` §73).
            while (EntryRunDwellRemaining(session) is { } owed)
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

            // The room's state while this peer is briefed: the value the room body
            // it joined with carries, so the state record and the room agree
            // (Rooms.RoomState.Briefing). The stage-end briefing writes it here as
            // well: its opening states the transition and the phase in front of the
            // roster, and the run is followed by the same briefing state a room's
            // first briefing writes, so both briefings leave the room on the value
            // their run is drawn under.
            await WriteRoomStateAsync(context, Rooms.RoomState.Briefing);

            // The peer's own round state is not started here. It is the round's
            // state and not the briefing's, and a room's first briefing draws
            // nothing when it is written into it; the round's open starts it and
            // it then outlives the round (MatchStartRunService.PeerRoundState).

            // A room of one's closing zero then opens the round, and this host
            // writes that itself: no report follows the zero there. Measured:
            // `briefing-then-solo-game` writes its zero at t+145.098 and its round
            // at t+146.007, and what its only client sends in between is an
            // acknowledgement of the zero and its `e1 0a` echo — no report. Waiting
            // for one is what leaves that client on its briefing screen, the game
            // faded out and never back in. A room of several does report after its
            // zero and opens on that report (`OnJoinReport`), and this peer waits
            // for the open it writes.
            while (MatchRecordPeerUtils.IsAlive(session) && !roundState.IsOpen)
            {
                if (IsBetweenRoundsOfGame)
                {
                    return;
                }

                // <b>The count's own zero opens the round.</b> A briefing that has
                // run its time out is over for every client in it, and the record
                // that says so is the count's closing zero — the same one a complete
                // ready set draws, which is what closes a briefing early when the
                // room readies. A client that stays on its screen and never sends
                // the report that follows the zero would otherwise hold the whole
                // room for a minute it has already waited out, so the zero is the
                // end here and not the leave that may or may not follow it. The
                // dwell behind the zero is the fade the client is in; it is kept
                // because it is the client's and not the report's.
                if (!countdown.HasEnded)
                {
                    await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
                    continue;
                }

                // The open waits behind the zero as well: `briefing-then-solo-game`
                // writes its zero at t+145.098 and its round at t+146.007, 0.9 s
                // later, and the client is fading out of its briefing across that
                // gap. Opening in the zero's own tick sent the live client from
                // that fade straight into the map and crashed it in its camera
                // code (`cam_transparent.cc`).
                await Task.Delay(MatchRecordTimingUtils.RoundOpenDwellMilliseconds);
                if (!MatchRecordPeerUtils.IsAlive(session) || roundState.IsOpen)
                {
                    return;
                }

                // The round plays under the other set: the briefing's own is the
                // one its first report arrived on, and the reference's briefing
                // at 0x1254 is followed by its round at 0x1a54, one sibling bit
                // along (MatchRecordGenerationUtils.Sibling).
                OpenRound(
                    generation.Sibling,
                    "the lone player's briefing wrote its closing zero");
                return;
            }
        }
    }
}
