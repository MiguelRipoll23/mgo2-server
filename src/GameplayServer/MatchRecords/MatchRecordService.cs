using Mgo2Server.GameplayServer.Rooms;
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
/// <c>0x1a54</c> run and the runs repeated behind it are written by
/// <see cref="MatchStartRunService"/>, because the captures gate them on a
/// client's join report and nothing here opens a round. This writer goes
/// from the sparse phase straight into the beat: the beat is what keeps the
/// channel alive, and the round start arrives separately, on the client's own
/// record, running alongside it on the same session's counter.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed partial class MatchRecordService(
    MatchRoundStateService roundState,
    BriefingCountdownService countdowns,
    BriefingReadyPublishService readyPublications,
    BriefingRoomPublishService roomPublications,
    MatchRecordSequenceService sequences,
    RoomRosterService roster,
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
            // The briefing's writers belong to the briefing and not to the sparse
            // phase, so all three run beside the sparse steps rather than behind
            // them: the countdown, the ready set's repeat and the room's own rules
            // pair and count. The captures write them inside the phase, the rules
            // pair and the controls and blobs arriving on the same ticks
            // (`mgo2-game-joined-when-red-team-then-briefing` writes its
            // `0xd001` and `0x0002` records through its briefing), and a host that
            // started them after the phase would open the clock on the briefing's
            // last seconds — which is a clock at zero.
            // The countdown is the one of the three that runs past the peer's run
            // rather than up to it: the reference hosts write their first countdown
            // after the run a briefing's report asked for and none before it, and
            // the zero it closes a briefing on is written when the briefing's own
            // time is out or the round opens, so it is not awaited here. Awaiting
            // it would put the beat back behind the end of the briefing.
            _ = countdowns.PublishAsync(context);
            var readyPublication = readyPublications.PublishAsync(context);

            // The room's rules pair runs for the briefing's whole length, up to the
            // round's open and not up to the peer's run (the briefing's own beat,
            // `BriefingRoomPublishService`), so it is not awaited: a beat held
            // behind it would not start until the round it was waiting for.
            _ = roomPublications.PublishAsync(context);

            logger.LogDebug(
                "UDP {LocalPort}: playing {StepCount} sparse match-record steps to {RemoteAddress}",
                context.LocalPort,
                SparsePhaseFramesUtils.SparseSteps().Count,
                session.RemoteAddress);
            foreach (var step in SparsePhaseFramesUtils.SparseSteps())
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

            // The beat is the round's, not the sparse phase's, and what it is
            // counted from is the peer's own run: the briefing is answered a run of
            // its own two to three seconds after the first report, and the round it
            // precedes opens later on the briefing's deadline
            // (docs/protocol/BRIEFING_FLOW.md §1–§2). Everything the beat writes is
            // an offset from that run, so the run is what it waits for — a wait for
            // the round's open held a room of one's client on the answer to its own
            // report for the whole briefing and left it hearing nothing else, which
            // is the shape the capture against this host shows
            // (docs/analysis/join-stall-after-mode2.md §10).
            //
            // The ready publication ends on that same run, so it is awaited after
            // it and returns on it rather than holding the beat behind the round.
            await roundState.WaitForRunWrittenAsync(session);
            await readyPublication;

            // A fresh room's briefing is not the round's, so it carries no beat.
            // The reference host writes its first beat only once the round opens:
            // `mgo2-game-started-on-briefing` holds its host at 3.8 frames a second
            // through the 140 s briefing and 42 a second in the round, while this
            // host wrote the 30 a second beat from the briefing's first run onward —
            // the client was handed a round's stream while it was still being
            // briefed, and paid for it in the frame rate it could hold. A room that
            // has already played a round keeps its beat through the briefing that
            // follows it, which is `mgo2-started-briefing-then-solo-game`'s shape.
            if (roundState.RoundNumber == 0)
            {
                await roundState.WaitForOpenAsync(session, MatchRoundStateService.WaitWhilePeerIsThere);
                if (!roundState.IsOpen)
                {
                    logger.LogDebug(
                        "UDP {LocalPort}: the briefing to {RemoteAddress} ended without a round, so no beat "
                        + "is written",
                        context.LocalPort,
                        session.RemoteAddress);
                    return;
                }

                // The round's own run opens its stream, and the beat's insertions
                // are numbered behind that run: waiting for the open alone would
                // let the first frame overtake the run it belongs behind.
                await roundState.WaitForRunWrittenAsync(session);
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
