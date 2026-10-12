using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Keeps writing the dedicated host's in-game records to a peer once the join
/// has been answered, replaying the measured frames of the capture's solo
/// session: a sparse control and state-blob phase, then the beat that carries the
/// room's live records.
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
/// <b>The round start is never written.</b> The opening marker, the
/// <c>0x1a54</c> run and its four repeats are the one part of the capture this
/// writer leaves out, and nothing here opens a round on a clock, on a client's
/// readiness report, or on anything else: the sparse phase closes and the beat
/// begins, and the beat is all that follows. The run is a host's answer to a
/// client that asked to play, and an unprompted one is worse than none — measured
/// on a live client, a run that arrives before the report is acknowledged and
/// ignored while the client re-reports, because the client will not enter a round
/// it did not ask for and the run has no report to echo. The beat is what keeps
/// the channel alive; the round start is withheld.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class MatchRecordService(ILogger<MatchRecordService> logger)
{
    /// <summary>
    /// How long a peer may stay quiet before its records stop. It matches the
    /// idle timeout the gameplay server gives <c>PeerSessionService</c>, so the
    /// records never outlive the session they are written to.
    /// </summary>
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The frame the beat starts on. The run would be frame 0, so the frame that
    /// followed it is 1, and the numbering is kept although the run is not
    /// written: the insertions, the item-box group and the vitals onset are all
    /// measured as offsets from the run's own index, and renumbering the beat
    /// would move every one of them off the frame the capture shows it on.
    /// </summary>
    private const int FirstBeatFrameIndex = 1;

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

            await BeatAsync(context);
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

            logger.LogDebug(
                "UDP {LocalPort}: match-record worker finished for {RemoteAddress}",
                context.LocalPort,
                session.RemoteAddress);
        }
    }

    /// <summary>
    /// Beats for as long as the peer is there: the capture's steady phase, at its
    /// 33 ms frame period, under the first identifier set of the capture.
    /// </summary>
    /// <remarks>
    /// <b>The set does not change while the stream runs.</b> The capture alternates
    /// it with the rounds it opens — <c>0x1a54</c>, then <c>0x1254</c> a
    /// <c>0x800</c> bit along — but nothing here opens a round, so nothing here
    /// has a round's parity to move to:
    /// <see cref="MatchRecordGenerationUtils.First"/> is the set the captured host
    /// holds from its own first join until its first round closes.
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    private async Task BeatAsync(PeerContext context)
    {
        var session = context.Session;
        var beat = new BeatCounters { FrameIndex = FirstBeatFrameIndex };

        while (IsAlive(session))
        {
            var plan = new BeatFramePlan(
                beat.FrameIndex,
                MatchRecordGenerationUtils.First,
                beat.SessionFlags2,
                beat.BlobsWritten,
                beat.ItemBoxSequence,
                beat.ItemBoxStateSequence);

            await SendAsync(context, plan.Records(), plan.Compressed);

            // The counters advance on what the frame carried, not on the frame:
            // the flags2 byte moves once per clock or control insertion and the
            // blob alternation once per state blob, which is what the capture's
            // cadence is. The item-box counters advance once per record the
            // group wrote, because that is the stream's own numbering and what
            // the client answers against.
            if (plan.CarriesSessionFlags)
            {
                beat.SessionFlags2++;
            }

            if (plan.CarriesStateBlob)
            {
                beat.BlobsWritten++;
            }

            beat.ItemBoxSequence = plan.ItemBoxSequenceAfter;
            beat.ItemBoxStateSequence = plan.ItemBoxStateSequenceAfter;

            beat.FrameIndex++;
            await Task.Delay(MatchRecordFramesUtils.FramePeriodMilliseconds);
        }

        logger.LogDebug(
            "UDP {LocalPort}: steady match records stopped for {RemoteAddress} because the peer is idle",
            context.LocalPort,
            session.RemoteAddress);
    }

    private static async Task SendAsync(
        PeerContext context,
        IReadOnlyList<MatchRecordFramesUtils.RecordMessage> records,
        bool compressed)
    {
        // The recorded host marks the state blobs and leaves the one-byte
        // controls and the beat frames plain, so where the context can batch, the
        // blobs go out under the marker the capture shows. A single record cannot
        // be batched compressed without changing what it means, so the compressed
        // path falls back to plain there.
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
