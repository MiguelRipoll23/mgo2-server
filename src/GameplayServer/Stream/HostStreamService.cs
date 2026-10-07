using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Stream;

/// <summary>
/// Keeps writing the dedicated host's gameplay stream to a peer once the join
/// has been answered, replaying the measured frames of the capture's solo
/// session: a sparse control/state phase, the match-start run, then the steady
/// beat-and-tick frame.
/// </summary>
/// <remarks>
/// <para>
/// The recorded host never stops writing. Between the post-join burst and the
/// last second of the round it keeps the channel moving — 15 954 <c>0x0a61</c>
/// records, 4 298 <c>0x090c</c>, and the slot, blob and control records under
/// them — and a client that joined before the second player arrived receives all
/// of it while alone. A server that answers the join and then goes quiet hands
/// the client a channel that is open and silent, which is the shape of a join
/// that stalls. This service is the missing half.
/// </para>
/// <para>
/// The stream is a replay rather than a simulation: the timelines and bodies are
/// <see cref="HostStreamFramesUtils"/>', read off <c>docs/mgo2-game.pcapng</c>,
/// and nothing here invents a value the capture does not show. It runs per peer,
/// so a room of several joiners gives each the stream the solo session recorded,
/// and it stops when that peer has been quiet for as long as the session reaper
/// allows — the stream is not a reason to keep a departed peer alive.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class HostStreamService(ILogger<HostStreamService> logger)
{
    /// <summary>
    /// How long a peer may stay quiet before its stream stops. It matches the
    /// idle timeout the gameplay server gives <c>PeerSessionService</c>, so a
    /// stream never outlives the session it writes to.
    /// </summary>
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    private readonly Lock gate = new();
    private readonly HashSet<PeerSession> streaming = [];

    /// <summary>
    /// Starts the stream for a peer, once. A joining client sends its profile
    /// again while it waits for the roster, and every one of those answers
    /// reaches this service; only the first one starts the stream.
    /// </summary>
    /// <param name="context">Context of the message the join was answered through.</param>
    public void Start(PeerContext context)
    {
        lock (gate)
        {
            if (!streaming.Add(context.Session))
            {
                return;
            }
        }

        logger.LogInformation(
            "UDP {LocalPort}: starting the recorded host stream to {RemoteAddress}",
            context.LocalPort,
            context.Remote);

        _ = RunAsync(context);
    }

    private async Task RunAsync(PeerContext context)
    {
        var session = context.Session;

        try
        {
            foreach (var step in HostStreamFramesUtils.SparseSteps())
            {
                if (!IsAlive(session))
                {
                    return;
                }

                if (step.DelayMilliseconds > 0)
                {
                    await Task.Delay(step.DelayMilliseconds);
                }

                await SendAsync(context, step.Records, step.Compressed);
            }

            await RunSteadyAsync(context);
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the host stream to {RemoteAddress} ended",
                context.LocalPort,
                session.RemoteAddress);
        }
        finally
        {
            lock (gate)
            {
                streaming.Remove(session);
            }
        }
    }

    /// <summary>
    /// The steady phase: the fixed frame every 29 ms, with a slot, blob or
    /// control record inserted in front of it on the periods the capture shows.
    /// The fourth byte is the host's own running value, carried through from the
    /// match-start run's first entry.
    /// </summary>
    /// <param name="context">Context the stream is written through.</param>
    private async Task RunSteadyAsync(PeerContext context)
    {
        var session = context.Session;
        var frameIndex = 0;
        var ordinal = HostStreamFramesUtils.SteadyOrdinalStart;
        var blobsWritten = 0;

        while (IsAlive(session))
        {
            var insertion = InsertionFor(frameIndex, ref ordinal, ref blobsWritten);

            // The marker follows the frame's content, not the phase: the
            // recorded host compresses a steady frame exactly when a state blob
            // rides in it (hdr 0x8648) and leaves the beat-only and slot-only
            // ones plain (hdr 0x0626, 0x062d, 0x0682), because a 22-byte mostly
            // zero body is the only thing in the steady stream that pays for the
            // compression. Sending them all plain writes bytes the capture does
            // not, and sending them all compressed would mark frames the peer
            // saw unmarked.
            var compressed = insertion is { Type: UdpCommandConstants.GameStateBlob };
            await SendAsync(context, HostStreamFramesUtils.SteadyFrame(insertion), compressed);
            frameIndex++;
            await Task.Delay(HostStreamFramesUtils.SteadyFramePeriodMilliseconds);
        }
    }

    private static HostStreamFramesUtils.StreamMessage? InsertionFor(
        int frameIndex,
        ref byte ordinal,
        ref int blobsWritten)
    {
        if (frameIndex % HostStreamFramesUtils.SlotAlphaPeriodFrames == 0)
        {
            return HostStreamFramesUtils.SlotInsertion(beta: false, ordinal++);
        }

        if (frameIndex % HostStreamFramesUtils.SlotBetaPeriodFrames == HostStreamFramesUtils.SlotBetaPhaseFrames)
        {
            return HostStreamFramesUtils.SlotInsertion(beta: true, ordinal++);
        }

        if (frameIndex % HostStreamFramesUtils.StateBlobPeriodFrames == 0)
        {
            // The first recorded insertion is the zero-valued 40-byte frame
            // (hdr 0x8648), followed by the 100-valued 41-byte frame; preserve
            // that measured alternation.
            return HostStreamFramesUtils.StateBlobInsertion(live: blobsWritten++ % 2 != 0);
        }

        if (frameIndex % HostStreamFramesUtils.ControlPeriodFrames == 0)
        {
            return HostStreamFramesUtils.ControlInsertion(
                HostStreamFramesUtils.SteadyControlBody(),
                ordinal++);
        }

        return null;
    }

    private static async Task SendAsync(
        PeerContext context,
        IReadOnlyList<HostStreamFramesUtils.StreamMessage> records,
        bool compressed)
    {
        // The recorded host marks the state blobs and the match-start run and
        // leaves the one-byte controls and the steady frames plain, so where the
        // context can batch, the run goes out under the marker the capture shows.
        // A single record cannot be batched compressed without changing what it
        // means, so the compressed path falls back to plain there.
        var batch = compressed
            ? context.SendRecordsCompressed ?? context.SendRecords
            : context.SendRecords;

        if (batch is { } sendRecords)
        {
            await sendRecords([.. records.Select(HostStreamFramesUtils.ToUdpMessage)]);
            return;
        }

        foreach (var record in records)
        {
            await context.Send(record.Type, record.Body, record.FourthByte);
        }
    }

    /// <summary>
    /// Whether the stream should keep writing. A peer that has gone quiet for
    /// the session timeout is one the reaper is about to drop, and a stream to
    /// it would write to an endpoint nobody reads.
    /// </summary>
    /// <param name="session">Session the stream writes to.</param>
    private static bool IsAlive(PeerSession session) =>
        session.Established &&
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - session.LastSeenAt <= IdleTimeout.TotalMilliseconds;
}
