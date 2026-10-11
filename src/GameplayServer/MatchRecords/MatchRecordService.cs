using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Keeps writing the dedicated host's in-game records to a peer once the join
/// has been answered, replaying the measured frames of the capture's solo
/// session: a sparse control and state-blob phase, then — once the client
/// reports itself ready — the match-start run, then the beat frame with its
/// round-clock, state-blob and control insertions.
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
/// <see cref="MatchRecordFramesUtils"/>', read off <c>docs/mgo2-game.pcapng</c>,
/// and nothing here invents a value the capture does not show. It runs per peer,
/// so a room of several joiners gives each the stream the solo session recorded,
/// and it stops when that peer has been quiet for as long as the session reaper
/// allows — the records are not a reason to keep a departed peer alive.
/// </para>
/// <para>
/// <b>The round opens when the client reports itself ready.</b> The sparse
/// phase ends at its last state blob and the writer waits: the match-start run
/// is the host's answer to that report, which arrives through
/// <c>PlayerReadyHandler</c>. A server that wrote the run on the sparse phase's
/// own clock opened a round nobody had announced readiness for, and the client
/// then reported again and again with nothing reading it — the stuck ready state
/// this replaces.
/// </para>
/// </remarks>
/// <param name="ready">Readiness reports peers have sent that open their rounds.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class MatchRecordService(
    PlayerReadyService ready,
    ILogger<MatchRecordService> logger)
{
    /// <summary>
    /// How long a peer may stay quiet before its records stop. It matches the
    /// idle timeout the gameplay server gives <c>PeerSessionService</c>, so the
    /// records never outlive the session they are written to.
    /// </summary>
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

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
        var round = new RoundState();

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

            // The sparse phase ends at the last state blob and the round opens
            // only once the client reports itself ready, so this is where the two
            // meet. A report that arrives before the writer gets here is already
            // recorded and is taken on the first poll.
            if (!await WaitForReadyAsync(context, round))
            {
                logger.LogDebug(
                    "UDP {LocalPort}: {RemoteAddress} never reported itself ready; the match records stop at the sparse phase",
                    context.LocalPort,
                    session.RemoteAddress);
                return;
            }

            await RunBeatAsync(context, round);
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

            // A request this worker will never spend — a peer that went quiet
            // while waiting for its round — goes with it, so the session does not
            // outlive the records that were to be written for it.
            ready.Forget(session);

            logger.LogDebug(
                "UDP {LocalPort}: match-record worker finished for {RemoteAddress}",
                context.LocalPort,
                session.RemoteAddress);
        }
    }

    /// <summary>
    /// The beat phase: the frame the recorded host repeats every
    /// <see cref="MatchRecordFramesUtils.FramePeriodMilliseconds"/> ms, with a
    /// round-clock, state-blob or control record inserted in front of the beat
    /// on the periods the capture shows, and the vitals record joining the beat
    /// once the round has run for <see cref="MatchRecordFramesUtils.VitalsOnsetFrames"/>
    /// frames.
    /// </summary>
    /// <remarks>
    /// A round lasts <see cref="MatchRecordFramesUtils.RoundLengthMilliseconds"/>,
    /// which is where the next round becomes <em>due</em> under the sibling
    /// identifier set — the same records a <c>0x800</c> bit along (see
    /// <see cref="MatchRecordGenerationUtils"/>). It is not what opens it: the
    /// client asking does, so the beat phase waits at the boundary for the
    /// request rather than writing a run unasked. The capture's round ended when
    /// a team reached the goal, and this replay uses that measured length rather
    /// than the 420 s the round's own clock carries, because it does not simulate
    /// the match and so cannot see a team reach anything.
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="round">The round the first run has already opened.</param>
    private async Task RunBeatAsync(PeerContext context, RoundState round)
    {
        var session = context.Session;

        while (IsAlive(session))
        {
            if (round.ElapsedMilliseconds >= MatchRecordFramesUtils.RoundLengthMilliseconds)
            {
                await WaitForReadyAsync(context, round);
            }

            var plan = new BeatFramePlan(
                round.FrameIndex,
                round.Generation,
                round.SessionFlags2,
                round.BlobsWritten);

            await SendAsync(context, plan.Records(), plan.Compressed);

            // The counters advance on what the frame carried, not on the frame:
            // the flags2 byte moves once per clock or control insertion and the
            // blob alternation once per state blob, which is what the capture's
            // cadence is.
            if (plan.CarriesSessionFlags)
            {
                round.SessionFlags2++;
            }

            if (plan.CarriesStateBlob)
            {
                round.BlobsWritten++;
            }

            round.FrameIndex++;
            round.ElapsedMilliseconds += MatchRecordFramesUtils.FramePeriodMilliseconds;
            await Task.Delay(MatchRecordFramesUtils.FramePeriodMilliseconds);
        }

        logger.LogDebug(
            "UDP {LocalPort}: steady match records stopped for {RemoteAddress} because the peer is idle",
            context.LocalPort,
            session.RemoteAddress);
    }

    /// <summary>
    /// Waits for the client to report itself ready, then opens the round: the
    /// rare control the capture's later rounds carry at their head, the round's
    /// marker, the match-start run and its four repeats, then the beat phase back
    /// with the round's own counters reset.
    /// </summary>
    /// <remarks>
    /// <b>The client reporting ready opens the round; the clock does not.</b>
    /// Measured across the three captures, every round begins within 0.6–5.5 s of
    /// a client's <c>e1 NN</c> report and no round begins without one. A server
    /// that opens the round on a timer instead writes a run the client never
    /// announced readiness for, and the client then reports again and again — the
    /// live run reported twenty-three times over two minutes and was answered to
    /// none of them, which is the shape of a game stuck in its ready state.
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="round">Round whose generation and counters advance.</param>
    /// <returns><c>false</c> when the peer went quiet before it reported.</returns>
    private async Task<bool> WaitForReadyAsync(PeerContext context, RoundState round)
    {
        var session = context.Session;

        while (true)
        {
            if (ready.TryTake(session, out var reported))
            {
                round.StartNextGeneration(reported);
                await SendRoundStartAsync(context, round.Generation);
                return true;
            }

            if (!IsAlive(session))
            {
                return false;
            }

            await Task.Delay(ReadyPollMilliseconds);
        }
    }

    /// <summary>
    /// Writes the round's opening run and its repeats under the given identifier
    /// set.
    /// </summary>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="generation">Identifier set of the round being opened.</param>
    private async Task SendRoundStartAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation)
    {
        logger.LogInformation(
            "UDP {LocalPort}: opening a round for {RemoteAddress} under "
            + "(operations 0x{Operations:x4}, rules 0x{Rules:x4}, vitals 0x{Vitals:x4}, marker 0x{Marker:x4}) "
            + "in answer to the client's readiness report",
            context.LocalPort,
            context.Session.RemoteAddress,
            generation.RoundClock,
            generation.RulesRoster,
            generation.Vitals,
            generation.MatchStartMarker);

        await SendAsync(
            context,
            [MatchRecordFramesUtils.ControlInsertion(
                MatchRecordFramesUtils.RareControlBody(),
                MatchRecordFramesUtils.RareControlFourthByte)],
            compressed: false);
        await SendAsync(context, [new(generation.MatchStartMarker, [])], compressed: false);
        await SendAsync(
            context,
            MatchStartRunFramesUtils.MatchStartRun(generation, generation.ReportedByte),
            compressed: true);
        for (var repeat = 0; repeat < MatchStartRunRepeatCount; repeat++)
        {
            await Task.Delay(MatchStartRunFramesUtils.MatchStartRepeatDelayMilliseconds);
            await SendAsync(
                context,
                MatchStartRunFramesUtils.MatchStartRunRepeat(generation, generation.ReportedByte),
                compressed: true);
        }
    }

    /// <summary>Repeats of the match-start run the capture writes after it.</summary>
    private const int MatchStartRunRepeatCount = 4;

    /// <summary>
    /// How often the writer looks for a readiness report. The report is recorded
    /// by the dispatch loop and taken here, so this is only how long a round waits
    /// to open after the client has already said it is ready — 33 ms, one beat
    /// frame, which is the capture's own resolution.
    /// </summary>
    private const int ReadyPollMilliseconds = 33;

    /// <summary>
    /// One round's mutable state: the identifier set it plays under and the
    /// counters the beat phase advances. Held in a class rather than in locals
    /// because the round boundary is crossed from an async method, which cannot
    /// take them by reference.
    /// </summary>
    private sealed class RoundState
    {
        /// <summary>Identifier set of the round being played.</summary>
        public MatchRecordGenerationUtils Generation { get; private set; } = MatchRecordGenerationUtils.First;

        /// <summary>Beat frames written in this round.</summary>
        public int FrameIndex { get; set; }

        /// <summary>Session-record flags2 the next beat insertion carries.</summary>
        public byte SessionFlags2 { get; set; } = MatchRecordFramesUtils.SteadySessionFlagsStart;

        /// <summary>State blobs written in this round.</summary>
        public int BlobsWritten { get; set; }

        /// <summary>Round time elapsed so far, in milliseconds.</summary>
        public int ElapsedMilliseconds { get; set; }

        /// <summary>
        /// Advances to the next round's identifier set, carrying the byte the
        /// client reported, and resets the counters.
        /// </summary>
        /// <param name="reported">Byte from the client's readiness report.</param>
        public void StartNextGeneration(byte reported)
        {
            Generation = Generation.Next(reported);
            FrameIndex = 0;
            SessionFlags2 = MatchRecordFramesUtils.SteadySessionFlagsStart;
            BlobsWritten = 0;
            ElapsedMilliseconds = 0;
        }
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
