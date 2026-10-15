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
/// once (<see cref="MatchStartRunService.Open"/>) and each peer's worker writes
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
        // room's own briefing time is the deadline that opens the round when no
        // second report does.
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
                // the round it precedes (MatchStartRunService.Open). The wait here
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
            }
        }
    }

    /// <summary>
    /// Writes the briefing's own run to a peer, once the briefing has heard a
    /// report, and returns when the round it precedes opens.
    /// </summary>
    /// <remarks>
    /// <b>The briefing's run is not its round's.</b> A client still being briefed
    /// is served the run the first report named, under that report's own set, and
    /// the round it is waiting for opens later and under the other set
    /// (<see cref="MatchStartRunService.Open"/>, <c>docs/protocol/BRIEFING_FLOW.md</c>
    /// §1–§2). The run's clock stays whole: nothing of the round has been played
    /// while the client is still reading the briefing, and the clock the countdown
    /// shows it is the briefing's own seven-byte record rather than this one.
    /// </remarks>
    /// <param name="context">Context the run is written through.</param>
    private async Task WriteBriefingRunAsync(PeerContext context)
    {
        var session = context.Session;

        while (MatchRecordPeerUtils.IsAlive(session) && !roundState.IsOpen)
        {
            // No run is written before a briefing has heard a report: the run
            // echoes that report's byte, so a briefing that has heard from nobody
            // has nothing to write one from.
            if (BriefingGeneration is not { } generation)
            {
                await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
                continue;
            }

            if (ReadBriefingRound() is not { } round)
            {
                // The room is not published yet, so there is no round for the
                // briefing to precede and no time to hold its clock at.
                await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
                continue;
            }

            await WriteRunCoreAsync(context, generation, round, round.TimeLimitUnits);
            return;
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

        await WriteRunCoreAsync(context, generation, round, roundState.RemainingUnits);
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
    private async Task WriteRunCoreAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation,
        MatchRotationEntry round,
        int clockUnits)
    {
        var reported = ready.TryGetReported(context.Session.RemoteAddress, out var value)
            ? value
            : MatchStartBodiesUtils.DefaultReadinessReportValue;

        // The room's own players, read here rather than at the open: the run is
        // written per peer, and a player who joined between two peers' runs is a
        // player the later peer can be told about. A room that cannot be read
        // leaves the pair with no slot taken, which is the shape the captures
        // open with anyway.
        var slots = await lobbySlots.ReadAsync();

        // The run's own 0x1001 identifiers take the roster exchange's counter's
        // next values — the arrays, the room's own stage and the close triple:
        // the join's records and the burst took the ones before them, and every
        // record written under those identifiers behind this one takes the next,
        // which is what the captured stream does — its run carries 7 and 8 for the
        // two arrays and 9, 10 and 11 for the close triple behind them
        // (docs/protocol/GAMEPLAY_SERVER.md §7).
        var runOrdinals = roster.TakeRunOrdinals(MatchStartRunFramesUtils.RosterOrdinalCount);

        // The run's operations records take its stream's first values. The stream
        // was restarted where the run's own stage began — the briefing restarted
        // it for the countdown (BeginBriefing), the round restarted it for its run
        // (OpenRound) — so the block reads 00, the record behind it 01 and the
        // clock 02, and the beat's own clocks go on from 03: the numbering the
        // capture's run and its beat show.
        var operationsOrdinals =
            sequences.Take(generation.Operations, MatchStartRunFramesUtils.OperationsRecordCount);

        var run = MatchStartRunFramesUtils.BuildRun(
            generation,
            reported,
            Rooms.TeamArrayFramesUtils.OpeningSequence,
            runOrdinals,
            operationsOrdinals,
            slots,
            ready.PresenceMask,
            clockUnits);

        logger.LogInformation(
            "UDP {LocalPort}: writing an opening run to {RemoteAddress}: {RecordCount} records, "
            + "payload echoing {Reported}, operations 0x{Operations:x4}, payload 0x{Payload:x4}, "
            + "rule {Rule}, map {Map}, {ClockUnits} of {TimeLimitUnits} units carried of its "
            + "{TimeLimitMinutes}-minute clock",
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
            round.TimeLimitMinutes);

        // The recorded host writes the run compressed and the run's repeats
        // behind it, so a client that lost the first frame is answered by the
        // ones that follow rather than waiting for the next round.
        await MatchRecordSendUtils.SendAsync(context, run);
        for (var repeat = 0; repeat < MatchStartRunFramesUtils.RepeatCount; repeat++)
        {
            await MatchRecordSendUtils.SendAsync(context, run);
        }

        roundState.NoteRunWritten(context.Session);
    }
}
