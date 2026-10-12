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
    /// Records a settling group writes under
    /// <see cref="MatchRecordGenerationUtils.ItemBox"/>: the two boxes' filler
    /// pair.
    /// </summary>
    public const int ItemBoxRecords = 2;

    /// <summary>
    /// Records the group writes under
    /// <see cref="MatchRecordGenerationUtils.ItemBoxState"/>: one per box.
    /// </summary>
    public const int ItemBoxStateRecords = 2;

    /// <summary>
    /// Groups the round opens with, one per box, each carrying that box's own
    /// event rather than the filler pair.
    /// </summary>
    public const int OpeningEventGroups = ItemBoxRecordBodiesUtils.OpeningEventCount;

    /// <summary>
    /// Which group of the round a beat frame carries: 0 for the round's first
    /// group, 1 for the next, and so on. The capture writes one group per
    /// <see cref="ItemBoxPeriodFrames"/> frames, so the group's ordinal is the
    /// frame's, divided.
    /// </summary>
    /// <param name="frameIndex">Beat frames written in the round so far.</param>
    /// <returns>The group's ordinal within the round, from zero.</returns>
    public static int GroupIndex(int frameIndex) =>
        (frameIndex - ItemBoxPhaseFrames) / ItemBoxPeriodFrames;

    /// <summary>
    /// Records a group writes under
    /// <see cref="MatchRecordGenerationUtils.ItemBox"/>: the four the opening
    /// events carry, and the filler pair for every group after them.
    /// </summary>
    /// <param name="groupIndex">Group's ordinal within the round, from zero.</param>
    /// <returns>Records this group writes under that identifier.</returns>
    public static int ItemBoxRecordsFor(int groupIndex) =>
        groupIndex < OpeningEventGroups
            ? ItemBoxRecordBodiesUtils.ItemBoxEventRecords
            : ItemBoxRecords;

    /// <summary>
    /// The item-box group: the round's boxes under its first item-box identifier
    /// and the filler pair under its second.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two identifiers come from the round's own generation rather than
    /// from <see cref="ItemBoxRecordBodiesUtils"/>, because the group is
    /// generation-bound: a round played under the sibling set writes
    /// <c>0x1250</c>/<c>0x125a</c> where the first round writes
    /// <c>0x1a50</c>/<c>0x1a5a</c>, and a round uses one pair only. game1 shows
    /// it from both ends — 106 s of its first round carry the first pair and no
    /// sibling form, its second carries the sibling pair and none of the
    /// first — and game2 the same way round, its client having opened that
    /// capture's first round with a <c>0x12de</c> report.
    /// </para>
    /// <para>
    /// <b>The group numbers its records, one sequence per identifier.</b> Its
    /// fourth byte is the stream's own counter, not a constant: the recorded host
    /// stamps the two <c>0x1a50</c> records and the two <c>0x1a5a</c> records with
    /// successive values, and each family's counter climbs with every group it
    /// writes. game1's first group carries <c>0x1a50</c> under <c>0</c> and
    /// <c>1</c> and <c>0x1a5a</c> under <c>0</c> and <c>1</c>, the next under
    /// <c>2</c>, <c>3</c> and <c>2</c>, <c>3</c>, and so on to <c>0x37</c>. It is
    /// what the client keys its answer on — the live client answers
    /// <c>0x5a50</c> with the same fourth byte it received, one answer per
    /// sequence — so a group that pins the byte to zero hands the client one
    /// record it can accept and a rank of duplicates behind it, and the family's
    /// window never drains. That is the shape a live client went black under.
    /// </para>
    /// <para>
    /// <b>The round's first two groups are the boxes' own events, not the
    /// filler.</b> A box is created by an event of four records — its head, its
    /// item, its position and the event's tail — and the six-byte filler pair is
    /// only the statement that a box is still there. The recorded host writes its
    /// two boxes' events in the round's first two groups (0.2 s and 0.46 s after
    /// the run) and the filler alone every group after that, so the group's
    /// content turns over once the round is open: the first two carry four
    /// <see cref="MatchRecordGenerationUtils.ItemBox"/> records each and the rest
    /// carry two, while the second identifier's pair does not change. Writing the
    /// filler from the first group on tells the client about boxes it was never
    /// told exist.
    /// </para>
    /// </remarks>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="groupIndex">
    /// Group's ordinal within the round, from zero, from
    /// <see cref="GroupIndex"/>.
    /// </param>
    /// <param name="itemBoxSequence">
    /// Fourth byte of the group's first <see cref="MatchRecordGenerationUtils.ItemBox"/>
    /// record; each later record of the group carries the next value.
    /// </param>
    /// <param name="itemBoxStateSequence">
    /// Fourth byte of the group's first <see cref="MatchRecordGenerationUtils.ItemBoxState"/>
    /// record; the second carries the next value.
    /// </param>
    /// <returns>The records of the group, in the captured order.</returns>
    public static List<MatchRecordFramesUtils.RecordMessage> ItemBoxGroup(
        MatchRecordGenerationUtils generation,
        int groupIndex,
        byte itemBoxSequence,
        byte itemBoxStateSequence)
    {
        var itemBoxRecords = ItemBoxRecordsFor(groupIndex);
        var records = new List<MatchRecordFramesUtils.RecordMessage>(
            itemBoxRecords + ItemBoxStateRecords);

        var openingEvent = groupIndex < OpeningEventGroups
            ? groupIndex
            : -1;

        for (var index = 0; index < itemBoxRecords; index++)
        {
            records.Add(new(
                generation.ItemBox,
                openingEvent >= 0
                    ? ItemBoxRecordBodiesUtils.ItemBoxEvent(openingEvent, index)
                    : ItemBoxRecordBodiesUtils.ItemBoxState((byte)index),
                (byte)(itemBoxSequence + index)));
        }

        for (var index = 0; index < ItemBoxStateRecords; index++)
        {
            records.Add(new(
                generation.ItemBoxState,
                ItemBoxRecordBodiesUtils.ItemBoxState((byte)index),
                (byte)(itemBoxStateSequence + index)));
        }

        return records;
    }
}
