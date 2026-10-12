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
/// The identifier set is not here. Nothing in the beat moves it: no round is
/// opened, so the writer holds <see cref="MatchRecordGenerationUtils.First"/> for
/// the whole stream.
/// </para>
/// </remarks>
internal sealed class BeatCounters
{
    /// <summary>Beat frames written so far.</summary>
    public int FrameIndex { get; set; }

    /// <summary>Session-record flags2 the next beat insertion carries.</summary>
    public byte SessionFlags2 { get; set; } = MatchRecordFramesUtils.SteadySessionFlagsStart;

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
