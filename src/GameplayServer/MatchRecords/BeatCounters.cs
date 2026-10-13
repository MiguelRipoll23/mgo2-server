namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The beat's mutable counters, as <see cref="MatchRecordService"/> writes the
/// stream frame by frame.
/// </summary>
/// <remarks>
/// <para>
/// Split out of <see cref="MatchRecordService"/> so neither file exceeds the
/// repository's size limit. The counters live in a class rather than in locals
/// because the beat is crossed from an async method, which cannot take them by
/// reference.
/// </para>
/// <para>
/// The identifier set is not here either: the beat reads the round's own off
/// <see cref="MatchRoundStateService.Generation"/> frame by frame, and a round
/// change restarts these counters along with the frame index, so none of them
/// outlives the round it was counting
/// (<c>MatchRecordService.BeatAsync</c>).
/// </para>
/// <para>
/// <b>The fourth byte of a clock or a control record is not one of these.</b> It
/// is the record stream's own counter, one per twelve-bit identifier, held by
/// <see cref="MatchRecordSequenceService"/>: the clock insertions take the
/// operations stream's next value and the control insertions the room's exchange's,
/// which is what stops a beat from numbering an <c>0x1a54</c> clock and an
/// <c>0xd001</c> control from one run of values the capture does not write
/// (<c>docs/analysis/replay-findings.md</c> §5.7).
/// </para>
/// </remarks>
internal sealed class BeatCounters
{
    /// <summary>Beat frames written so far.</summary>
    public int FrameIndex { get; set; }

    /// <summary>State blobs written so far.</summary>
    public int BlobsWritten { get; set; }

    /// <summary>
    /// Fourth byte the next <c>0x1a50</c> item-box record carries. It is the
    /// family's own index, one sequence per identifier, and the client answers
    /// against it — see <see cref="ItemBoxFramesUtils.ItemBoxGroup"/>.
    /// </summary>
    public byte ItemBoxSequence { get; set; }

    /// <summary>Fourth byte the next <c>0x1a5a</c> item-box state record carries.</summary>
    public byte ItemBoxStateSequence { get; set; }
}
