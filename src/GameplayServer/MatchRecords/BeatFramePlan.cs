using Mgo2Server.Shared.Constants;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Assembles one beat frame: which insertion rides at its head, whether the
/// item-box group does, whether the frame goes out compressed, and whether the
/// vitals record has joined yet.
/// </summary>
/// <remarks>
/// <para>
/// Split out of <see cref="MatchRecordService"/> so neither file exceeds the
/// repository's size limit. The periods and phases this applies are measured in
/// <see cref="MatchRecordFramesUtils"/>; this file only decides a single frame
/// from its index and the round's running counters.
/// </para>
/// <para>
/// The two counters are arguments rather than derived from the frame index
/// because the capture's counts are not arithmetic in the index: the
/// session-flags2 byte advances once per <em>insertion</em> and the blob
/// alternation once per <em>blob</em>, so a frame index cannot stand in for
/// either.
/// </para>
/// </remarks>
/// <param name="FrameIndex">Beat frames written in the round so far.</param>
/// <param name="Generation">Identifier set of the round being played.</param>
/// <param name="RoundClockUnits">
/// Units of the round clock still to run, in the stream's own 125 ms ticks. It is
/// read from the round on each frame rather than captured once, so the clock the
/// beat writes counts down with the round (
/// <see cref="MatchRoundStateService.RemainingUnits"/>).
/// </param>
/// <param name="VitalsOnset">
/// Whether a player has reported vitals in this round, which is the record the
/// vitals record's onset is gated on in place of the capture's frame count
/// (<see cref="MatchRoundStateService.VitalsArrived"/>).
/// </param>
/// <param name="BlobsWritten">State blobs the round has written so far.</param>
/// <param name="ItemBoxSequence">
/// Fourth byte the frame's first item-box record carries, when the frame carries
/// the group.
/// </param>
/// <param name="ItemBoxStateSequence">
/// Fourth byte the frame's first item-box state record carries, when the frame
/// carries the group.
/// </param>
public sealed record BeatFramePlan(
    int FrameIndex,
    MatchRecordGenerationUtils Generation,
    int RoundClockUnits,
    bool VitalsOnset,
    int BlobsWritten,
    byte ItemBoxSequence,
    byte ItemBoxStateSequence)
{
    /// <summary>
    /// Which insertion this frame carries, decided from its index alone.
    /// </summary>
    private BeatInsertion InsertionKind
    {
        get
        {
            if (FrameIndex % MatchRecordFramesUtils.OperationsPeriodFrames == 0)
            {
                return BeatInsertion.Operations;
            }

            if (FrameIndex % MatchRecordFramesUtils.OperationsClassBitPeriodFrames
                == MatchRecordFramesUtils.OperationsClassBitPhaseFrames)
            {
                return BeatInsertion.OperationsClassBit;
            }

            if (FrameIndex % MatchRecordFramesUtils.StateBlobPeriodFrames == 0)
            {
                return BeatInsertion.StateBlob;
            }

            return FrameIndex % MatchRecordFramesUtils.ControlPeriodFrames == 0
                ? BeatInsertion.Control
                : BeatInsertion.None;
        }
    }

    /// <summary>The insertion kinds a beat frame can open on.</summary>
    private enum BeatInsertion
    {
        /// <summary>The frame opens on the rules pair alone.</summary>
        None,

        /// <summary>The round's clock, under the operations identifier.</summary>
        Operations,

        /// <summary>The round's clock, under the class-bit form.</summary>
        OperationsClassBit,

        /// <summary>A state blob, under its own identifier and attribute class.</summary>
        StateBlob,

        /// <summary>A control byte, under the room's own control identifier.</summary>
        Control,
    }

    /// <summary>
    /// The head insertion for this frame, or <c>null</c> when the capture shows
    /// none at this index.
    /// </summary>
    /// <remarks>
    /// <b>The fourth byte is taken here and not passed in.</b> A clock record and a
    /// control record are numbered on different streams — the round's operations
    /// id and the room's exchange id — so the value is taken at the moment the
    /// record is written, from the counter of the identifier it travels under
    /// (<see cref="MatchRecordSequenceService"/>). Taking it for a frame that
    /// carries no such insertion would spend a value the capture never writes.
    /// </remarks>
    /// <param name="sequences">The stream counters the record's fourth byte comes from.</param>
    /// <returns>The insertion record, or <c>null</c>.</returns>
    public MatchRecordFramesUtils.RecordMessage? Insertion(MatchRecordSequenceService sequences) =>
        InsertionKind switch
        {
            BeatInsertion.Operations => MatchRecordFramesUtils.OperationsInsertion(
                Generation,
                alternate: false,
                sequences.Take(Generation.Operations),
                RoundClockUnits),
            BeatInsertion.OperationsClassBit => MatchRecordFramesUtils.OperationsInsertion(
                Generation,
                alternate: true,
                sequences.Take(Generation.OperationsClassBit),
                RoundClockUnits),

            // The first recorded insertion is the zero-valued 40-byte frame
            // (hdr 0x8648), followed by the 100-valued 41-byte frame; preserve
            // that measured alternation.
            BeatInsertion.StateBlob => MatchRecordFramesUtils.StateBlobInsertion(live: BlobsWritten % 2 != 0),

            // The control takes the room's exchange counter, not the clock's: the
            // capture's controls sit inside the id-1 sequence, between the roster
            // records that share it.
            BeatInsertion.Control => MatchRecordFramesUtils.ControlInsertion(
                MatchRecordFramesUtils.SteadyControlBody(),
                sequences.Take(Shared.Constants.UdpCommandConstants.InGameControl)),

            _ => null,
        };

    /// <summary>
    /// Beat frames through which the round's item-box group rides: 303, the
    /// round's first ten seconds at the beat's own 33 ms frame.
    /// </summary>
    /// <remarks>
    /// <b>The group is the round's opening, not a cadence of the beat.</b> Every
    /// capture writes it in bursts inside the round's first seconds and then
    /// leaves the beat quiet until the round's next item-box event — the events
    /// themselves being the live round's own, which this host does not simulate
    /// (<see cref="ItemBoxFramesUtils.ItemBoxGroup"/>):
    /// <c>mgo2-game-started-on-briefing</c> writes 90 of its 244 round-one
    /// records inside ten seconds of the run (9 a second, in two bursts) and then
    /// none at all in the forty-five seconds that follow;
    /// <c>mgo2-started-briefing-then-solo-game</c> writes four bursts inside 8.4 s
    /// and none until its round's end; <c>mgo2-game1</c> five bursts over 12.5 s.
    /// A beat that carried the group for the whole round wrote 418 of them in a
    /// minute against the 59 the same window of that capture holds, and 780 over
    /// the live round's 112 s against this one's 90
    /// (<c>docs/analysis/join-stall-after-mode2.md</c> §8 and §9).
    /// </remarks>
    public const int ItemBoxOpeningFrames = 303;

    /// <summary>
    /// Whether the item-box group rides in this frame, behind the rules pair. It
    /// rides in one frame in every eight, the same frame the capture compresses,
    /// and only inside the round's own opening
    /// (<see cref="ItemBoxOpeningFrames"/>).
    /// </summary>
    public bool CarriesItemBoxes =>
        FrameIndex <= ItemBoxOpeningFrames
        && FrameIndex % ItemBoxFramesUtils.ItemBoxPeriodFrames == ItemBoxFramesUtils.ItemBoxPhaseFrames;

    /// <summary>
    /// Which group of the round this frame carries, from zero, when it carries
    /// one at all.
    /// </summary>
    public int ItemBoxGroupIndex => ItemBoxFramesUtils.GroupIndex(FrameIndex);

    /// <summary>
    /// Fourth byte the next item-box record of this family will carry once this
    /// frame has been written, so a round that carried the group keeps numbering
    /// it by the records it wrote: four on a group that carries the round's own
    /// box event, two on the filler groups after them. Unchanged on a frame that
    /// carries none, which leaves the counter alone.
    /// </summary>
    public byte ItemBoxSequenceAfter => CarriesItemBoxes
        ? (byte)(ItemBoxSequence + ItemBoxFramesUtils.ItemBoxRecordsFor(ItemBoxGroupIndex))
        : ItemBoxSequence;

    /// <summary>
    /// Fourth byte the next item-box state record will carry once this frame has
    /// been written. Unchanged on a frame that carries none.
    /// </summary>
    public byte ItemBoxStateSequenceAfter => CarriesItemBoxes
        ? (byte)(ItemBoxStateSequence + ItemBoxFramesUtils.ItemBoxStateRecords)
        : ItemBoxStateSequence;

    /// <summary>
    /// Whether this frame goes out compressed.
    /// </summary>
    /// <remarks>
    /// The marker follows the frame's content, not the phase: the recorded host
    /// compresses a beat frame when a state blob rides in it (hdr 0x8648) or an
    /// item-box group does (hdr 0x801f) and leaves the clock-only and
    /// control-only ones uncompressed (hdr 0x0626, 0x062d, 0x0682), because
    /// those two are the only insertions that pay for the compression. Sending
    /// them all plain writes bytes the capture does not, and sending them all
    /// compressed would mark frames the peer saw unmarked.
    /// </remarks>
    public bool Compressed =>
        InsertionKind == BeatInsertion.StateBlob || CarriesItemBoxes;

    /// <summary>
    /// Whether the vitals record rides in this frame.
    /// </summary>
    /// <remarks>
    /// <b>The onset is the round's first vitals, with the capture's own offset as
    /// its floor.</b> The recorded host writes the rules-roster pair alone for the
    /// round's first 27.0 s and adds the vitals record after it, because that is
    /// when its round had health to report; a live host has the record the moment
    /// a player reports any (<see cref="VitalsOnset"/>). The measured offset is
    /// kept as a floor rather than dropped, so a room whose players never report
    /// vitals still carries the record the client's beat was measured with.
    /// </remarks>
    public bool IncludeVitals => VitalsOnset || FrameIndex >= MatchRecordFramesUtils.VitalsOnsetFrames;

    /// <summary>
    /// Whether this frame's insertion is a state blob, which is what advances
    /// the round's blob count and so its zero/live alternation.
    /// </summary>
    public bool CarriesStateBlob => InsertionKind == BeatInsertion.StateBlob;

    /// <summary>
    /// The records of this frame, in send order.
    /// </summary>
    /// <param name="sequences">The stream counters the insertions' fourth bytes come from.</param>
    /// <remarks>
    /// <b>The item-box group rides behind the rules pair.</b> game1's round-1
    /// item-box frames read <c>0a61 0a61 1a50 … 1a5a …</c> with the pair first,
    /// and <c>090c 0a61 0a61 1a50 … 1a5a …</c> once the vitals record has joined,
    /// so the group is the frame's last records and the clock/blob/control
    /// insertion stays at the head. The capture's order is not uniform across the
    /// match (a round-2 frame with a blob reads <c>1a50 … 1a5a … 0002 090c 0a61
    /// 0a61</c>), so this follows the opening round, whose frames the client
    /// answers first; see <see cref="MatchRecordFramesUtils.BeatFrame"/>.
    /// </remarks>
    /// <returns>The frame's records.</returns>
    public List<MatchRecordFramesUtils.RecordMessage> Records(MatchRecordSequenceService sequences)
    {
        var insertions = new List<MatchRecordFramesUtils.RecordMessage>();
        if (Insertion(sequences) is { } insertion)
        {
            insertions.Add(insertion);
        }

        var itemBoxes = CarriesItemBoxes
            ? ItemBoxFramesUtils.ItemBoxGroup(
                Generation,
                ItemBoxGroupIndex,
                ItemBoxSequence,
                ItemBoxStateSequence)
            : null;

        return MatchRecordFramesUtils.BeatFrame(Generation, insertions, IncludeVitals, itemBoxes);
    }
}