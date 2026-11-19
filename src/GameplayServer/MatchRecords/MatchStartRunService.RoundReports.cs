using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Waits for every client in the room to report the round that follows the
    /// result screens now ending.
    /// </summary>
    /// <remarks>
    /// <b>The reference opens on the room's last report, not on the screens'
    /// clock alone.</b> A client that is still reading the result screen sends its
    /// report before it leaves it; opening the round on the clock starts the map
    /// over a client that has not reported, and its own player disappears and the
    /// session drops. A room of one client and its bots has one report to wait for
    /// too: the live client sends its report after the screen, so the one report is
    /// waited for; a room of several waits for all of them, with a bound so one
    /// client that never reports cannot hold the room for ever.
    /// </remarks>
    /// <param name="roundNumber">The round whose screens just ended, for the log.</param>
    private async Task WaitForRoundReportsAsync(int roundNumber)
    {
        var expected = briefingStates.ClientCount;
        if (expected <= 0)
        {
            return;
        }

        var waited = 0;
        int departed;
        while (true)
        {
            lock (gate)
            {
                departed = roundDeparted.Count;
            }

            if (departed >= expected
                || waited >= MatchScreenTimingsUtils.RoundReportWaitMilliseconds
                || !roundState.HasRoundEnded)
            {
                break;
            }

            await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
            waited += MatchRoundStateService.BriefingRunPollMilliseconds;
        }

        if (departed < expected)
        {
            logger.LogWarning(
                "Round {RoundNumber}'s result screens ended with {Departed} of {Expected} client(s) "
                + "reporting the round after them; opening it after {Waited} ms rather than holding the room",
                roundNumber,
                departed,
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
