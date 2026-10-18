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
    /// (<see cref="OpenWhenEveryPlayerIsReady"/>). A gate on the reports themselves
    /// was measured wrong twice over: a room's reports run up to half a minute ahead
    /// of its readies (`mgo2-game-joined-when-red-team-then-briefing` reports at
    /// t+57.656 and readies at t+112.750), and in its second briefing the ready
    /// comes *before* the report (t+870.222 against t+844.858).
    /// </para>
    /// <para>
    /// <b>The briefing is answered first and the round second.</b> A briefing
    /// hears two join reports a round. The first names the set the briefing itself is
    /// written under — the client is still being briefed, so what it is served is
    /// that briefing's run and not a round (<see cref="MatchStartRunService.Runs"/>)
    /// — and the second, arriving on the other set once the briefing's time is
    /// out, asks for the round the captures show playing after it
    /// (<c>docs/protocol/BRIEFING_FLOW.md</c> §1–§2). A report on the set the
    /// briefing is already on is the first one repeated: a client re-reports until
    /// it is answered, and that is the same report and not a round asked for.
    /// </para>
    /// <para>
    /// <b>The clock is armed here, and only as far as the room has players for.</b>
    /// A join report is not a readiness, so it arms no deadline: a room of one is
    /// frozen, its count held at the room's whole briefing time and its briefing
    /// never out (§17 of <c>docs/analysis/join-stall-after-mode2.md</c>). The report
    /// that gives the room a second player is the one its timer belongs to, and
    /// that is where the count is started and the deadline armed
    /// (<see cref="ArmBriefingDeadline"/>), for the same reason the briefing's own
    /// preparation waits: the join that made the room can be a minute ahead of the
    /// client that has to read the briefing.
    /// </para>
    /// <para>
    /// <b>The gate is here and not only in the handler.</b> A round opens for a
    /// room, so "is the room ready" is a condition of opening one rather than a
    /// step a caller is asked to remember: the handler that takes a report is one
    /// caller today, and a gate kept there would let any other caller start a
    /// round on a room that is still being briefed. It is also the invariant a
    /// second round depends on, because a round's end clears the states and the
    /// next report to arrive is a single player's.
    /// </para>
    /// </remarks>
    /// <param name="reportIdentifier">Identifier the join report arrived on.</param>
    /// <returns><c>true</c> when this call opened the round.</returns>
    public bool OnJoinReport(ushort reportIdentifier)
    {
        var generation = MatchRecordGenerationUtils.OfJoinReport(reportIdentifier);

        MatchRecordGenerationUtils? briefing;
        int armed;
        bool first;
        bool startsBriefing;
        lock (gate)
        {
            briefing = briefingGeneration;
            lastJoinReportIdentifier = reportIdentifier;
            armed = briefingNumber;
            first = briefing is null;
            if (first)
            {
                // The briefing's own set: the first report names it, and the run
                // the worker writes for the briefing is written under it.
                briefingGeneration = generation;
            }

            // The briefing's timer is the room's own and runs only once the room
            // has somebody else in it: a room of one holds its count at the whole
            // briefing time and never reaches a deadline, so the report that makes
            // the room a room of two is the one armed here. One briefing arms it
            // once, whatever the reports after that one say.
            startsBriefing = armedDeadlineBriefing != armed
                && briefingStates.JoinedCount >= CountingJoinReportCount;
            if (startsBriefing)
            {
                armedDeadlineBriefing = armed;
            }
        }

        if (first)
        {
            // The count a peer is sent starts at its first report: held at the
            // room's whole briefing time until a second player reports, and its own
            // end only once it moves (BriefingCountdownService).
            BeginBriefingClock();

            logger.LogInformation(
                "The room's briefing heard its first join report on 0x{Identifier:x4}; the briefing is "
                + "written under that set and no round opens on a join report",
                reportIdentifier);
        }

        if (startsBriefing)
        {
            ArmBriefingDeadline(armed);
        }

        if (first)
        {
            return false;
        }

        if (generation == briefing)
        {
            logger.LogDebug(
                "A report arrived on 0x{Identifier:x4}, the set the room's briefing is already on; the "
                + "round waits for a report on the other one",
                reportIdentifier);
            return false;
        }

        // The other set: the report the captures show arriving once the briefing is
        // done. It opens the round only for a room whose players have all readied —
        // the clock, if the room has one, opens it for the rest (OpenWhenBriefingEndsAsync).
        return OpenRound(
            generation,
            $"the join report arrived on 0x{reportIdentifier:x4}",
            waitForEveryone: true);
    }

    /// <summary>
}
