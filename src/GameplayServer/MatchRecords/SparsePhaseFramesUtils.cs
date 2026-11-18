using Mgo2Server.Shared.Constants;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The sparse phase: the records a peer is written before any round exists, in the
/// order the recorded host wrote them.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is split from the beat because it is replayed and the beat is not.</b>
/// The steps below are the recorded host's own draw of its channel while it waits
/// for the room to report ready — the bodies are read off <c>mgo2-game1.pcapng</c>
/// and nothing here is derived — whereas the beat is written against a round that
/// is running, with a clock that counts. Keeping both in one file is how a constant
/// of one becomes a literal in the other.
/// </para>
/// <para>
/// <b>The capture's waits are not reproduced.</b> The recorded host spread these
/// records over 42.2 s because it had nothing else to say; every one of them is a
/// control byte or a full state block the client can take at once, and holding them
/// back only postpones state the briefing is already waiting on. They are written in
/// order as soon as the roster answer has gone out, and the briefing's own writers —
/// the countdown, the ready set and the room's pair — are what pace the channel from
/// there.
/// </para>
/// </remarks>
public static class SparsePhaseFramesUtils
{
    /// <summary>
    /// The sparse phase, in send order: each entry is written as one datagram.
    /// </summary>
    /// <remarks>
    /// <b>The controls' fourth bytes are the capture's own values and not the
    /// exchange's counter.</b> They read <c>02</c>…<c>09</c> across the phase, and
    /// those values overlap the ones the join's run and its burst take from the
    /// room's exchange (<c>00</c>–<c>02</c> and <c>03</c>–<c>06</c> under a
    /// one-player roster), so a stream that took them from
    /// <see cref="MatchRecordSequenceService"/> would number them differently from
    /// the capture. They are pinned for that reason, not by oversight; what the
    /// values mean is **[U]**.
    /// </remarks>
    /// <returns>The steps in send order, ending at the last state blob.</returns>
    public static List<IReadOnlyList<MatchRecordFramesUtils.RecordMessage>> SparseSteps() =>
    [
        [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x02)],
        [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)],
        [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x02)],
        [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.AlternateControl(), 0x03)],
        [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x03)],
        [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)],
        [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x04)],
        [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)],
        [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.AlternateControl(), 0x05)],
        [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)],
        [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x06)],
        [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.AlternateControl(), 0x07)],
        [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)],
        [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x08)],
        [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)],
        [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.AlternateControl(), 0x09)],
        [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), MatchRecordFramesUtils.StateBlobSparseAttributeClass)],
    ];
}
