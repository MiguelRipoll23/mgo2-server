namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The run as the tick behind it writes it again: the same record set, re-sent.
/// </summary>
/// <remarks>
/// Split out of <c>MatchStartRunFramesUtils.cs</c> to keep both files under the
/// 400-line limit; the run's own build is there.
/// </remarks>
public static partial class MatchStartRunFramesUtils
{
    /// <summary>
    /// The run's records as the tick behind it writes them again: everything the
    /// run carries except its opening — the marker, when the run is the report's
    /// answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every capture writes the run twice, and the second one is this.</b> The
    /// run answering a report and the set behind it are one tick apart —
    /// <c>mgo2-started-briefing-then-solo-game</c> t+57.626 and t+59.993,
    /// <c>mgo2-game-started-on-briefing</c> t+58.255 and t+60.383, and its round's
    /// t+198.371 and t+200.869, <c>mgo2-game3</c> t+59.596 and t+62.093 — and the
    /// second carries the state block, the short record behind it, the arrays, the
    /// room's stage, the close triple, the clock, the payload and the rules pair
    /// with no marker in front of them and no roster heads. The records keep the
    /// ordinals the run gave them, which is what makes the repeat the same set
    /// re-sent rather than a second run.
    /// </para>
    /// <para>
    /// The marker, where the run carries one, is the answer to the peer's own
    /// report and there is nothing for it to answer a second time, so it is the
    /// whole of what the repeat drops — by its place in the run and not by its
    /// identifier. A briefing's run carries no marker (its report was answered
    /// beside the publication it drew), so its count is zero.
    /// </para>
    /// <para>
    /// <b>The empty <c>0x5001</c> pair is not the repeat's to drop either.</b> It
    /// is the answer to the peer's entry echo and is placed in front of the run at
    /// write, not built into it, so a repeat never carries one
    /// (<see cref="MatchStartRunService.WithEntryAnswers"/>).
    /// </para>
    /// </remarks>
    /// <param name="generation">Identifier set the run was written under.</param>
    /// <param name="run">The run as it was written.</param>
    /// <returns>The records to write behind the run.</returns>
    public static List<MatchRecordFramesUtils.RecordMessage> RepeatOf(
        MatchRecordGenerationUtils generation,
        IReadOnlyList<MatchRecordFramesUtils.RecordMessage> run)
    {
        // A briefing's run opens on its state block, so the marker is not there to
        // drop: the opening count is the one that run was built with.
        var answersReport = run.Count > 0 && run[0].Type == generation.MatchStartMarker;

        // The item-box group is left behind as well, because its bytes are
        // sequence numbers: a repeat would hand the client a second record with a
        // number it has already accepted, which is the shape the family's window
        // never drains under (`ItemBoxFramesUtils`). The repeat writes its own
        // groups on the counters instead
        // (`MatchStartRunService.RepeatItemBoxGroupsAsync`).
        // A briefing's repeat leaves the join-state answer behind with its opening.
        // The reference's fresh-room run writes `0x025c 00 00 00 00` in its frame
        // (`briefing-then-solo-game` t+9.780, fourth byte 6) and the tick-behind copy
        // at t+11.579 carries thirteen records with no `0x025c` among them: the pair
        // of heads and the join-state answer are both the run's opening and both are
        // dropped. Repeating the answer hands the client a second "you have joined"
        // every tick, on the record its own join path restarts from.
        var briefing = !answersReport;

        return
        [
            .. run
                .Skip(briefing ? BriefingRunOpeningCount : RunOpeningCount)
                .Where(record => record.Type != generation.ItemBox
                    && record.Type != generation.ItemBoxState
                    && !(briefing && record.Type == generation.JoinState)),
        ];
    }
}
