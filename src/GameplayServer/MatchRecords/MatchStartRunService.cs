using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Opens a round for the room and ends it again.
/// </summary>
/// <remarks>
/// <para>
/// <b>A round opens on the room's reports and never on a clock.</b> Measured
/// over every capture that holds a round, each one begins within 0.6–5.5 s of a
/// client's readiness report and none begins without one
/// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2). A host that opened its round
/// on the measured interval from the roster got the timing right and the
/// behaviour wrong: the client sat in its ready state and reported twenty-three
/// times over two minutes while the run it had already been sent went
/// unrepeated. The gate here is therefore the reports, and the round opens when
/// the room has one from every player it holds — or when its briefing runs out
/// first, which is the next paragraph.
/// </para>
/// <para>
/// <b>A report of the room's only client opens the round here, and the
    /// earlier reading that held it back is withdrawn.</b> This file used to wait
    /// for a second player on the reading that "a room's first player is briefed
    /// rather than started" — a reading it supported by noting that no capture
    /// holds a room whose sole player reported. That is true of the captures and
    /// it is not a rule: in every one of them a report opens the round, and in
    /// the two captures this host serves the room holds exactly one *client* (the
    /// host is not a reporter), where the gate left the report unanswered for the
    /// whole briefing time. What the client does with an unanswered report is
    /// measured and it is not waiting: it re-sends the identical report every
    /// 2.5 s (`myserver2.pcapng` t+49.5, 52.0, 54.9, 57.5, 60.2), where a report a
    /// capture answers is sent once and answered within 0.6–5.5 s. So a room whose
    /// clients have all reported opens its round then, however many of them there
    /// are.
    /// </para>
/// <para>
/// <b>How many reports are needed is a deadline, not a rule.</b> A client waits
/// in its briefing for the host's round either until every player is ready or
/// until its own briefing time is up, so a room that waited for a player who never
/// reports leaves the others past the end of their timer with no round at all
/// (<c>docs/GAMEPLAY_SERVER.md</c> §4.2). The first report of a briefing therefore
/// arms a deadline of the room's own briefing time, and a briefing that runs out
/// opens the round for the players it did hear from — under the set the last of
/// their reports arrived on. A briefing no player has answered opens nothing: a
/// round is still something a report asks for, and a room nothing has answered has
/// no set to open one under.
/// </para>
/// <para>
/// <b>What ends a round.</b> The round's own clock, which is the length its
/// operations record carries, and that length is the room's own: the round the
/// rotation names, timed by this instance's settings for it
/// (<see cref="MatchSettingsService"/>, and
/// <c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2 for the measurement). A goal is
/// not visible on this channel — the objective is scored in the round's own state
/// block and the end-of-round report travels over the lobby's TCP channel — so
/// <see cref="End"/> is the seam a goal would use, and no round ends early for
/// one here.
/// </para>
/// <para>
/// <b>The room's settings are this instance's own.</b> A round carries a rule, a
/// map and a clock, and the dedicated host is the one that chose all three: they
/// are its configuration (<see cref="MatchSettingsService"/>), the same settings
/// its room row is written from, so a round is opened on the very rotation the
/// client was handed. There is nothing to read to open one, and a host whose room
/// is not published yet opens no round at all — the client goes on reporting,
/// which is the state a server with nothing to serve should leave it in.
/// </para>
/// </remarks>
/// <param name="roundState">The room's round, which this service opens and closes.</param>
/// <param name="ready">The room's briefing states, which a round's end re-arms.</param>
/// <param name="matchSettings">Resolver of the room's own rule, map and clock.</param>
/// <param name="lobbySlots">
/// Reader of the room's own players by their slot in its 24-entry player table
/// and the side each is on: the run's lobby-slot pair names them, so a room whose
/// players were never read is a run that tells its clients nobody is here.
/// </param>
/// <param name="score">
/// The host's own score for the round, cleared when a round opens.
/// </param>
/// <param name="results">
/// The round's result: the winner a clock's end is written with, and the state
/// write that publishes it once the round has closed.
/// </param>
/// <param name="roster">
/// The room's roster exchange, whose counter numbers the run's records: the
/// lobby-slot pair takes the two values after the join's own records and the
/// burst's, which is what keeps the pair consecutive and every record of that
/// identifier in one ascending sequence.
/// </param>
/// <param name="logger">Logger of this service.</param>
public sealed partial class MatchStartRunService(
    MatchRoundStateService roundState,
    Rooms.PlayerReadyService ready,
    MatchSettingsService matchSettings,
    Rooms.LobbySlotService lobbySlots,
    Rooms.RoomRosterService roster,
    MatchScoreService score,
    MatchResultService results,
    BriefingCountdownService countdown,
    MatchRecordSequenceService sequences,
    ILogger<MatchStartRunService> logger)
{
    private readonly Lock gate = new();

    private int briefingNumber;
    private ushort? lastReportIdentifier;

    /// <summary>
    /// Identifier the room's briefing last heard a report on, or <c>null</c> while
    /// the briefing current has heard from nobody. It is the generation the round
    /// will open under, and so the one the ready set is published under while the
    /// room waits (<see cref="BriefingReadyPublishService"/>).
    /// </summary>
    public ushort? LastReportIdentifier
    {
        get
        {
            lock (gate)
            {
                return lastReportIdentifier;
            }
        }
    }

    /// <summary>
    /// Opens the room's round, under the identifier set the report that asked
    /// for it arrived on, once every player in the room has reported.
    /// </summary>
    /// <remarks>
    /// <b>The gate is here and not only in the handler.</b> A round opens for a
    /// room, so "is the room ready" is a condition of opening one rather than a
    /// step a caller is asked to remember: the handler that takes a report is one
    /// caller today, and a gate kept there would let any other caller start a
    /// round on a room that is still being briefed. It is also the invariant a
    /// second round depends on, because a round's end clears the states and the
    /// next report to arrive is a single player's.
    /// </remarks>
    /// <param name="reportIdentifier">Identifier the readiness report arrived on.</param>
    /// <returns><c>true</c> when this call opened the round.</returns>
    public bool Open(ushort reportIdentifier) =>
        OpenRound(reportIdentifier, waitForEveryone: true);

    /// <summary>
    /// Opens the room's round, under the identifier set a report arrived on.
    /// </summary>
    /// <param name="reportIdentifier">Identifier the readiness report arrived on.</param>
    /// <param name="waitForEveryone">
    /// Whether the round waits for every player in the room to have reported. It
    /// is <c>false</c> for the briefing's deadline alone, which is the one caller
    /// allowed to open a round with players still to report.
    /// </param>
    /// <returns><c>true</c> when this call opened the round.</returns>
    private bool OpenRound(ushort reportIdentifier, bool waitForEveryone)
    {
        // The identifier is kept for the briefing's deadline, which opens the round
        // for the players that did report when not all of them did. It is the set
        // those reports arrived under, and a round opens under the set its own
        // client is on rather than under one this host picked.
        lock (gate)
        {
            lastReportIdentifier = reportIdentifier;
        }

        // The gate is every player in the room having reported, and it is the
        // room's own population rather than a floor: a room of one client opens
        // on that client's report, which is what every capture does. A player
        // who never reports is still ended by the briefing's own deadline
        // (OpenWhenBriefingEndsAsync), which is the contract the class summary
        // gives it.
        if (waitForEveryone && !ready.AllReady)
        {
            logger.LogDebug(
                "A report arrived on 0x{Identifier:x4} but {PendingCount} player(s) in the room have not "
                + "reported; the round stays closed until they do or the briefing runs out",
                reportIdentifier,
                ready.PendingCount);
            return false;
        }

        // A round that is already playing is answered with the settings it opened
        // on — resolving them below is what a round about to open costs, and a
        // report the room has already answered does not pay it twice.
        var nextRound = roundState.RoundSettings ?? ReadRound();
        if (nextRound is null)
        {
            return false;
        }

        var generation = MatchRecordGenerationUtils.OfReadinessReport(reportIdentifier);

        // The clock a round opens on is whole, and it stays whole for as much of
        // the room's briefing as is still to run. A report opens a round while its
        // client is still being briefed — that is what the report is — so the
        // round's clock counts from the briefing's own end and not from the open;
        // a clock that counted from the open would run the round out under a
        // client still reading the briefing (MatchRoundStateService.RemainingUnits).
        var hold = countdown.Remaining(DateTimeOffset.UtcNow, out var remaining)
            ? (int)remaining.TotalMilliseconds
            : 0;

        var round = roundState.Open(generation, nextRound, hold);
        if (round is null)
        {
            // A client reports until it is answered, so a second report for a
            // round already playing is the same round and not another one.
            return false;
        }

        // The round opens its own operations stream. The run's three records take
        // the first values and the beat's clocks go on from there — the recorded
        // run carries 00 01 02 and its beat 03 — and the briefing's clock, which
        // wrote under the same identifier, is behind this point. A round whose
        // generation is the other one opens a stream of its own, because the
        // sibling bit is inside the keyed twelve
        // (<see cref="MatchRecordSequenceService.StreamOf"/>).
        sequences.Restart(generation.Operations);

        // The round's score starts empty: the previous round's kills and deaths
        // are not this one's, and the scoreboard is a per-round board.
        score.Reset();

        logger.LogInformation(
            "Opening round {RoundNumber} for the room: the report arrived on 0x{Identifier:x4}, "
            + "so the round plays under the sibling-form set if that is the other generation",
            round,
            reportIdentifier);

        _ = EndWhenTimeRunsOutAsync(
            round.Value,
            hold + RoundClockUtils.TimeLimitMilliseconds(nextRound));
        return true;
    }

    /// <summary>
    /// The round the room's own rotation names for the round that is about to
    /// open: what this host is configured to run.
    /// </summary>
    /// <remarks>
    /// The round number counted is the next one and not the one playing, because
    /// the rotation is walked by the round that opens: a room whose first round is
    /// playing answers here with its second entry. Nothing is resolved while a
    /// round is already open — the report that would open one has been answered,
    /// and a second resolution would only be settings nobody is asking for.
    /// </remarks>
    /// <returns>The round to open, or <c>null</c> when the room has none published.</returns>
    private MatchRotationEntry? ReadRound()
    {
        var settings = matchSettings.FindPublished();
        if (settings is null)
        {
            logger.LogWarning(
                "This host's room is not published, so no round is opened for it; the client goes on "
                + "reporting until it is");
            return null;
        }

        var round = settings.Round(roundState.RoundNumber + 1);
        logger.LogDebug(
            "Match {GameIdentifier} ({GameName}) plays round {RoundNumber} on rule {Rule}, map {Map} for "
            + "{TimeLimitMinutes} minutes; the room's briefing lasts {BriefingTimeMinutes} minutes",
            settings.GameIdentifier,
            settings.Name,
            roundState.RoundNumber + 1,
            round.Rule,
            round.Map,
            round.TimeLimitMinutes,
            settings.BriefingTimeMinutes);
        return round;
    }

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
    /// <b>The briefing it begins is the room's own.</b> The countdown, the deadline
    /// and the streams are re-armed where every briefing is armed
    /// (<see cref="BeginBriefing"/>), which is what makes the freshly reset room a
    /// room waiting for a game rather than one replaying a match nobody played.
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
