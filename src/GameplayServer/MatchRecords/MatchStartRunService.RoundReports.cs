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
    /// that follows its result screens.
    /// </summary>
    /// <remarks>
    /// <b>The reference opens on the room's last report, not on the screens'
    /// clock.</b> A client reports its map-loaded state once the screens are
    /// behind it, and that report is also what makes it ask for its own entry: the
    /// live run of 2026-11-26 opened round 2 at 16:08:00, wrote the round's state
    /// before the client's 16:08:01 report, and the client never sent the `0x83`
    /// request that draws the entry it spawns with. The count is
    /// <see cref="roundDeparted"/>, which the close clears and the reports taken
    /// while <see cref="MatchRoundStateService.IsRoundEnding"/> route into it; a
    /// room of one client and its bots waits for its one report, and the bound
    /// keeps a client that never reports from holding the room for ever. The
    /// bound starts at the close, so it spans the client's whole screen — the
    /// reference's measured 24.0 s — rather than starting after it.
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
