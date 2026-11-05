using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The briefing half of the round opener: the count a room is shown while it is
/// briefed, the instant its briefing began, and the number that keeps one count
/// acting on its own briefing rather than on the one after it.
/// </summary>
/// <remarks>
/// The reports themselves are taken in <see cref="MatchStartRunService.OnJoinReport"/>,
/// which gates them on the room's membership, and the run each round's open
/// writes lives in <c>MatchStartRunService.Runs.cs</c>. This file is the wait
/// between the two: a briefing begins where the room's rounds do, counts the
/// room's own briefing time down, and ends when the client that was waiting on it
/// reports again after having left its screen.
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Begins the room's briefing: the wait whose count the report that reaches it
    /// arms.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A briefing begins where the room's rounds do, which is the first peer's join
    /// and every round's end, and each one gets a number so a deadline that was
    /// armed for it acts on it and not on whatever briefing is current when it
    /// lands.
    /// </para>
    /// <para>
    /// <b>Its time is armed by the report that reaches it and not here.</b> The two
    /// are far apart on a live join: the join is answered when the client reaches
    /// the room, and the client reports when it has loaded the map and is reading
    /// its briefing, fifty seconds later in the capture this was measured on
    /// (<c>myserver12.pcapng</c>). A briefing armed at the join is therefore spent
    /// before the player it was armed for has arrived — that client's report at
    /// t+54.050 was followed by its round 8 s later, where both reference
    /// briefings give the reporting client 28 s and 118 s — and a briefing that no
    /// player answers arms nothing at all, which is the contract
    /// <see cref="OnJoinReport"/> already owes it.
    /// </para>
    /// <para>
    /// <b>What is armed where, exactly.</b> The report that opens the briefing arms
    /// the count its peers are sent and nothing else (<see cref="BeginBriefingClock"/>);
    /// the report that completes the room's reports starts that count moving
    /// (<see cref="StartBriefingCount"/>). A room of one is complete at its own
    /// report, and a room of two or more at its second; a room no player reported in
    /// has neither, which is the one case that is not timed at all.
    /// </para>
    /// </remarks>
    private void BeginBriefing()
    {
        var settings = matchSettings.FindPublished();
        if (settings is null)
        {
            // No published room is no round to open and no briefing time to time
            // it against; the reports that arrive will open nothing until it exists.
            logger.LogDebug(
                "A briefing began before this host's room was published; no briefing clock runs for it");
            return;
        }

        lock (gate)
        {
            briefingNumber++;
            lastJoinReportIdentifier = null;
            briefingGeneration = null;
            briefingReportInstant = null;
            countStartedBriefing = null;
        }

        // The clock a briefing is held on: the whole time of the round it precedes
        // for a room that has not played yet — the reference's fresh room writes
        // `01 60 09`, its five-minute round, through its briefing
        // (`mgo2-started-briefing-then-solo-game` t+57.626 to t+170.366) — and zero
        // once a round has played, because the round that ended is over.
        // `mgo2-started-on-game-round1` is the measurement: every round end of it
        // writes `01 00 00` through the briefing that follows, and the next round
        // opens on its whole 2 400 only when it opens
        // (`MatchRoundStateService.BriefingClockUnits`).
        roundState.NoteBriefingClock(
            roundState.HasRoundEnded
                ? 0
                : settings.Round(roundState.RoundNumber + 1).TimeLimitUnits);

        logger.LogDebug(
            "A briefing runs for {BriefingTime} minute(s); its count is armed by the report that reaches "
            + "it and starts once the room's players have reported, so a room of one is timed by its own",
            settings.BriefingTimeMinutes);
    }

    /// <summary>
    /// Units of the round clock a briefing is held on: the whole time of the round
    /// the briefing precedes, or zero once the room has played a round.
    /// </summary>
    /// <remarks>
    /// <b>It is the clock the briefing's run carries, read for the record beside a
    /// publication.</b> A briefing's peers are sent the round's whole clock in its
    /// run and in every player-state record beside a ready publication — the one
    /// capture holding the latter writes <c>60 09</c> = 2 400 there, and the same
    /// session opens the round behind that briefing at 2 400 (t+572.4 of
    /// <c>working-server-log.pcapng</c>). Both read
    /// <see cref="MatchRoundStateService.BriefingClockUnits"/>, so a record cannot
    /// carry a clock the run it travels with does not.
    /// </remarks>
    public int BriefingRoundUnits => roundState.BriefingClockUnits;

    /// <summary>
    /// Arms the count a briefing's peers are sent, at the join report that reaches
    /// the briefing: the countdown record its client's briefing screen shows. It is
    /// held at the room's whole briefing time until the room's reports are complete.
    /// </summary>
    /// <remarks>
    /// <b>The count is shown before the room can be timed.</b> A client that has
    /// reported is reading its briefing screen and is owed the count that screen
    /// shows, which the reference writes as the room's whole briefing time and holds
    /// there while the room is still waiting for a player to report
    /// (<see cref="BriefingCountdownService.Remaining"/>). What moves it is the
    /// report that completes the room (<see cref="StartBriefingCount"/>): a room of
    /// one moves it at its own.
    /// </remarks>
    /// <param name="generation">
    /// The set the briefing's first report arrived on, whose operations stream the
    /// countdown is written on and restarted.
    /// </param>
    private void BeginBriefingClock(MatchRecordGenerationUtils generation)
    {
        var settings = matchSettings.FindPublished();
        if (settings is null)
        {
            logger.LogDebug(
                "The room's briefing heard its first join report but this host's room is not published, so "
                + "no briefing count is armed for it");
            return;
        }

        // The briefing opens the operations stream of the set its first report
        // named: the capture's countdown begins the stream and the run behind it
        // takes the same stream's first values, so the restart belongs to the
        // report and not to the briefing's preparation, which happens before any
        // report names a set.
        sequences.Restart(generation.Operations);

        // The countdown goes to the peers the briefing is shown to, so it is armed
        // with it: a client waiting out its own briefing timer is told how much of
        // it is left. It is the seven-byte 0x0b operations record, counting this
        // same briefing time down in seconds (<see cref="BriefingCountdownBodiesUtils"/>),
        // and it rides the generation this report named rather than a fixed one.
        countdown.Begin(DateTimeOffset.UtcNow, settings.BriefingTimeMinutes, generation.Operations);

        logger.LogDebug(
            "The room's briefing heard its first join report, so its {BriefingTime}-minute count is "
            + "armed: it is held at the whole briefing time until the room's reports are complete",
            settings.BriefingTimeMinutes);
    }

    /// <summary>
    /// Starts the count a briefing's peers are sent, at the report that completes
    /// the room: the seconds the client's briefing screen shows running down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A room of one holds its count.</b> Its count is held at the room's whole
    /// briefing time and nothing moves, which is what the reference host writes for
    /// a sole client (<c>mgo2-game-started-on-briefing</c> holds <c>0b 3c</c> for
    /// tens of seconds). The second player's report is what gives the room somebody
    /// else to wait for, and it starts the count there; a room that reports no
    /// player starts nothing.
    /// </para>
    /// <para>
    /// <b>The count opens no round.</b> What opens one is the report the client
    /// sends once it has left its briefing screen — the weapon select it shows next,
    /// then its spawn — which <see cref="OnJoinReport"/> takes
    /// (<c>docs/protocol/BRIEFING_FLOW.md</c> §2). The count is what the screen
    /// shows and the round waits on the client's own timer, not on this record.
    /// </para>
    /// <para>
    /// <b>The count and the briefing are one briefing time, so they start
    /// together.</b> A countdown started here and the briefing's own start in
    /// <see cref="BeginBriefingClock"/> are two answers to "how long is left", and
    /// they are the same room setting.
    /// </para>
    /// </remarks>
    /// <param name="briefing">The briefing the count belongs to.</param>
    private void StartBriefingCount(int briefing)
    {
        var settings = matchSettings.FindPublished();
        if (settings is null)
        {
            logger.LogDebug(
                "The room's reports are complete but this host's room is not published, so no briefing "
                + "count is started for it");
            return;
        }

        countdown.StartCounting(DateTimeOffset.UtcNow);

        logger.LogInformation(
            "The room's reports are complete at {ReportedCount} of {PlayerCount} player(s), so its "
            + "{BriefingTime}-minute briefing counts down from now; the round opens on the report that "
            + "follows the briefing",
            briefingStates.JoinedCount,
            briefingStates.PlayerCount,
            settings.BriefingTimeMinutes);
    }
}
