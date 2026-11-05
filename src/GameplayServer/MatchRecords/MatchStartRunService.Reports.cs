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
    /// stream, and it is that one which opens a round
    /// (<see cref="BriefingTimeElapsed"/>): the report that follows the briefing is
    /// the client leaving its screen — the weapon select it shows next, then its
    /// spawn — and the reports before it are the client still waiting on that
    /// screen. The ready records an earlier reading waited on do not exist:
    /// `working-server-4-sessions.pcapng` holds no `1f &lt;slot&gt; &lt;state&gt;`
    /// anywhere in ninety seconds of briefing, and its round opens on the report.
    /// </para>
    /// <para>
    /// <b>The briefing is answered first and the round second.</b> A briefing
    /// hears two join reports a round. The first names the set the briefing itself is
    /// written under — the client is still being briefed, so what it is served is
    /// that briefing's run and not a round (<see cref="MatchStartRunService.Runs"/>)
    /// — and the second, arriving once the room's briefing time is out, is the client
    /// leaving its briefing screen and asks for the round the captures show playing
    /// after it (<c>docs/protocol/BRIEFING_FLOW.md</c> §2). Every report in between
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
    /// <returns><c>true</c> when this call opened the round.</returns>
    public bool OnJoinReport(ushort reportIdentifier)
    {
        var generation = MatchRecordGenerationUtils.OfJoinReport(reportIdentifier);

        int armed;
        bool first;
        bool startsBriefing;
        lock (gate)
        {
            lastJoinReportIdentifier = reportIdentifier;
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
            }

            // The briefing's timer is the room's own, and it belongs to the room
            // the reports describe: its count is started by the second player's
            // report, the one that gives a frozen room somebody else to wait for. A
            // room of one holds its count at the room's whole briefing time and
            // nothing runs out, so its round is opened by the report the client
            // sends when its own briefing timer is done (BriefingTimeElapsed). One
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

        // Every report before the room's briefing time is out is the client still on
        // its briefing screen, re-sending while its own timer runs; the round is not
        // what it is asking for yet.
        if (!BriefingTimeElapsed(out var remaining))
        {
            logger.LogDebug(
                "A report arrived on 0x{Identifier:x4} with {Remaining} of the room's briefing still to "
                + "run; the client is on its briefing screen and the round waits for the report it sends "
                + "when it leaves it",
                reportIdentifier,
                remaining);
            return false;
        }

        // The report that follows the briefing is the client leaving its screen for
        // the weapon select and its spawn, which is the one record a round opens on.
        // Measured: a client reports e1 08 at t+304.686, writes its own briefing rows
        // behind it, sends nothing for ninety seconds, reports e1 08 again at
        // t+395.811, and its host opens the round 0.2 s later with the clock at 2 400
        // (`working-server-4-sessions.pcapng`). No ready record is in that capture.
        return OpenRound(
            generation,
            $"the report that followed the room's briefing arrived on 0x{reportIdentifier:x4}");
    }

    /// <summary>
    /// Whether the room's briefing time has run out since the report that began it.
    /// </summary>
    /// <remarks>
    /// It is the room's own briefing time, read where the room is published, and a
    /// host with no published room counts every report as one that follows a
    /// briefing: there is no briefing to wait on and no round for the wait to serve
    /// (<see cref="OpenRound"/> resolves nothing without a room).
    /// </remarks>
    /// <param name="remaining">
    /// How much of the briefing is left, or zero once it is out.
    /// </param>
    /// <returns><c>true</c> when the briefing is out and a report is a client leaving it.</returns>
    private bool BriefingTimeElapsed(out TimeSpan remaining)
    {
        DateTimeOffset? reportedAt;
        lock (gate)
        {
            reportedAt = briefingReportInstant;
        }

        var briefingTime = TimeSpan.FromMinutes(matchSettings.FindPublished()?.BriefingTimeMinutes ?? 0);
        remaining = reportedAt is { } began
            ? briefingTime - (DateTimeOffset.UtcNow - began)
            : TimeSpan.Zero;
        return remaining <= TimeSpan.Zero;
    }
}
