using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The other end of a round: the clock that closes it, and the room an emptied one
/// is returned to.
/// </summary>
/// <remarks>
/// The reports that ask for a round are taken in <see cref="MatchStartRunService.Open"/>
/// and the run each round's open writes lives in <c>MatchStartRunService.Runs.cs</c>;
/// this file is what comes after that — a round that ends, the wait that ends it on
/// its own time limit, and the fresh briefing a room with nobody left in it goes
/// back to.
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Returns an emptied room to its first round: no round playing, no round
    /// counted, no score or tickets carried, and a fresh briefing running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The room's population decides, and only an empty room resets.</b> A
    /// leaver among others changes nothing: the round that is playing is the round
    /// the players still in the room are playing, and it goes on under its own
    /// clock. With the last client gone there is no round at all — its count, its
    /// clock and its tickets belong to a match that is over — so the next client
    /// into the room arrives at a first round instead of into the one that
    /// emptied.
    /// </para>
    /// <para>
    /// <b>The briefing it begins is the room's own.</b> The streams are re-armed
    /// where every briefing's are (<see cref="BeginBriefing"/>) and the clock that
    /// ends it is armed by the first report to reach the fresh briefing
    /// (<see cref="ArmBriefingClock"/>), which is what makes the reset room a room
    /// waiting for a game rather than one replaying a match nobody played.
    /// </para>
    /// </remarks>
    /// <returns><c>true</c> when the room held no clients and has been reset.</returns>
    public bool ResetWhenEmpty()
    {
        if (roster.JoinerCount > 0)
        {
            return false;
        }

        var dropped = roundState.Reset();
        score.Reset();
        ready.BeginNextBriefing();
        BeginBriefing();

        logger.LogInformation(
            "The room holds no client; its round count, clock and tickets start over (a round was playing: "
            + "{WasPlaying}) and a fresh briefing runs",
            dropped);
        return true;
    }

    /// <summary>
    /// Ends the round now playing and returns the room to its briefing, where a
    /// fresh set of reports opens the next round.
    /// </summary>
    /// <param name="reason">Why the round ended, for the log.</param>
    /// <param name="roundResult">
    /// Result code the round ended on, or <c>null</c> when this host has none to
    /// write. It is kept beside the round so the peer writers can publish it
    /// (<see cref="MatchRoundStateService.LastRoundResult"/>).
    /// </param>
    /// <returns><c>true</c> when a round was playing and has now ended.</returns>
    public bool End(string reason, byte? roundResult = null)
    {
        var ended = roundState.Close(roundResult);
        if (ended is null)
        {
            return false;
        }

        // A round is preceded by its own briefing: the captures open a round,
        // play it, and then rewrite the ready set for the round after it
        // (docs/protocol/BRIEFING_FLOW.md). Clearing the states here is what
        // makes the next round wait for reports of its own instead of opening
        // the moment the last round's clock stopped.
        ready.BeginNextBriefing();

        // The next briefing runs on its own deadline as well: a player who reports
        // late delays the round by the briefing time at most, and never past it.
        BeginBriefing();
        logger.LogInformation(
            "Round {RoundNumber} ended because {Reason}; the room is back in its briefing and the next "
            + "round opens when its players have reported again or when the briefing runs out",
            ended,
            reason);
        return true;
    }

    /// <summary>
    /// Ends a round once its time limit has run out. A round that has already
    /// been ended — or replaced — by the time the limit lands is left alone.
    /// </summary>
    /// <remarks>
    /// The wait is the round's own time limit, which is the clock the round
    /// opened on and the room's rule setting for it
    /// (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2). A round that ends on a
    /// goal instead never reaches this wait, which is why the check is on the
    /// round the wait belongs to rather than on the round being open.
    /// </remarks>
    /// <param name="roundNumber">Round the wait belongs to.</param>
    /// <param name="timeLimitMilliseconds">
    /// How long the round runs: its own rule's time, which is what its opening
    /// run says the clock is worth.
    /// </param>
    private async Task EndWhenTimeRunsOutAsync(int roundNumber, int timeLimitMilliseconds)
    {
        try
        {
            await Task.Delay(timeLimitMilliseconds);
            if (roundState.RoundNumber != roundNumber || !roundState.IsOpen)
            {
                return;
            }

            End(
                $"round {roundNumber}'s time limit ran out after "
                + $"{timeLimitMilliseconds} ms",
                await results.ClockResultAsync());
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "The round time limit for round {RoundNumber} ended without stopping it",
                roundNumber);
        }
    }
}
