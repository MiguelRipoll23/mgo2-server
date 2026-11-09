using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The other end of a round: the clock that closes it, and the room an emptied one
/// is returned to.
/// </summary>
/// <remarks>
/// The reports that ask for a round are taken in <see cref="MatchStartRunService.OnJoinReport"/>
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
    /// where every briefing's are (<see cref="BeginBriefing"/>); the count it is
    /// shown from is armed by the first report to reach the fresh briefing
    /// (<see cref="BeginBriefingClock"/>) and the deadline that ends it only once
    /// the room has a second player (<see cref="StartBriefingCount"/>), which is
    /// what makes the reset room a room waiting for a game rather than one replaying
    /// a match nobody played.
    /// </para>
    /// </remarks>
    /// <returns><c>true</c> when the room held no clients and has been reset.</returns>
    public bool ResetWhenEmpty()
    {
        // A bot is a seat kept free for a player, not a player: a room whose only
        // members are bots is a room nobody is in, and the round a client left must
        // not be handed to the client that rejoins (RoomRosterService.HumanJoinerCount).
        if (roster.HumanJoinerCount > 0)
        {
            return false;
        }

        var dropped = roundState.Reset();
        score.Reset();
        score.ResetRoundWins();
        lock (gate)
        {
            briefingDeparted.Clear();
        }
        briefingStates.BeginNextBriefing();
        sequences.Restart(Rooms.RoomRosterService.ExchangeIdentifier);
        BeginBriefing();

        logger.LogInformation(
            "The room holds no client; its round count, clock and tickets start over (a round was playing: "
            + "{WasPlaying}) and a fresh briefing runs",
            dropped);
        return true;
    }

    /// <summary>
    /// Whether the room is between two rounds of one game: the round that just
    /// ended has more of its game to play.
    /// </summary>
    /// <remarks>
    /// <b>That stretch is not a briefing.</b> A game of several rounds is stated as
    /// consecutive identical rotation entries (<c>MatchSettings.IsLastRoundOfGame</c>)
    /// and the captures play it back to back: <c>mgo2-game2</c> re-readies on the
    /// sibling set at t+107.978 and its round opens at t+109.120 on a whole clock,
    /// and a client that joined a round in progress was served that round at 1 324
    /// of 2 400 units with no briefing (<c>mgo2-joined-game-in-progress-last-round</c>
    /// t+59.200, <c>docs/protocol/MATCH_FLOW.md</c> §5).
    /// </remarks>
    private bool IsBetweenRoundsOfGame =>
        roundState.HasRoundEnded
        && !roundState.IsOpen
        && !matchSettings.IsLastRoundOfGame(roundState.RoundNumber);

    /// <summary>
    /// Begins the briefing a peer entering the room is shown, unless the room is
    /// inside a game that has not ended.
    /// </summary>
    private void BeginBriefingForEntry()
    {
        if (roundState.IsOpen || IsBetweenRoundsOfGame)
        {
            logger.LogDebug(
                "The room is inside a game that has not ended, so no briefing begins for the "
                + "peer entering it: it is served the round");
            return;
        }

        BeginBriefing();
    }

    /// <summary>
    /// Ends the round now playing and returns the room to its briefing, where a
    /// fresh set of reports opens the next round.
    /// </summary>
    /// <param name="reason">Why the round ended, for the log.</param>
    /// <param name="roundResult">
    /// Result code the round ended on, or <c>null</c> when this host has none to
    /// write. It is kept beside the round so the peer writers can publish it
    /// (<see cref="MatchRoundStateService.LastRoundResult"/>), and the round is kept
    /// as its game's last when the rotation says it is, which is what decides the
    /// game's own result and starts its rounds-won counters over once their last
    /// number has gone out.
    /// </param>
    /// <returns><c>true</c> when a round was playing and has now ended.</returns>
    public bool End(string reason, byte? roundResult = null)
    {
        // The rotation is what says how many rounds a game is, and the count only
        // moves when the next round opens: the round now playing is the one that
        // ends, so it is read before the close names it.
        var playing = roundState.RoundNumber;
        var closesGame = matchSettings.IsLastRoundOfGame(playing);

        // A round that is its game's last states the game's result as well, in the
        // round's own write — `02 02 02 02 02` is "state[2] = 2, state[3] = 2"
        // (docs/protocol/STATE_VECTOR.md §3).
        var ended = roundState.Close(roundResult, closesGame);
        if (ended is null)
        {
            return false;
        }

        // A round inside a game is followed by the next round of that game, not
        // by a briefing. The rotation states a game of several rounds as
        // consecutive identical entries (MatchSettings.IsLastRoundOfGame), and the
        // capture plays those rounds back to back: mgo2-started-on-game-round1
        // covers two whole five-minute rounds between t+601.4 and t+1238.2 with no
        // zero-clock stretch anywhere between them (docs/protocol/MATCH_FLOW.md §5).
        //
        // The briefing instant is left where the game's own briefing put it, so the
        // report the client sends between two rounds is already past the room's
        // briefing time and opens the next round, instead of the room waiting out a
        // briefing nothing shown it.
        if (!closesGame)
        {
            logger.LogInformation(
                "Round {RoundNumber} ended because {Reason}; its game has another round, so the room "
                + "runs no briefing and the next round opens on the report that follows it",
                ended,
                reason);
            return true;
        }

        // A game's last round is preceded by the game's briefing: the captures
        // open a round, play it, and then rewrite the ready set for the game after
        // it (docs/protocol/BRIEFING_FLOW.md). Clearing the states here is what
        // makes the next game wait for a briefing of its own instead of opening the
        // moment the last round's clock stopped.
        briefingStates.BeginNextBriefing();

        // The next briefing runs on its own count and is ended by the report that
        // follows it, so a client that came back from its screen starts the round
        // however many of the room's players readied.
        BeginBriefing();
        logger.LogInformation(
            "Round {RoundNumber} ended because {Reason}; the room is back in its briefing and the next "
            + "round opens on the report that follows its briefing",
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
