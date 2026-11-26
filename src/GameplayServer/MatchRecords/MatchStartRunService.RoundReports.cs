using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Waits, after a round's result screens have ended, for the room's clients to
    /// report the round that follows them.
    /// </summary>
    /// <remarks>
    /// <b>The reference opens on the room's last report, not on the screens'
    /// clock alone.</b> A client reports its map-loaded state once the screens are
    /// behind it, and that report is also what makes it ask for its own entry: the
    /// live run of 2026-11-26 opened round 2 at 16:08:00, wrote the round's state
    /// before the client's 16:08:01 report, and the client never sent the `0x83`
    /// request that draws the entry it spawns with. The count is the room's
    /// reported players, which a round's end clears, so it is this boundary's
    /// reports and not the round's; a room of one client and its bots waits for
    /// its one report, and the bound keeps a client that never reports from
    /// holding the room for ever.
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
        int reported;
        while (true)
        {
            reported = briefingStates.ReportedPlayerCount;
            if (reported >= expected
                || waited >= MatchScreenTimingsUtils.RoundReportWaitMilliseconds
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
