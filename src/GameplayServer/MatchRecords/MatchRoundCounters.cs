namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// One round's mutable beat counters, as <see cref="MatchRecordService"/> writes
/// the round frame by frame.
/// </summary>
/// <remarks>
/// <para>
/// Split out of <see cref="MatchRecordService"/> so neither file exceeds the
/// repository's size limit. The counters live in a class rather than in locals
/// because the round is crossed from an async method, which cannot take them by
/// reference.
/// </para>
/// <para>
/// The identifier set is not here: it belongs to the room, which opens the round
/// and decides which generation it plays under.
/// </para>
/// </remarks>
internal sealed class MatchRoundCounters
{
    /// <summary>Beat frames written in this round.</summary>
    public int FrameIndex { get; set; }

    /// <summary>Session-record flags2 the next beat insertion carries.</summary>
    public byte SessionFlags2 { get; set; } = MatchRecordFramesUtils.SteadySessionFlagsStart;

    /// <summary>State blobs written in this round.</summary>
    public int BlobsWritten { get; set; }

    /// <summary>Run-shaped repeats written inside this round's beat.</summary>
    public int RepeatsWritten { get; set; }

    /// <summary>
    /// Fourth byte the round's next <c>0x1a50</c> item-box record carries. It is
    /// the family's own index, one sequence per identifier, and the client
    /// answers against it — see <see cref="ItemBoxFramesUtils.ItemBoxGroup"/>.
    /// </summary>
    public byte ItemBoxSequence { get; set; }

    /// <summary>Fourth byte the round's next <c>0x1a5a</c> item-box state record carries.</summary>
    public byte ItemBoxStateSequence { get; set; }

    /// <summary>Resets the counters for a newly opened round.</summary>
    public void Reset()
    {
        FrameIndex = 0;
        SessionFlags2 = MatchRecordFramesUtils.SteadySessionFlagsStart;
        BlobsWritten = 0;
        RepeatsWritten = 0;
        ItemBoxSequence = 0;
        ItemBoxStateSequence = 0;
    }
}
