using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

public sealed partial class MatchStartRunService
{
    /// <summary>
    /// How long the screens' bound is held open for the reports still owing, once
    /// the measured pace has run out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The screens' pace and the client's own are not the same clock, and the
    /// client's is the longer one.</b> The live client reported the next round 29 s
    /// after the close (2026-11-28 00:58:37, closed 00:58:08) and 25 s after it in
    /// the run before (00:42:41 against 00:42:16); this room's bound is 24 s, so
    /// every one of those opens happened while the client was still on its result
    /// screens or in the fade that follows them. The capture opens 0.19 s after the
    /// last report of its room and not a moment before it
    /// (<c>docs/analysis/join-stall-after-mode2.md</c> §57), so the bound holds the
    /// room open for the reports it is waiting on instead of opening into a client
    /// that cannot read the new round: the opening set is refused outright while the
    /// client's phase flag is still set (`P2P_RunOpener_BuildAndSend`, <c>+0xc0</c>),
    /// which is a round heard playing under a black screen (2026-11-28 01:0x).
    /// </para>
    /// <para>
    /// <b>It is a grace and not a gate.</b> A room nobody reports from is opened at
    /// its end, so a client that never reports costs a longer black screen rather
    /// than a stalled room.
    /// </para>
    /// </remarks>
    public const int RoundScreensReportGraceMilliseconds = 10_000;
    /// <summary>
    /// The clients that have reported the round that follows the result screens
    /// now running, read under <c>gate</c>.
    /// </summary>
    private int RoundDepartedCount
    {
        get
        {
            lock (gate)
            {
                return roundDeparted.Count;
            }
        }
    }

    /// <summary>
    /// Waits, from the round's close, for the room's clients to report the round
    /// that follows its result screens, or for the screens' measured pace to run
    /// out.
    /// </summary>
    /// <remarks>
    /// <b>The screens' pace releases the round; a report only shortens it.</b> The
    /// reference opens 0.19 s after its client's last post-screen report
    /// (<c>mgo2-started-on-game-round1</c> round 2, t+215.632), but that report is
    /// sent only once the round has been told to the client. Measured twice on
    /// 2026-11-27: the live client reported 2 s after a 30 s bound opened round 2,
    /// and 2 s after a 60 s one — never before the open. A bound longer than the
    /// screens therefore cannot produce the report it waits for, and the wait ends
    /// where the screens do, on their measured 24.0 s pace
    /// (<see cref="MatchScreenTimingsUtils.RoundEndScreensMilliseconds"/>). The
    /// count is <see cref="roundDeparted"/>, which the close clears and the reports
    /// taken while <see cref="MatchRoundStateService.IsRoundEnding"/> route into
    /// it; a room of one client and its bots waits for its one report.
    /// </remarks>
    /// <param name="roundNumber">The round whose screens are running, for the log.</param>
    private async Task WaitForRoundReportsAsync(int roundNumber)
    {
        var expected = briefingStates.ClientCount;
        if (expected <= 0)
        {
            return;
        }

        var waited = 0;
        int reported;
        while (true)
        {
            reported = RoundDepartedCount;

            // A client that leaves is no longer one the room waits for.
            expected = briefingStates.ClientCount;
            if (reported >= expected
                || waited >= MatchScreenTimingsUtils.RoundEndScreensMilliseconds
                || !roundState.HasRoundEnded)
            {
                break;
            }

            await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
            waited += MatchRoundStateService.BriefingRunPollMilliseconds;
        }

        // **The bound is the screens' pace, and the open is the room's.** A client
        // half a second behind its own screens reports just after the bound, and a
        // room that opens into it is a room whose next round meets a client still in
        // its fade: the live session of 2026-11-28 00:50:53 opened round 2 at the
        // bound and was reported 0.9 s later, and the round played out of the client's
        // sight. The capture opens 0.19 s after the last report and never before it,
        // so the bound is given a short grace for the reports it is waiting on — and
        // only the reports: a room nobody reports from still opens, at the grace's
        // end.
        var grace = 0;
        while (reported < expected
            && roundState.HasRoundEnded
            && grace < RoundScreensReportGraceMilliseconds)
        {
            await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
            grace += MatchRoundStateService.BriefingRunPollMilliseconds;
            reported = RoundDepartedCount;
            expected = briefingStates.ClientCount;
        }

        if (reported < expected)
        {
            logger.LogWarning(
                "Round {RoundNumber}'s result screens ended with {Reported} of {Expected} client(s) "
                + "reporting the round after them; opening it after {Waited} ms rather than holding the room",
                roundNumber,
                reported,
                expected,
                waited);
            return;
        }

        logger.LogDebug(
            "Round {RoundNumber}'s result screens are over and all {Expected} client(s) have reported the "
            + "round after them, so it opens",
            roundNumber,
            expected);
    }
}
