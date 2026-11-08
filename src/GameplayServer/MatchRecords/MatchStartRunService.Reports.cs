using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The report half of the round opener: the join report a client sends when it
/// has loaded the room's map, and the round the report that follows the
/// briefing opens.
/// </summary>
/// <remarks>
/// The briefing's own count, its deadline and the number that keeps one
/// deadline acting on its own briefing live in
/// <c>MatchStartRunService.Briefing.cs</c>, and the runs a round is opened with
/// in <c>MatchStartRunService.Runs.cs</c>: this file is the report that arrives
/// between them.
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// The peers that have sent the report they send *after* the briefing's
    /// closing zero, which is the one a player sends leaving its briefing screen.
    /// </summary>
    /// <remarks>
    /// <b>It takes the room, and not the one report, to open a round.</b> The
    /// reference opens on the room's **last** such report and not on its first:
    /// <c>mgo2-game-started-on-briefing</c>'s last player reports at t+196.828 and
    /// the round opens 1.543 s later, and
    /// <c>mgo2-game-joined-when-red-team-then-briefing</c>'s client reports at
    /// t+95.464 with the round following at t+123.774 — twenty-eight seconds
    /// later — *because the room's other players were still arriving*
    /// (<c>docs/protocol/BRIEFING_FLOW.md</c> §2). A host that opens on the first
    /// one starts the round over clients still reading their screens, which is the
    /// <c>PLEASE WAIT</c> the briefing draws while it waits for the room.
    /// </remarks>
    private readonly HashSet<string> briefingDeparted = [];

    /// <summary>
    /// Takes a client's join report and, when it is the one the room's round waits
    /// for, opens the round under the set that report arrived on. It is also where
    /// a briefing's clock is armed with the room it was given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a join and not a readiness.</b> The record says the client has
    /// loaded the room and its map and is on the briefing screen
    /// (<see cref="Rooms.BriefingStateService.JoinReport"/>); a player's own
    /// readiness arrives on a different record entirely, on the operations
    /// stream, and what opens a round is not that record but the report the client
    /// sends once the briefing has written its closing zero
    /// (<see cref="BriefingCountdownService.HasEnded"/>). That report is the client
    /// leaving its screen — the weapon select it shows next, then its spawn — and
    /// the reports before it are the client still waiting on that screen. The ready
    /// records an earlier reading waited on do not exist:
    /// `working-server-4-sessions.pcapng` holds no `1f &lt;slot&gt; &lt;state&gt;`
    /// anywhere in ninety seconds of briefing, and its round opens on the report.
    /// </para>
    /// <para>
    /// <b>The briefing is answered first and the round second.</b> A briefing
    /// hears two join reports a round. The first names the set the briefing itself is
    /// written under — the client is still being briefed, so what it is served is
    /// that briefing's run and not a round (<see cref="MatchStartRunService.Runs"/>)
    /// — and the second, arriving once the room's briefing has written its closing
    /// zero, is the client leaving its briefing screen and asks for the round the
    /// captures show playing after it (<c>docs/protocol/BRIEFING_FLOW.md</c> §2).
    /// Every report in between
    /// is the client still on that screen, re-sending while its own timer runs; it is
    /// answered with the briefing's run and opens nothing.
    /// </para>
    /// <para>
    /// <b>The clock is armed here, and only as far as the room has players for.</b>
    /// A join report is not a readiness, so it arms no deadline: a room of one is
    /// frozen, its count held at the room's whole briefing time and its briefing
    /// never out (§17 of <c>docs/analysis/join-stall-after-mode2.md</c>). The report
    /// that gives the room a second player is the one its timer belongs to, and
    /// that is where the count is started and the deadline armed
    /// (<see cref="StartBriefingCount"/>), for the same reason the briefing's own
    /// preparation waits: the join that made the room can be a minute ahead of the
    /// client that has to read the briefing.
    /// </para>
    /// <para>
    /// <b>The gate is here and not only in the handler.</b> A round opens for a
    /// room, so "has the briefing been waited out" is a condition of opening one
    /// rather than a step a caller is asked to remember: the handler that takes a
    /// report is one caller today, and a gate kept there would let any other caller
    /// start a round on a room that is still being briefed. It is also the invariant
    /// a second round depends on, because a round's end clears the states and the
    /// next report to arrive is a single player's.
    /// </para>
    /// </remarks>
    /// <param name="reportIdentifier">Identifier the join report arrived on.</param>
    /// <param name="reporter">
    /// Endpoint the report arrived from, which is the peer the round counts as
    /// having left its briefing screen.
    /// </param>
    /// <returns><c>true</c> when this call opened the round.</returns>
    public bool OnJoinReport(ushort reportIdentifier, string reporter)
    {
        var generation = MatchRecordGenerationUtils.OfJoinReport(reportIdentifier);

        int armed;
        bool first;
        bool startsBriefing;
        lock (gate)
        {
            lastJoinReportIdentifier = reportIdentifier;
            joinReportCount++;
            armed = briefingNumber;
            first = briefingGeneration is null;
            if (first)
            {
                // The briefing's own set: the first report names it, and the run
                // the worker writes for the briefing is written under it. Its
                // instant is kept with the set because the run is spaced from it
                // as well as from the entry (EntryRunDwellRemaining): a client
                // may enter before it reports.
                briefingGeneration = generation;
                briefingReportInstant = DateTimeOffset.UtcNow;

                // A new briefing's own reports: the last one's departures do not
                // open this one's round.
                briefingDeparted.Clear();
            }

            // The briefing's timer is the room's own, and it belongs to the room
            // the reports describe: its count is started by the second player's
            // report, the one that gives a frozen room somebody else to wait for. A
            // room of one holds its count at the room's whole briefing time and
            // nothing runs out, so its round is opened by the report that follows
            // the briefing's closing zero (BriefingCountdownService.HasEnded). One
            // briefing starts it once, whatever the reports after that one say.
            startsBriefing = countStartedBriefing != armed
                && briefingStates.PlayerCount >= CountingJoinReportCount
                && briefingStates.JoinedCount >= CountingJoinReportCount;
            if (startsBriefing)
            {
                countStartedBriefing = armed;
            }
        }

        if (first)
        {
            // The count a peer is sent starts at its first report: held at the
            // room's whole briefing time until a second player reports, and its own
            // end only once it moves (BriefingCountdownService).
            BeginBriefingClock(generation);

            logger.LogInformation(
                "The room's briefing heard its first join report on 0x{Identifier:x4}; the briefing is "
                + "written under that set; the round opens on the report that follows it",
                reportIdentifier);
        }

        if (startsBriefing)
        {
            StartBriefingCount(armed);
        }

        if (first)
        {
            return false;
        }

        // Every report before the room's briefing has closed is the client still on
        // its briefing screen, re-sending while its own timer runs; the round is not
        // what it is asking for yet. What closes a briefing is its closing zero,
        // `0b 00 00 <ready set> 00 00` — the record a client's screen ends on — and
        // not the room's briefing time: a room of one holds its count at the whole
        // time and sits on that screen far past it (`briefing-then-solo-game` arms
        // its count at t+12.9 and writes its zero at t+145.1, when its only player
        // readied), so a clock read here opened the round minutes before the client
        // left the screen, and the report that asked for the round found one open.
        if (!countdown.HasEnded && !NoBriefingToWaitFor)
        {
            logger.LogDebug(
                "A report arrived on 0x{Identifier:x4} before the room's briefing wrote its closing zero; "
                + "the client is on its briefing screen and the round waits for the report it sends when "
                + "it leaves it",
                reportIdentifier);
            return false;
        }

        // The report that follows the briefing is the client leaving its screen for
        // the weapon select and its spawn, which is the one record a round opens on.
        // Measured: a client reports e1 08 at t+304.686, writes its own briefing rows
        // behind it, sends nothing for ninety seconds, reports e1 08 again at
        // t+395.811, and its host opens the round 0.2 s later with the clock at 2 400
        // (`working-server-4-sessions.pcapng`). No ready record is in that capture.
        //
        // And it is the room's last such report, not its first. The reference waits
        // for the rest: `joined-when-red-team`'s client leaves its screen at
        // t+95.464 and the round opens at t+123.774, because the room's other
        // players were still arriving (`docs/protocol/BRIEFING_FLOW.md` §2), and
        // `started-on-briefing` opens 1.543 s behind its last report. A round opened
        // on the first departure starts over the clients still on the screen, which
        // is what the briefing's `PLEASE WAIT` is drawn for.
        //
        // The count is over the players that have reported at all, not over the
        // room's population: a bot is in the room without a screen, and a peer that
        // never reported is a peer the briefing is not waiting on.
        if (briefingStates.PlayerCount > 1)
        {
            int departed;
            lock (gate)
            {
                briefingDeparted.Add(reporter);
                departed = briefingDeparted.Count;
            }

            var reported = briefingStates.JoinedCount;
            if (departed < reported)
            {
                logger.LogDebug(
                    "The report on 0x{Identifier:x4} from {Reporter} is one of the room's departures from "
                    + "its briefing screen; {Departed} of {Reported} reported player(s) have left it and the "
                    + "round waits for the last",
                    reportIdentifier,
                    reporter,
                    departed,
                    reported);
                return false;
            }
        }

        return OpenRound(
            generation,
            $"the report that followed the room's briefing arrived on 0x{reportIdentifier:x4}");
    }

    /// <summary>
    /// Whether the room has no briefing to wait for, so a report is itself what a
    /// round opens on.
    /// </summary>
    /// <remarks>
    /// A room with no briefing time writes no count and no closing zero
    /// (<see cref="BriefingCountdownService.Begin"/> arms nothing without one), so
    /// <see cref="BriefingCountdownService.HasEnded"/> never turns and the zero
    /// cannot be what a report waits for. A host whose room is not published yet has
    /// the same answer, and no round to open for it
    /// (<see cref="OpenRound"/> resolves nothing without a room).
    /// </remarks>
    private bool NoBriefingToWaitFor =>
        matchSettings.FindPublished() is not { BriefingTimeMinutes: > 0 };
}
