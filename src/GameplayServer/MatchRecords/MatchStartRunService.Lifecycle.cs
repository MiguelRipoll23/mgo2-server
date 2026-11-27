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
    /// Whether a stage's last round has closed and its screens are still holding
    /// the next briefing back.
    /// </summary>
    /// <remarks>
    /// It covers the instant between the stage end closing and the briefing being
    /// armed behind it, which is where a peer's run watcher would otherwise see
    /// the screens gone and write the played briefing's run again.
    /// </remarks>
    private volatile bool stageBriefingPending;

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
    /// <para>
    /// <b>The client that emptied the room keeps its ready.</b> A session that starts
    /// its handshake over is the same client on the same briefing screen, so the
    /// ready it sent there is still true of it, and the fresh briefing begins with
    /// that one state rather than with none
    /// (<see cref="Rooms.BriefingStateService.BeginNextBriefing(string?)"/>). A room of
    /// one leaves its briefing on its ready set and on nothing else, so dropping the
    /// ready is what leaves that client on a held count that never moves.
    /// </para>
    /// </remarks>
    /// <param name="clientKeepingItsReady">
    /// Endpoint of the client whose started-over session emptied the room, whose ready
    /// state stays with the briefing that begins for it, or <c>null</c> when the room
    /// emptied for good.
    /// </param>
    /// <returns><c>true</c> when the room held no clients and has been reset.</returns>
    public bool ResetWhenEmpty(string? clientKeepingItsReady = null)
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
        results.Reset();
        countdown.Reset();

        // **The record counters are not cleared, and that is the reference's own
        // behaviour.** Its host keeps counting across a room that empties and rejoins:
        // `pcapng1`'s `0x1254` runs 416 values in one ascending line from start to
        // finish and its `0x1a54` `00..11` across two round markers, with no backward
        // step in either. A cleared counter hands a live client bytes its reader has
        // already passed, and a strict-equality reader refuses those for the rest of the
        // wrap; `myserver.pcapng` shows this host's `0x1254` going `05 -> 00` where the
        // reference never goes back (`docs/analysis/stream-sequencing.md`, D5).
        lock (gate)
        {
            briefingDeparted.Clear();
            briefingNumber = 0;
            lastJoinReportIdentifier = null;
            briefingGeneration = null;
            countStartedBriefing = null;
            joinReportCount = 0;
            arrayEchoes.Clear();
            stageBriefingPending = false;
            stageBriefingNumber = 0;
        }

        entered.Clear();
        entrySequences.Clear();
        entryAnswers.Clear();
        entryInstants.Clear();
        briefingStates.Reset(clientKeepingItsReady);
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
    /// (<see cref="MatchRoundStateService.LastRoundResult"/>), and the round is kept
    /// as its game's last when the rotation says it is, which is what decides the
    /// game's own result and starts its rounds-won counters over once their last
    /// number has gone out.
    /// </param>
    /// <param name="roundOverReason">
    /// Why the round ended. The played round's rule classifies it for the protocol
    /// (<see cref="MatchRoundOverBodiesUtils"/>): BASE's clock is <c>1</c>, TDM's
    /// clock is <c>4</c> and its ticket pool is <c>5</c>, and a rule with no measured
    /// code keeps <c>0</c> rather than borrowing another rule's message. The
    /// dedicated host keeps the classification and does not send the <c>0x0e</c>
    /// record (<see cref="MatchResultService.WriteResultAsync"/>).
    /// </param>
    /// <returns><c>true</c> when a round was playing and has now ended.</returns>
    public bool End(
        string reason,
        byte? roundResult = null,
        RoundOverReason roundOverReason = RoundOverReason.None)
    {
        // The rotation is what says how many rounds a game is, and the count only
        // moves when the next round opens: the round now playing is the one that
        // ends, so it is read before the close names it.
        var playing = roundState.RoundNumber;
        var closesGame = matchSettings.IsLastRoundOfGame(playing);
        var round = roundState.RoundSettings;
        var openedAt = roundState.OpenedAt;

        // The close happens before the next entry can become current, so the played
        // round's own rule gates the verdict.
        var roundOverVerdict = MatchRoundOverBodiesUtils.VerdictFor(
            round?.Rule ?? matchSettings.CurrentRule,
            roundOverReason,
            roundResult);

        // A round that is its game's last states the game's result as well, in the
        // round's own write — `02 02 02 02 02` is "state[2] = 2, state[3] = 2"
        // (docs/protocol/STATE_VECTOR.md §3).
        var ended = roundState.Close(roundResult, closesGame, roundOverVerdict);
        if (ended is null)
        {
            return false;
        }

        // The round's rows are written before its report screens and before the
        // next open clears the score.
        RecordClosedRound(round, openedAt, roundResult);

        // A round inside a stage is followed by the next round of that stage, not
        // by a briefing. The rotation states a stage of several rounds as
        // consecutive identical entries (MatchSettings.IsLastRoundOfGame), and the
        // reference plays those rounds back to back: mgo2-started-on-game-round1
        // covers two whole five-minute rounds between t+601.4 and t+1238.2 with no
        // zero-clock stretch anywhere between them (docs/protocol/MATCH_FLOW.md §5).
        //
        // The round's own result and score graph come first, and they are the
        // client's. The reference holds the zero clock while that client reads
        // them, and opens the next round on the client's last post-screen report;
        // the 24.0 s the capture shows is that client's pace, not a hold this host
        // imposes (`MatchScreenTimingsUtils.RoundEndScreensMilliseconds`).
        //
        // The ready set is rewritten between the rounds of one stage all the same:
        // the captures clear it at every round end and the round that follows opens
        // from the reports of its own (`joined-when-red-team` publishes nothing
        // between t+423 and t+450 but its countdown). A room that carried the set
        // over opened the next round on a player who had not reported it, which is
        // the same defect the stage end clears the set for
        // (docs/protocol/MATCH_FLOW.md §5).
        briefingStates.BeginNextBriefing();
        lock (gate)
        {
            briefingDeparted.Clear();
            roundDeparted.Clear();
        }

        if (!closesGame)
        {
            _ = ReleaseRoundScreensAsync(roundState.RoundScreensVersion, ended.Value);
            logger.LogInformation(
                "Round {RoundNumber} ended because {Reason}; its stage has another round, so the room "
                + "runs no briefing, holds its result screens for their measured "
                + "{ScreensMilliseconds} ms pace and then opens the next round",
                ended,
                reason,
                MatchScreenTimingsUtils.RoundEndScreensMilliseconds);
            return true;
        }

        // A stage's last round is followed by the stage's own result screens —
        // the round result, the score graph and the stage result — and only then by
        // the next stage's briefing. The briefing is armed by the stage end, so no
        // run or count of it can reach a client still reading the played round's
        // result (`MatchScreenTimingsUtils.StageEndMilliseconds`).
        lock (gate)
        {
            stageBriefingPending = true;
        }

        _ = ReleaseStageEndAsync(roundState.StageEndVersion, ended.Value);

        logger.LogInformation(
            "Round {RoundNumber} ended because {Reason}; its stage is over, so its result screens run for "
            + "{StageEndMilliseconds} ms and the next briefing begins after them",
            ended,
            reason,
            MatchScreenTimingsUtils.StageEndMilliseconds);
        return true;
    }

    /// <summary>
    /// Releases the briefing the stage's result screens are holding, once they have
    /// run: the stage end closes and the played round's clock goes to zero.
    /// </summary>
    /// <remarks>
    /// It runs beside the round's own waits rather than inside the handler that
    /// closed the round, because the kill feed behind that handler must not wait out
    /// a result screen. A room that emptied, or a round that closed after this one,
    /// is not this stage end: <see cref="MatchRoundStateService.EndStageEnd"/> checks
    /// the identity and leaves it alone.
    /// </remarks>
    /// <param name="version">The stage end's version, as the close numbered it.</param>
    /// <param name="roundNumber">The stage's last round, for the log.</param>
    private async Task ReleaseStageEndAsync(int version, int roundNumber)
    {
        try
        {
            await Task.Delay(MatchScreenTimingsUtils.StageEndMilliseconds);
            if (!roundState.EndStageEnd(version))
            {
                return;
            }

            // The briefing is armed only once the screens are over: its run and its
            // countdown are the next thing the client is shown, and arming them at
            // the close let either reach it while the stage result was still up.
            BeginBriefing(afterStageScreens: true);

            // The row is what a joining client stages, and the briefing behind
            // these screens is already on the next rotation entry. Waiting for the
            // heartbeat leaves a window in which the room names the played map while
            // its entry records carry the next map's spawns.
            await PublishBriefingStageAsync(roundNumber);

            logger.LogInformation(
                "The stage that closed with round {RoundNumber} has shown its result screens, so the "
                + "next briefing begins",
                roundNumber);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "The stage end that followed round {RoundNumber} ended without releasing the next briefing",
                roundNumber);
        }
        finally
        {
            lock (gate)
            {
                stageBriefingPending = false;
            }
        }
    }

    /// <summary>
    /// Opens the next round of a stage once the round that ended has shown its
    /// result screens.
    /// </summary>
    /// <remarks>
    /// <b>The room's post-screen reports end the screens and open the next
    /// round.</b> A client still reading the result screen reports before it
    /// leaves, and the room waits for every client's report before it opens the
    /// next round, with a bound so one client cannot hold it. The round opens
    /// under the sibling set, which is the one the reference plays the next round
    /// under (<c>mgo2-started-on-game-round1</c>: round 1 on <c>0x1254</c>, round
    /// 2 at t+215.632 on <c>0x1a54</c>). A room that emptied, or a round that
    /// opened while the screens ran, is not this release and is left alone.
    /// </remarks>
    /// <param name="version">The screens' version, as the close numbered it.</param>
    /// <param name="roundNumber">The round whose screens ended, for the log.</param>
    private async Task ReleaseRoundScreensAsync(int version, int roundNumber)
    {
        try
        {
            // **The room's post-screen report ends the screens; the measured 24.0 s
            // is the client's pace.** On 2026-11-26 the client reported its
            // map-loaded state at 16:08:01, and opening on a clock wrote the round's
            // state before that report, so the client never asked for its entry and
            // never spawned. The reference opens 0.19 s after the last report
            // (`mgo2-started-on-game-round1` round 2, t+215.632). Waiting here, with
            // the screens still armed so those reports are the screens' own, is the
            // same gate without a fixed hold in front of it.
            await WaitForRoundReportsAsync(roundNumber);
            if (!roundState.EndRoundScreens(version) || roundState.IsOpen || !roundState.HasRoundEnded)
            {
                return;
            }

            var opened = OpenRound(
                roundState.Generation.Sibling,
                "the result screens finished and the room's post-screen reports followed them");
            if (!opened)
            {
                logger.LogWarning(
                    "Round {RoundNumber}'s result screens finished but the next round of the stage could "
                    + "not open; the room stays on the closed round until a report opens one",
                    roundNumber);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "The round result screens that followed round {RoundNumber} ended without opening the next round",
                roundNumber);
        }
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
            // The clock is read and not waited out: a solo round's clock is held
            // while the room holds one player, so a wait of the round's whole
            // length would end a round whose timer never moved
            // (Rooms.RoomState.RoundStartSolo). Each poll re-reads what is left,
            // and a held clock keeps returning the same value without ending
            // anything.
            while (roundState.RoundNumber == roundNumber && roundState.IsOpen)
            {
                var remainingUnits = roundState.RemainingUnits;
                if (remainingUnits > 0)
                {
                    var remainingMilliseconds = remainingUnits
                        * MatchRecordBodiesUtils.ClockUnitMilliseconds;
                    await Task.Delay(Math.Clamp(remainingMilliseconds, 250, 1_000));
                    continue;
                }

                End(
                    $"round {roundNumber}'s time limit ran out after "
                    + $"{timeLimitMilliseconds} ms",
                    await results.ClockResultAsync(),
                    RoundOverReason.Clock);
                return;
            }
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
