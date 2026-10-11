namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The item-box bodies the dedicated host carries in its beat frame, as measured
/// from the solo window of <c>mgo2-game1.pcapng</c>. Split out of
/// <see cref="MatchRecordBodiesUtils"/> so neither file exceeds the repository's
/// size limit.
/// </summary>
public static class ItemBoxRecordBodiesUtils
{
    /// <summary>
    /// Measured identifier of an item box's state record — one of the
    /// world-object group the recorded host carries in every eighth beat frame
    /// from 0.2 s after the match-start run on.
    /// </summary>
    /// <remarks>
    /// The replay console's parser does not dispatch this identifier, so the
    /// reading here is the capture's: the body opens with the box's index and
    /// the rest is the box's state. The spawn bodies the groups alternate with
    /// (index <c>0</c> at head code <c>0x74</c>, index <c>1</c> at <c>0x8c</c>,
    /// index <c>2</c> at <c>0xb4</c>) are the live round's own item placement and
    /// are not replayed; only the recurring state group is. Two other forms of
    /// the identifier are on the wire: the round's own sibling,
    /// <c>0x1250</c>/<c>0x125a</c> (a <c>0x800</c> bit along), which a round
    /// played under the sibling set writes instead — see
    /// <see cref="MatchRecordGenerationUtils"/> — and the payload-class form
    /// <c>0x9a50</c>/<c>0x9a5a</c> (<c>0x8000</c> along), which the capture
    /// writes occasionally alongside it.
    /// </remarks>
    public const ushort ItemBoxIdentifier = 0x1a50;

    /// <summary>Measured identifier of the item box's second state record.</summary>
    public const ushort ItemBoxStateIdentifier = 0x1a5a;

    /// <summary>Length of an item box's recurring state body.</summary>
    public const int ItemBoxStateLength = 6;

    /// <summary>
    /// The recurring item-box state body: the box's index followed by five zero
    /// bytes, which is what the capture writes for both boxes on the eight-frame
    /// beat.
    /// </summary>
    /// <param name="index">Index of the box, 0 or 1 in the capture.</param>
    /// <returns>The record body.</returns>
    public static byte[] ItemBoxState(byte index) => [index, 0, 0, 0, 0, 0];
}
