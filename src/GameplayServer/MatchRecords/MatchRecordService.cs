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
/// <b>The round start belongs to another writer.</b> The opening marker, the
/// <c>0x1a54</c> run and its four repeats are written by
/// <see cref="MatchStartRunService"/>, because the captures gate them on a
/// client's readiness report and nothing here opens a round. This writer goes
/// from the sparse phase straight into the beat: the beat is what keeps the
/// channel alive, and the round start arrives separately, on the client's own
/// record, running alongside it on the same session's counter.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class MatchRecordService(
    MatchRoundStateService roundState,
    ILogger<MatchRecordService> logger)
{
    /// <summary>
    /// The frame the beat starts on. The round start sits ahead of it and is
    /// written by <see cref="MatchStartRunService"/> when a client reports itself
    /// ready, so the beat's frame index starts after it: the insertions, the
    /// item-box group and the vitals onset are all measured as offsets from the
    /// run's own index, and renumbering the beat would move every one of them off
    /// the frame the capture shows it on.
    /// </summary>
    private const int FirstBeatFrameIndex = 1;

    /// <summary>
    /// Whether the records should keep being written, on the rule
    /// <see cref="MatchRecordPeerUtils"/> holds.
    /// </summary>
    /// <param name="session">Session the records are written to.</param>
    /// <returns><c>true</c> while the peer is still there.</returns>
    private static bool IsAlive(PeerSession session) => MatchRecordPeerUtils.IsAlive(session);

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

            // The beat is the round's, not the sparse phase's: everything it
            // writes is measured as an offset from the run, so it starts when
            // the round opens and not a beat earlier.
            await roundState.WaitForOpenAsync(session);
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
    /// <c>0x800</c> bit along — but the beat is not a round and this writer does
    /// not open one, so it has no parity to move to:
    /// <see cref="MatchRecordGenerationUtils.First"/> is the set the captured host
    /// holds from its own first join until its first round closes. The round
    /// start written by <see cref="MatchStartRunService"/> takes its set from the
    /// identifier the readiness report arrived on, which is where the capture's
    /// parity lives.
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

    }
