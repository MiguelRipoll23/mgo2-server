using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Keeps writing the dedicated host's in-game records to a peer once the join
/// has been answered, replaying the measured frames of the capture's solo
/// session: a sparse control and state-blob phase, then the rounds of the room
/// it is in.
/// </summary>
/// <remarks>
/// <para>
/// The recorded host never stops writing. Between the post-join burst and the
/// last second of the round it keeps the channel moving — 15 954 <c>0x0a61</c>
/// records, 4 298 <c>0x090c</c>, and the clock, blob and control records under
/// them — and a client that joined before the second player arrived receives all
/// of it while alone. A server that answers the join and then goes quiet hands
/// the client a channel that is open and silent, which is the shape of a join
/// that stalls. This service is the missing half.
/// </para>
/// <para>
/// The stream is a replay rather than a simulation: the timelines and bodies are
/// <see cref="MatchRecordFramesUtils"/>', read off <c>mgo2-game1.pcapng</c>, and
/// nothing here invents a value the capture does not show. It runs per peer, so
/// a room of several joiners gives each the beat, and it stops when that peer has
/// been quiet for as long as the session reaper allows — the records are not a
/// reason to keep a departed peer alive.
/// </para>
/// <para>
/// <b>The room opens the round, and this writes it out.</b> The sparse phase
/// ends at its last state blob and the writer waits for <see cref="MatchRoundService"/>
/// to open a round — which it does on its own clock, the first
/// <see cref="MatchRecordFramesUtils.SparsePhaseMilliseconds"/> after the room
/// took its first peer and each later one a round's measured length after the
/// last. The run therefore lands where the capture's sparse phase closes rather
/// than on a packet. A peer that entered while a round was already open beats
/// that round under its own generation and writes the run for the next one,
/// which is the second player in game1: it joins at t+154.3 s, plays the round
/// under the first identifier set, and takes the run at t+194.0 s when the room
/// opens the second.
/// </para>
/// </remarks>
/// <param name="rounds">Rounds the room opens and every peer plays.</param>
/// <param name="ready">Readiness reports the room bars a round on.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class MatchRecordService(
    MatchRoundService rounds,
    PlayerReadyService ready,
    ILogger<MatchRecordService> logger)
{
    /// <summary>
    /// How long a peer may stay quiet before its records stop. It matches the
    /// idle timeout the gameplay server gives <c>PeerSessionService</c>, so the
    /// records never outlive the session they are written to.
    /// </summary>
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Times the recorded host writes the run-shaped frame inside the beat that
    /// follows the run. Measured: the solo window's first round carries the run
    /// at counter 32793 and four repeats at 32849, 32905, 32961 and 33017.
    /// </summary>
    private const int MatchStartRunRepeatCount = 4;

    /// <summary>
    /// How often the writer looks for a new round. The room is a service, so this
    /// is only how long a writer waits to notice a round it is owed — 33 ms, one
    /// beat frame, which is the capture's own resolution.
    /// </summary>
    private const int RoundPollMilliseconds = 33;

    private readonly Lock gate = new();
    private readonly HashSet<PeerSession> writing = [];

    /// <summary>
    /// Starts the records for a peer, once. A joining client sends its profile
    /// again while it waits for the roster, and every one of those answers
    /// reaches this service; only the first one starts the stream.
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

        logger.LogInformation(
            "UDP {LocalPort}: starting the recorded host's match records to {RemoteAddress}",
            context.LocalPort,
            context.Remote);

        _ = RunAsync(context);
    }

    private async Task RunAsync(PeerContext context)
    {
        var session = context.Session;
        rounds.Enter(session);

        try
        {
            logger.LogDebug(
                "UDP {LocalPort}: playing {StepCount} sparse match-record steps to {RemoteAddress}",
                context.LocalPort,
                MatchRecordFramesUtils.SparseSteps().Count,
                session.RemoteAddress);
            foreach (var step in MatchRecordFramesUtils.SparseSteps())
            {
                if (!IsAlive(session))
                {
                    logger.LogDebug(
                        "UDP {LocalPort}: match records to {RemoteAddress} stopped during the sparse phase because the peer is idle",
                        context.LocalPort,
                        session.RemoteAddress);
                    return;
                }

                if (step.DelayMilliseconds > 0)
                {
                    await Task.Delay(step.DelayMilliseconds);
                }

                await SendAsync(context, step.Records, step.Compressed);
            }

            await RunRoundsAsync(context);
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the match records to {RemoteAddress} ended",
                context.LocalPort,
                session.RemoteAddress);
        }
        finally
        {
            lock (gate)
            {
                writing.Remove(session);
            }

            // The peer leaves the room before its last report is dropped, so the
            // room never bars a round on a session the reaper is about to take.
            rounds.Leave(session);
            ready.Forget(session);

            logger.LogDebug(
                "UDP {LocalPort}: match-record worker finished for {RemoteAddress}",
                context.LocalPort,
                session.RemoteAddress);
        }
    }

    /// <summary>
    /// Plays the room's rounds to this peer. The room decides when a round opens
    /// and which identifier set it plays under; this writer follows it — writing
    /// the match-start run when the round is one the peer is owed, and the beat
    /// frame under it until the room opens the next.
    /// </summary>
    /// <remarks>
    /// <b>A peer that joined mid-round is owed the run only from the next round.</b>
    /// Its entry round is read once, here: a peer already in the room when a
    /// round opens takes that round's run, and a peer that arrived while one was
    /// being played beats that round and takes the run when the room opens the
    /// next. Writing the run for the round in progress to a late arrival would
    /// reset the stream under a client that is already in it.
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    private async Task RunRoundsAsync(PeerContext context)
    {
        var session = context.Session;
        var enteredOn = rounds.EnteredOnRound(session);
        var writtenVersion = -1L;
        var round = new MatchRoundCounters();

        while (IsAlive(session))
        {
            var snapshot = rounds.Current;
            if (!snapshot.IsOpen)
            {
                // No round has opened yet: the room's first round is due when
                // the sparse phase this writer is replaying ends. The writer
                // waits on the room rather than writing a run of its own, so a
                // room of several peers still plays one round.
                await Task.Delay(RoundPollMilliseconds);
                continue;
            }

            if (snapshot.Version != writtenVersion)
            {
                writtenVersion = snapshot.Version;
                round.Reset();

                if (snapshot.Number > enteredOn)
                {
                    await SendRoundStartAsync(
                        context,
                        snapshot.Generation,
                        ready.EchoedByte(session, MatchRecordBodiesUtils.DefaultReadinessReportValue));
                    // The run is frame 0 of the round, so the beat that follows
                    // starts at 1. That is the counting the capture uses: its
                    // item-box group sits at offset 6, 14, … from the run and
                    // its repeats at 56, 112, …, which the phases below only
                    // produce when the run owns the first index.
                    round.FrameIndex = 1;
                }
                else
                {
                    logger.LogDebug(
                        "UDP {LocalPort}: {RemoteAddress} joined round {RoundNumber} in progress; beating it "
                        + "under its identifier set and opening the next one for it",
                        context.LocalPort,
                        session.RemoteAddress,
                        snapshot.Number);
                }
            }

            // The run's repeats ride inside the beat; they do not replace it.
            // The recorded host keeps the 33 ms frame moving after the run and
            // writes the run-shaped frame in place of a beat at frame 56, 112,
            // 168 and 224 — measured as the four repeats each round carries, and
            // as the item-box frames every eighth frame between them. Writing
            // them back to back ahead of the beat, as this did, is a round with
            // no beat under it for the seven seconds the client is leaving its
            // loading screen, which is where a live client went black.
            if (round.FrameIndex > 0
                && round.FrameIndex % MatchStartRunFramesUtils.MatchStartRepeatPeriodFrames == 0
                && round.RepeatsWritten < MatchStartRunRepeatCount)
            {
                round.RepeatsWritten++;
                await SendAsync(
                    context,
                    MatchStartRunFramesUtils.MatchStartRunRepeat(
                        snapshot.Generation,
                        ready.EchoedByte(session, MatchRecordBodiesUtils.DefaultReadinessReportValue)),
                    compressed: true);
                round.FrameIndex++;
                await Task.Delay(MatchRecordFramesUtils.FramePeriodMilliseconds);
                continue;
            }

            var plan = new BeatFramePlan(
                round.FrameIndex,
                snapshot.Generation,
                round.SessionFlags2,
                round.BlobsWritten,
                round.ItemBoxSequence,
                round.ItemBoxStateSequence);

            await SendAsync(context, plan.Records(), plan.Compressed);

            // The counters advance on what the frame carried, not on the frame:
            // the flags2 byte moves once per clock or control insertion and the
            // blob alternation once per state blob, which is what the capture's
            // cadence is. The item-box counters advance once per record the
            // group wrote, because that is the stream's own numbering and what
            // the client answers against.
            if (plan.CarriesSessionFlags)
            {
                round.SessionFlags2++;
            }

            if (plan.CarriesStateBlob)
            {
                round.BlobsWritten++;
            }

            round.ItemBoxSequence = plan.ItemBoxSequenceAfter;
            round.ItemBoxStateSequence = plan.ItemBoxStateSequenceAfter;

            round.FrameIndex++;
            await Task.Delay(MatchRecordFramesUtils.FramePeriodMilliseconds);
        }

        logger.LogDebug(
            "UDP {LocalPort}: steady match records stopped for {RemoteAddress} because the peer is idle",
            context.LocalPort,
            session.RemoteAddress);
    }

    /// <summary>
    /// Writes the round's opening marker and run under the given identifier set,
    /// echoing the byte this peer reported. The run's repeats are not here: they
    /// ride inside the beat that follows, in <see cref="RunRoundsAsync"/>.
    /// </summary>
    /// <remarks>
    /// <b>The marker answers the readiness report when there is one.</b> The
    /// marker's identifier is the round's payload with the answer bit set —
    /// <c>0x5ade</c> for <c>0x1ade</c>, <c>0x52de</c> for <c>0x12de</c> — which
    /// is why the dispatcher leaves the report unnoticed in the pass that
    /// received it and this writes the one answer that goes out, the report or
    /// no report: game1's solo round carries a single host <c>0x5ade</c>, at
    /// t+46.4155 with the run.
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="generation">Identifier set of the round being opened.</param>
    /// <param name="reported">Byte the peer's readiness report carried.</param>
    private async Task SendRoundStartAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation,
        byte reported)
    {
        logger.LogInformation(
            "UDP {LocalPort}: opening a round for {RemoteAddress} under "
            + "(operations 0x{Operations:x4}, rules 0x{Rules:x4}, vitals 0x{Vitals:x4}, marker 0x{Marker:x4}) "
            + "in answer to the client's readiness report, echoing 0x{Reported:x2}",
            context.LocalPort,
            context.Session.RemoteAddress,
            generation.RoundClock,
            generation.RulesRoster,
            generation.Vitals,
            generation.MatchStartMarker,
            reported);

        await SendAsync(
            context,
            [MatchRecordFramesUtils.ControlInsertion(
                MatchRecordFramesUtils.RareControlBody(),
                MatchRecordFramesUtils.RareControlFourthByte)],
            compressed: false);
        // The report's answer, written here rather than in the pass that
        // received it: the recorded host answers with the round.
        await SendAsync(context, [new(generation.MatchStartMarker, [])], compressed: false);
        await SendAsync(
            context,
            MatchStartRunFramesUtils.MatchStartRun(generation, reported),
            compressed: true);
    }

    private static async Task SendAsync(
        PeerContext context,
        IReadOnlyList<MatchRecordFramesUtils.RecordMessage> records,
        bool compressed)
    {
        // The recorded host marks the state blobs and the match-start run and
        // leaves the one-byte controls and the beat frames plain, so where the
        // context can batch, the run goes out under the marker the capture shows.
        // A single record cannot be batched compressed without changing what it
        // means, so the compressed path falls back to plain there.
        var batch = compressed
            ? context.SendRecordsCompressed ?? context.SendRecords
            : context.SendRecords;

        if (batch is { } sendRecords)
        {
            await sendRecords([.. records.Select(MatchRecordFramesUtils.ToUdpMessage)]);
            return;
        }

        foreach (var record in records)
        {
            await context.Send(record.Type, record.Body, record.FourthByte);
        }
    }

    /// <summary>
    /// Whether the records should keep being written. A peer that has gone quiet
    /// for the session timeout is one the reaper is about to drop, and records
    /// sent to it would go to an endpoint nobody reads.
    /// </summary>
    /// <param name="session">Session the records are written to.</param>
    private static bool IsAlive(PeerSession session) =>
        session.Established &&
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - session.LastSeenAt <= IdleTimeout.TotalMilliseconds;
}
