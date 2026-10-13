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
/// <param name="SessionFlags2">Session-record flags2 this frame's insertion carries.</param>
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
    byte SessionFlags2,
    int BlobsWritten,
    byte ItemBoxSequence,
    byte ItemBoxStateSequence)
{
    /// <summary>
    /// The head insertion for this frame, or <c>null</c> when the capture shows
    /// none at this index.
    /// </summary>
    public MatchRecordFramesUtils.RecordMessage? Insertion
    {
        get
        {
            if (FrameIndex % MatchRecordFramesUtils.OperationsPeriodFrames == 0)
            {
                return MatchRecordFramesUtils.OperationsInsertion(
                    Generation,
                    alternate: false,
                    SessionFlags2,
                    RoundClockUnits);
            }

            if (FrameIndex % MatchRecordFramesUtils.OperationsClassBitPeriodFrames
                == MatchRecordFramesUtils.OperationsClassBitPhaseFrames)
            {
                return MatchRecordFramesUtils.OperationsInsertion(
                    Generation,
                    alternate: true,
                    SessionFlags2,
                    RoundClockUnits);
            }

            if (FrameIndex % MatchRecordFramesUtils.StateBlobPeriodFrames == 0)
            {
                // The first recorded insertion is the zero-valued 40-byte frame
                // (hdr 0x8648), followed by the 100-valued 41-byte frame;
                // preserve that measured alternation.
                return MatchRecordFramesUtils.StateBlobInsertion(live: BlobsWritten % 2 != 0);
            }

            return FrameIndex % MatchRecordFramesUtils.ControlPeriodFrames == 0
                ? MatchRecordFramesUtils.ControlInsertion(
                    MatchRecordFramesUtils.SteadyControlBody(),
                    SessionFlags2)
                : null;
        }
    }

    /// <summary>
    /// Whether the item-box group rides in this frame, behind the rules pair. It
    /// rides in one frame in every eight, the same frame the capture compresses.
    /// </summary>
    public bool CarriesItemBoxes =>
        FrameIndex % ItemBoxFramesUtils.ItemBoxPeriodFrames == ItemBoxFramesUtils.ItemBoxPhaseFrames;

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
        Insertion is { Type: UdpCommandConstants.GameStateBlob } || CarriesItemBoxes;

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
    public bool CarriesStateBlob => Insertion is { Type: UdpCommandConstants.GameStateBlob };

    /// <summary>
    /// Whether this frame's insertion carries the session-flags2 byte, and so
    /// advances the round's flags2 counter.
    /// </summary>
    /// <remarks>
    /// The clock and control insertions do; the state blob does not. That is the
    /// capture's own split — the blob rides under a fourth byte of its own — so
    /// counting every insertion alike would advance the counter on frames the
    /// recorded host never moved it on.
    /// </remarks>
    public bool CarriesSessionFlags => Insertion is { } insertion
        && insertion.Type != UdpCommandConstants.GameStateBlob;

    /// <summary>
    /// The records of this frame, in send order.
    /// </summary>
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
    public List<MatchRecordFramesUtils.RecordMessage> Records()
    {
        var insertions = new List<MatchRecordFramesUtils.RecordMessage>();
        if (Insertion is { } insertion)
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