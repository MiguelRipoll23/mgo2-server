namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The item-box group the dedicated host prepends to its beat frame, as measured
/// from the solo window of <c>mgo2-game1.pcapng</c>. Split out of
/// <see cref="MatchRecordFramesUtils"/> so neither file exceeds the repository's
/// size limit; the bodies are <see cref="ItemBoxRecordBodiesUtils"/>'.
/// </summary>
public static class ItemBoxFramesUtils
{
    /// <summary>
    /// Beat frames between one item-box group and the next: eight, measured. The
    /// solo window carries the group in 38 frames and all but four of the gaps
    /// between them are eight frames (0.26 s), with the group's content rotating
    /// between the two boxes' state and the round's spawn bodies.
    /// </summary>
    public const int ItemBoxPeriodFrames = 8;

    /// <summary>
    /// Offset of the first item-box group within its period: the group first
    /// appears six frames (0.2 s) after the match-start run.
    /// </summary>
    public const int ItemBoxPhaseFrames = 6;

    /// <summary>
    /// The item-box group: the two boxes' state under
    /// <see cref="ItemBoxRecordBodiesUtils.ItemBoxIdentifier"/> and under
    /// <see cref="ItemBoxRecordBodiesUtils.ItemBoxStateIdentifier"/>, each a
    /// six-byte body of the box's index and five zero bytes.
    /// </summary>
    /// <returns>The records of the group, in the captured order.</returns>
    public static List<MatchRecordFramesUtils.RecordMessage> ItemBoxGroup() =>
    [
        new(ItemBoxRecordBodiesUtils.ItemBoxIdentifier, ItemBoxRecordBodiesUtils.ItemBoxState(0)),
        new(ItemBoxRecordBodiesUtils.ItemBoxIdentifier, ItemBoxRecordBodiesUtils.ItemBoxState(1)),
        new(ItemBoxRecordBodiesUtils.ItemBoxStateIdentifier, ItemBoxRecordBodiesUtils.ItemBoxState(0)),
        new(ItemBoxRecordBodiesUtils.ItemBoxStateIdentifier, ItemBoxRecordBodiesUtils.ItemBoxState(1)),
    ];
}
