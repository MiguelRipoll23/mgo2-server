using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

public sealed partial class MatchStartRunService
{
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
