using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The run half of the round opener: the per-peer watcher that writes a peer's
/// briefing run, waits for the round that briefing precedes to open, and writes
/// that round's run.
/// </summary>
/// <remarks>
/// <b>The run is written per peer because the channel is.</b> Every peer has its
/// own session, key and counter, and the run's payload echoes the byte that peer
/// reported, so a run is a peer's own record and not the room's: the room opens
/// once (<see cref="MatchStartRunService.OnJoinReport"/>) and each peer's worker writes
/// the run for it. A peer is served two runs a round — the briefing's, under the
/// set its first report named and carrying the round's whole clock, and the
/// round's own, under the other set and carrying what is left of it once the
/// briefing is done. The clock is not the round's whole length on the second
/// because the workers reach their own run at their own moment, and a peer that
/// joins inside a round is handed the round it joined and not the one it missed.
/// </remarks>
public sealed partial class MatchStartRunService
{
    private readonly HashSet<PeerSession> writing = [];

    /// <summary>
    /// Starts writing the round's opening run to a peer, once. A joining client
    /// re-sends its profile while it waits for the roster and every one of those
    /// answers reaches this service; only the first starts the worker, and the
    /// worker goes on to serve every round the room opens.
    /// </summary>
    /// <param name="context">Context of the message the join was answered through.</param>
    public void Start(PeerContext context)
    {
        lock (gate)
        {
            if (!writing.Add(context.Session))
            {
                return;
            }
        }

        // The room's first briefing begins with the first peer that reaches it:
        // that peer's report names the set the briefing is written under, and the
        // room's own briefing time is the deadline that opens the round, armed
        // once the room has a second player to be timed with (ArmBriefingDeadline).
        BeginBriefing();

        logger.LogDebug(
            "UDP {LocalPort}: watching for rounds to open for {RemoteAddress}",
            context.LocalPort,
            context.Remote);
        _ = RunAsync(context);
    }

    /// <summary>
    /// Writes a peer's runs, one per round the room opens, for as long as the
    /// peer is there.
    /// </summary>
    /// <param name="context">Context the run is written through.</param>
    private async Task RunAsync(PeerContext context)
    {
        var session = context.Session;

        try
        {
            while (MatchRecordPeerUtils.IsAlive(session))
            {
                // The briefing is answered before the round is: the first report
                // names the set the briefing is written under, and what a client
                // still being briefed is served is that briefing's run rather than
                // the round it precedes (MatchStartRunService.OnJoinReport). The wait here
                // is while no briefing has heard a report; the run it writes takes
                // the set that report named.
                await WriteBriefingRunAsync(context);

                // The run cannot be written before a round exists: its payload
                // echoes the report, and there is no report behind a round that
                // nobody asked for. The wait is the peer's own lifetime, because
                // a briefing lasts as long as the players in it take.
                await roundState.WaitForOpenAsync(session, MatchRoundStateService.WaitWhilePeerIsThere);
                if (!roundState.IsOpen)
                {
                    // The only way a wait that lasts while the peer is there
                    // returns without a round is that the peer went quiet in it.
                    logger.LogDebug(
                        "UDP {LocalPort}: stopping the round watcher for {RemoteAddress}: the peer is idle",
                        context.LocalPort,
                        session.RemoteAddress);
                    return;
                }

                // The briefing ends on the lobby-slot confirmation, which is
                // written here and not at the entry (ConfirmEntryAsync).
                await ConfirmEntryAsync(context);

                var round = roundState.RoundNumber;
                var generation = roundState.Generation;
                await WriteRunAsync(context, generation);
                await roundState.WaitForCloseAsync(round, session);

                // The result is written behind the round, on the set it played
                // under. The close is checked and not assumed: the wait also
                // returns when the peer goes idle, and a round that never closed
                // has no result of its own — only the one before it.
                if (!roundState.IsOpen)
                {
                    await results.WriteResultAsync(context, generation);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the round watcher for {RemoteAddress} ended",
                context.LocalPort,
                session.RemoteAddress);
        }
        finally
        {
            lock (gate)
            {
                writing.Remove(session);
                entered.Remove(session);
                ForgetEntry(session);
            }
        }
    }

    /// <summary>
    /// The round the briefing now running precedes: the next round the room's own
    /// rotation names. It is <c>null</c> while the room is not published.
    /// </summary>
    /// <remarks>
    /// It reads the rotation directly rather than going through
    /// <see cref="ReadRound"/> because the briefing polls it and a warning per poll
    /// is noise; the round's own open logs it once.
    /// </remarks>
    private MatchRotationEntry? ReadBriefingRound() =>
        matchSettings.FindPublished()?.Round(roundState.RoundNumber + 1);

    /// <summary>
    /// Writes one round's opening run to a peer and repeats it behind itself.
    /// </summary>
    /// <param name="context">Context the run is written through.</param>
    /// <param name="generation">Identifier set the round plays under.</param>
    private async Task WriteRunAsync(PeerContext context, MatchRecordGenerationUtils generation)
    {
        if (roundState.RoundSettings is not { } round)
        {
            // The round closed under the wait — its clock ran out before this
            // peer's worker reached the run — so there is nothing to open it with.
            logger.LogDebug(
                "UDP {LocalPort}: round {RoundNumber} closed before its opening run was written to "
                + "{RemoteAddress}",
                context.LocalPort,
                roundState.RoundNumber,
                context.Remote);
            return;
        }

        await WriteRunCoreAsync(context, generation, round, roundState.RemainingUnits, briefing: false);
    }

    /// <summary>
    /// Writes one run — a round's, or the briefing's that precedes it — to a peer
    /// and repeats it behind itself.
    /// </summary>
    /// <param name="context">Context the run is written through.</param>
    /// <param name="generation">Identifier set the run is written under.</param>
    /// <param name="round">The round whose rule, map and clock the run carries.</param>
    /// <param name="clockUnits">
    /// Units of the round clock the run carries: the round's whole time for a
    /// briefing's run, which has played nothing, and what is left of it for a
    /// round that is already playing.
    /// </param>
    /// <param name="briefing">
    /// Whether this is the run a briefing is answered with. A room that has not
    /// played a round writes the briefing's own shape, without the round's stage,
    /// its clock, its payload or its rules pair; a room whose round has played
    /// keeps the full run, which is what the reference writes between one round
    /// and the next.
    /// </param>
    private async Task WriteRunCoreAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation,
        MatchRotationEntry round,
        int clockUnits,
        bool briefing)
    {
        // A room's first briefing has no round behind it, so the run that answers
        // it carries no round state. Writing the stage and the clock there is what
        // told the client a round already existed and started it in the game
        // instead of on the briefing screen; the reference host's fresh-room run
        // (`mgo2-game-started-on-briefing` t+58.255) holds neither.
        var carriesRoundState = !briefing || roundState.RoundNumber > 0;

        var reported = briefingStates.TryGetReported(context.Session.RemoteAddress, out var value)
            ? value
            : MatchStartBodiesUtils.DefaultJoinReportValue;

        // The run's own 0x1001 identifiers take the roster exchange's counter's
        // next values — the arrays, the room's own stage where a round has played
        // and the close triple: the join's records and the burst took the ones
        // before them, and every record written under those identifiers behind
        // this one takes the next, which is what the captured stream does — its
        // run carries 7 and 8 for the two arrays and 9, 10 and 11 for the close
        // triple behind them (docs/protocol/GAMEPLAY_SERVER.md §7). A fresh
        // briefing's run has no stage, so its closes are the values directly
        // behind the arrays.
        var runOrdinals = roster.TakeRunOrdinals(
            carriesRoundState
                ? MatchStartRunFramesUtils.RosterOrdinalCount
                : MatchStartRunFramesUtils.BriefingRosterOrdinalCount);

        // The run's operations records take its stream's first values. The stream
        // was restarted where the run's own stage began — the briefing restarted
        // it for the countdown (BeginBriefing), the round restarted it for its run
        // (OpenRound) — so the block reads 00, the record behind it 01 and the
        // clock 02, and the beat's own clocks go on from 03: the numbering the
        // capture's run and its beat show. A fresh briefing's run has no clock and
        // takes the first two only; the round's own run opens its stream again at
        // the open.
        var operationsOrdinals = sequences.Take(
            generation.Operations,
            carriesRoundState
                ? MatchStartRunFramesUtils.OperationsRecordCount
                : MatchStartRunFramesUtils.BriefingOperationsRecordCount);

        var run = MatchStartRunFramesUtils.BuildRun(
            generation,
            reported,
            Rooms.TeamArrayFramesUtils.OpeningSequence,
            runOrdinals,
            operationsOrdinals,
            briefingStates.JoinedMask,
            clockUnits,
            EntryAnswersOf(context.Session),
            carriesRoundState,

            // A briefing's run does not answer the report: the report was
            // answered beside its publication, with the round's marker, in the
            // pass that received it. A run that repeats that marker hands the
            // peer a second round start a few seconds after it entered
            // (`docs/analysis/join-stall-after-mode2.md` §26).
            answersReport: !briefing);

        logger.LogInformation(
            "UDP {LocalPort}: writing an opening run to {RemoteAddress}: {RecordCount} records, "
            + "payload echoing {Reported}, operations 0x{Operations:x4}, payload 0x{Payload:x4}, "
            + "rule {Rule}, map {Map}, {ClockUnits} of {TimeLimitUnits} units carried of its "
            + "{TimeLimitMinutes}-minute clock, {Phase}",
            context.LocalPort,
            context.Remote,
            run.Count,
            reported,
            generation.Operations,
            generation.MatchStartPayload,
            round.Rule,
            round.Map,
            clockUnits,
            round.TimeLimitUnits,
            round.TimeLimitMinutes,
            carriesRoundState ? "with the round's stage and clock" : "as a fresh briefing, with no round state");

        // Written once, in one frame, as every capture writes it — and written
        // again whole on the tick behind it, which is the other half of the same
        // measurement. The run used to be written three times inside one pass on a
        // count taken from the rounds that carry two and three whole runs; those
        // repetitions are spaced — `mgo2-started-briefing-then-solo-game` writes
        // the run's records at t+57.626 and again at t+59.993 — and three copies
        // inside one pass are not a repeat, they are the same frame three times,
        // which gives a client that lost the first one nothing
        // (`docs/analysis/connection-not-stable-disconnect.md` §4). The repeat a
        // capture does write is the one below: the same set one briefing tick
        // later, with the marker and the roster heads left out
        // (`MatchStartRunFramesUtils.RepeatOf`).
        //
        // The phase the run is being written for is read before it goes out, so
        // the repeat can tell whether the room moved on in the tick between.
        var roundNumber = roundState.RoundNumber;
        var wasOpen = roundState.IsOpen;

        await MatchRecordSendUtils.SendAsync(context, run);

        roundState.NoteRunWritten(context.Session);

        // The repeat runs beside this worker rather than inside it: a run the tick
        // wait sat in front of would hold this peer's next run — the round's, when
        // the room opens inside that tick — behind a timer nothing is waiting on.
        _ = RepeatRunAsync(context, generation, run, roundNumber, wasOpen);
    }

    /// <summary>
    /// Writes a peer the run's records again, one briefing tick after the run
    /// itself, which is the second instant every capture writes.
    /// </summary>
    /// <remarks>
    /// <b>Only while the room is still in the phase the run was written for.</b>
    /// A briefing whose round opened inside the tick is a room whose run the
    /// round's own writer is about to replace, and repeating the briefing's set
    /// over it would put a whole clock on a round already playing. The repeat does
    /// not fault: it runs beside the waits for the round, so an exception here
    /// would be an unobserved one, and a send that fails says the peer is gone,
    /// which is what ending quietly means.
    /// </remarks>
    /// <param name="context">Context the repeat is written through.</param>
    /// <param name="generation">Identifier set the run was written under.</param>
    /// <param name="run">The run as it was written.</param>
    /// <param name="roundNumber">Round number the run was written for.</param>
    /// <param name="wasOpen">Whether a round was playing when it was written.</param>
    private async Task RepeatRunAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation,
        IReadOnlyList<MatchRecordFramesUtils.RecordMessage> run,
        int roundNumber,
        bool wasOpen)
    {
        try
        {
            await Task.Delay(BriefingCountdownBodiesUtils.PublishPeriodMilliseconds);

            if (!MatchRecordPeerUtils.IsAlive(context.Session))
            {
                return;
            }

            if (roundState.RoundNumber != roundNumber || roundState.IsOpen != wasOpen)
            {
                logger.LogDebug(
                    "UDP {LocalPort}: the room moved on before the tick behind the run written to "
                    + "{RemoteAddress}, so the run is not repeated",
                    context.LocalPort,
                    context.Remote);
                return;
            }

            logger.LogDebug(
                "UDP {LocalPort}: repeating the run written to {RemoteAddress} one tick behind it, "
                + "without its opening answer",
                context.LocalPort,
                context.Remote);
            await MatchRecordSendUtils.SendAsync(
                context,
                MatchStartRunFramesUtils.RepeatOf(generation, run));
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the run written to {RemoteAddress} was not repeated",
                context.LocalPort,
                context.Remote);
        }
    }
}
