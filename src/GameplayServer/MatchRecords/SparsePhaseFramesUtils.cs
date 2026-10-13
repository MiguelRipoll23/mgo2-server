using Mgo2Server.Shared.Constants;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The sparse phase: the records a peer is written before any round exists, one
/// step per set of them and the measured wait between two steps.
/// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is split from the beat because it is replayed and the beat is not.</b>
    /// The steps below are the recorded host's own draw of its channel while it waits
    /// for the room to report ready — the timings and bodies are read off
    /// <c>mgo2-game1.pcapng</c> and nothing here is derived — whereas the beat is
    /// written against a round that is running, with a clock that counts. Keeping
    /// both in one file is how a constant of one becomes a literal in the other.
    /// </para>
    /// <para>
    /// <b>A step is not a frame.</b> A frame of the capture is a datagram and the
    /// phase is sparse: 4.9 s of nothing follows the first record, and the steps that
    /// carry no wait are the records the capture put in one datagram. The marker that
    /// says a datagram is compressed is per step, because the recorded host marks the
    /// state blobs and leaves the one-byte controls plain.
    /// </para>
    /// </remarks>
public static class SparsePhaseFramesUtils
{
    /// <summary>
    /// How long the sparse phase lasts: the sum of the delays
    /// <see cref="SparseSteps"/> itself carries, 42 240 ms — 42.2 s, the
    /// capture's own close of the phase. It is the point on this clock at which
    /// the match-start run follows, and it is derived from the steps rather than
    /// restated so the two cannot drift apart.
    /// </summary>
    public static int SparsePhaseMilliseconds => SparsePhaseLengthMilliseconds;

    private static readonly int SparsePhaseLengthMilliseconds =
        SparseSteps().Sum(step => step.DelayMilliseconds);

    /// <summary>One step of the stream: records to write together, then the wait before the next.</summary>
    /// <param name="DelayMilliseconds">Delay before this step, in milliseconds.</param>
    /// <param name="Records">Records written together as one datagram.</param>
    /// <param name="Compressed">
    /// Whether the datagram goes out LZSS-compressed. The recorded host marks the
    /// state blobs and the match-start run and leaves the one-byte control records
    /// and the beat frames plain, so the marker follows the frame rather than the
    /// peer — the same rule the roster run follows.
    /// </param>
    public sealed record RecordStep(
        int DelayMilliseconds,
        IReadOnlyList<MatchRecordFramesUtils.RecordMessage> Records,
        bool Compressed = false);

    /// <summary>
    /// The sparse phase, counted from the roster answer. Each delay is measured
    /// between consecutive captured frames, so a zero means the two went out back
    /// to back.
    /// </summary>
    /// <remarks>
    /// <b>The phase ends at the last state blob and the run does not follow it.</b>
    /// The run is not a step of this list: it waits for the client's readiness
    /// report, and the captured client sent it 1.66 s after the phase closed — so
    /// the run lands at the capture's t+46.4 s rather than the phase's t+42.2 s,
    /// and only because the client asked. <see cref="MatchStartRunService"/> writes
    /// it on that report.
    /// </remarks>
    /// <returns>The steps in send order, ending at the last state blob.</returns>
    public static List<RecordStep> SparseSteps() =>
    [
        new(4930, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x02)]),
        new(0, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)], Compressed: true),
        new(0, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x02)]),
        new(4800, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.AlternateControl(), 0x03)]),
        new(2400, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x03)]),
        new(0, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)], Compressed: true),
        new(2370, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x04)]),
        new(2750, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)], Compressed: true),
        new(2370, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.AlternateControl(), 0x05)]),
        new(4750, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)], Compressed: true),
        new(0, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x06)]),
        new(4770, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.AlternateControl(), 0x07)]),
        new(110, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)], Compressed: true),
        new(4250, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x08)]),
        new(2490, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)], Compressed: true),
        new(6250, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.AlternateControl(), 0x09)]),
        new(0, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)], Compressed: true),
    ];
}
