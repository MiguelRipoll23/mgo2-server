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
    ILogger<MatchStartRunService> logger)
{
    private readonly Lock gate = new();
    private int briefingNumber;
    private ushort? lastReportIdentifier;

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
        var round = roundState.Open(generation, nextRound);
        if (round is null)
        {
            // A client reports until it is answered, so a second report for a
            // round already playing is the same round and not another one.
            return false;
        }

        // The round's score starts empty: the previous round's kills and deaths
        // are not this one's, and the scoreboard is a per-round board.
        score.Reset();

        logger.LogInformation(
            "Opening round {RoundNumber} for the room: the report arrived on 0x{Identifier:x4}, "
            + "so the round plays under the sibling-form set if that is the other generation",
            round,
            reportIdentifier);

        _ = EndWhenTimeRunsOutAsync(round.Value, RoundClockUtils.TimeLimitMilliseconds(nextRound));
        return true;
    }

    /// <summary>
    /// Begins the room's briefing: the wait for every player to report, under a
    /// deadline of the room's own briefing time.
    /// </summary>
    /// <remarks>
    /// A briefing begins where the room's rounds do, which is the first peer's join
    /// and every round's end, and each one gets a number so a deadline that was
    /// armed for it acts on it and not on whatever briefing is current when it
    /// lands.
    /// </remarks>
    private void BeginBriefing()
    {
        var settings = matchSettings.FindPublished();
        if (settings is null)
        {
            // No published room is no round to open and no briefing time to time
            // it against; the reports that arrive will open nothing until it exists.
            logger.LogDebug(
                "A briefing began before this host's room was published; no deadline is armed for it");
            return;
        }

        int briefing;
        lock (gate)
        {
            briefing = ++briefingNumber;
            lastReportIdentifier = null;
        }

        _ = OpenWhenBriefingEndsAsync(briefing, settings.BriefingTimeMinutes);
    }

    /// <summary>
    /// Opens the room's round when its briefing time runs out without every player
    /// having reported.
    /// </summary>
    /// <remarks>
    /// <b>It is the client's own wait that makes this a deadline.</b> A client in a
    /// briefing waits for the host's round and stops waiting when its own briefing
    /// time is up, so a room that waited on a player who never reports leaves the
    /// players it did hear from past the end of their timer with nothing to play
    /// (<c>docs/GAMEPLAY_SERVER.md</c> §4.2). The round it opens is the round those
    /// reports asked for — same rule, same map, same clock — and the player still to
    /// report is served it as a late joiner is, which is what §4.1 measures a room
    /// doing with a peer that arrives mid-round.
    /// </remarks>
    /// <param name="briefing">Briefing this deadline belongs to.</param>
    /// <param name="briefingMinutes">Minutes the briefing runs for.</param>
    private async Task OpenWhenBriefingEndsAsync(int briefing, int briefingMinutes)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(briefingMinutes));

            if (roundState.IsOpen)
            {
                // A report opened the round inside the briefing, which is what the
                // deadline is there to save the room from waiting out.
                return;
            }

            ushort reportIdentifier;
            lock (gate)
            {
                if (briefingNumber != briefing || lastReportIdentifier is not { } reported)
                {
                    // Another briefing has begun — its own deadline acts for it — or
                    // nothing in this one has answered the room.
                    return;
                }

                reportIdentifier = reported;
            }

            logger.LogInformation(
                "The room's {BriefingTime}-minute briefing ran out with {PendingCount} player(s) still to "
                + "report; the round opens for the players that reported, on 0x{Identifier:x4}",
                briefingMinutes,
                ready.PendingCount,
                reportIdentifier);
            OpenRound(reportIdentifier, waitForEveryone: false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "The briefing deadline for briefing {Briefing} ended without opening a round",
                briefing);
        }
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
            + "round opens when every player has reported again or the briefing runs out",
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
