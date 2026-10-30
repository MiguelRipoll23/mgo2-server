using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The briefing half of the round opener: the count a room is shown while it is
/// briefed, the deadline that ends the wait for its players to ready, and the
/// number that keeps one deadline acting on its own briefing rather than on the
/// one after it.
/// </summary>
/// <remarks>
/// The reports themselves are taken in <see cref="MatchStartRunService.OnJoinReport"/>,
/// which gates them on the room's membership, and the run each round's open
/// writes lives in <c>MatchStartRunService.Runs.cs</c>. This file is the wait
/// between the two: a briefing begins where the room's rounds do and ends when
/// its own time runs out.
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Begins the room's briefing: the wait for every player to ready, whose
    /// count and deadline the report that completes the room arms.
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
    /// <see cref="OpenWhenBriefingEndsAsync"/> already owes it.
    /// </para>
    /// <para>
    /// <b>What is armed where, exactly.</b> The report that opens the briefing arms
    /// the count its peers are sent and nothing else (<see cref="BeginBriefingClock"/>);
    /// the report that completes the room's reports starts that count moving and
    /// arms the deadline that ends it (<see cref="ArmBriefingDeadline"/>). A room of
    /// one is complete at its own report, and a room of two or more at its second;
    /// a room no player reported in has neither, which is the one case that is not
    /// timed at all.
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
            armedDeadlineBriefing = null;
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
            roundState.RoundNumber > 0
                ? 0
                : settings.Round(roundState.RoundNumber + 1).TimeLimitUnits);

        logger.LogDebug(
            "A briefing runs for {BriefingTime} minute(s); its count is armed by the report that reaches "
            + "it and starts once the room's players have reported, so a room of one is timed by its own",
            settings.BriefingTimeMinutes);
    }

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
    /// (<see cref="BriefingCountdownService.Remaining"/>). What moves it — and what
    /// arms the deadline behind it — is the report that completes the room
    /// (<see cref="ArmBriefingDeadline"/>): a room of one moves it at its own.
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
    /// Arms the deadline that ends a briefing, at the report that completes the
    /// room: the count that moves from there, and the wait that opens the round
    /// once the room's own briefing time is out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A room of one is not armed at all.</b> Its count is held at the room's
    /// whole briefing time and nothing runs out, which is what the reference host
    /// writes for a sole client (<c>mgo2-game-started-on-briefing</c> holds
    /// <c>0b 3c</c> for tens of seconds), and the only thing that starts a solo
    /// round is that player's own ready (<c>mgo2-started-briefing-then-solo-game</c>
    /// readies at t+165.780 and asks for its round at t+173.438). A deadline armed
    /// on the sole report is a round nobody asked for
    /// (<c>docs/analysis/join-stall-after-mode2.md</c> §17). The second player's
    /// report is what gives the room somebody else to wait for, and it is armed
    /// there; a room that reports no player is armed by nothing.
    /// </para>
    /// <para>
    /// <b>The count and the deadline are one briefing time, so they start
    /// together.</b> A countdown armed here and a deadline armed elsewhere are two
    /// answers to "how long is left", and the capture this was measured on is what
    /// that looks like: the deadline expired eight seconds into the client's own
    /// briefing and the round it opened held its clock at the room's whole seven
    /// minutes for the briefing's nominal minute, so the client was shown a frozen
    /// 7:00 from t+62.371 to t+123.973 while its briefing had lasted eight seconds
    /// (<see cref="BriefingCountdownService.TimeRemaining"/>).
    /// </para>
    /// </remarks>
    /// <param name="briefing">The briefing the deadline belongs to.</param>
    private void ArmBriefingDeadline(int briefing)
    {
        var settings = matchSettings.FindPublished();
        if (settings is null)
        {
            logger.LogDebug(
                "The room has no player left to report but this host's room is not published, so no "
                + "briefing time is armed for it");
            return;
        }

        countdown.StartCounting(DateTimeOffset.UtcNow);
        _ = OpenWhenBriefingEndsAsync(briefing, settings.BriefingTimeMinutes);

        logger.LogInformation(
            "The room's reports are complete at {ReportedCount} of {PlayerCount} player(s), so its "
            + "{BriefingTime}-minute briefing runs from now: the countdown moves and the round opens "
            + "when the briefing's time is out",
            briefingStates.JoinedCount,
            briefingStates.PlayerCount,
            settings.BriefingTimeMinutes);
    }

    /// <summary>
    /// Opens the room's round when its briefing time runs out without every player
    /// having readied.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the client's own wait that makes this a deadline.</b> A client in a
    /// briefing waits for the host's round and stops waiting when its own briefing
    /// time is up, so a room that waited on a player who never readies leaves the
    /// players it did hear from past the end of their timer with nothing to play
    /// (<c>docs/GAMEPLAY_SERVER.md</c> §4.2). <b>It is armed where the room's reports
    /// are complete</b> (<see cref="ArmBriefingDeadline"/>): its time is the room's
    /// own, and a room of one whose report armed nothing kept its count frozen and
    /// never opened a round at all. The round it opens is the round those reports
    /// asked for — same rule, same map, same clock — and the player still to ready is
    /// served it as a late joiner is, which is what §4.1 measures a room doing with a
    /// peer that arrives mid-round.
    /// </para>
    /// <para>
    /// A briefing nothing answered opens nothing: a round is still something a
    /// report asks for. And a briefing whose reporters have all left opens nothing
    /// either: the round is opened for players, and a room that has reaped every one
    /// of them has no report left to open one under.
    /// </para>
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
                if (briefingNumber != briefing || lastJoinReportIdentifier is not { } reported)
                {
                    // Another briefing has begun — its own deadline acts for it — or
                    // nothing in this one has answered the room.
                    return;
                }

                reportIdentifier = reported;
            }

            if (briefingStates.JoinedCount == 0)
            {
                // A round is opened for players, and every player this briefing
                // heard from has left the room: their states are dropped when
                // their sessions are reaped, so there is no report left to open
                // one under. Without this the deadline would start a round for a
                // room nobody is in, and the next player to arrive would be
                // served that round instead of a briefing of its own.
                logger.LogDebug(
                    "The room's {BriefingTime}-minute briefing ran out with no player left that had "
                    + "reported; no round is opened",
                    briefingMinutes);
                return;
            }

            logger.LogInformation(
                "The room's {BriefingTime}-minute briefing ran out with {PendingReadyCount} player(s) still "
                + "to ready; the round opens for the players that readied, on 0x{Identifier:x4}",
                briefingMinutes,
                briefingStates.PendingBriefingReadyCount,
                reportIdentifier);
            OpenRound(
                MatchRecordGenerationUtils.OfJoinReport(reportIdentifier),
                $"the briefing ran out with its last join report on 0x{reportIdentifier:x4}",
                waitForEveryone: false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "The briefing deadline for briefing {Briefing} ended without opening a round",
                briefing);
        }
    }
}
