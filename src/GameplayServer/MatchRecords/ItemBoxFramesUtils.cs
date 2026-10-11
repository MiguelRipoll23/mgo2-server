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
    /// The item-box group: the two boxes' state under the round's first
    /// item-box identifier and under its second, each a six-byte body of the
    /// box's index and five zero bytes.
    /// </summary>
    /// <remarks>
    /// The two identifiers come from the round's own generation rather than
    /// from <see cref="ItemBoxRecordBodiesUtils"/>, because the group is
    /// generation-bound: a round played under the sibling set writes
    /// <c>0x1250</c>/<c>0x125a</c> where the first round writes
    /// <c>0x1a50</c>/<c>0x1a5a</c>, and a round uses one pair only. game1 shows
    /// it from both ends — 106 s of its first round carry the first pair and no
    /// sibling form, its second carries the sibling pair and none of the
    /// first — and game2 the same way round, its client having opened that
    /// capture's first round with a <c>0x12de</c> report.
    /// </remarks>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <returns>The records of the group, in the captured order.</returns>
    public static List<MatchRecordFramesUtils.RecordMessage> ItemBoxGroup(
        MatchRecordGenerationUtils generation) =>
    [
        new(generation.ItemBox, ItemBoxRecordBodiesUtils.ItemBoxState(0)),
        new(generation.ItemBox, ItemBoxRecordBodiesUtils.ItemBoxState(1)),
        new(generation.ItemBoxState, ItemBoxRecordBodiesUtils.ItemBoxState(0)),
        new(generation.ItemBoxState, ItemBoxRecordBodiesUtils.ItemBoxState(1)),
    ];
}
