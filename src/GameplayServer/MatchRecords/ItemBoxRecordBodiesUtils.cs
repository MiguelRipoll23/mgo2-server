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
    /// the rest is the box's state. Two other forms of the identifier are on the
    /// wire: the round's own sibling, <c>0x1250</c>/<c>0x125a</c> (a <c>0x800</c>
    /// bit along), which a round played under the sibling set writes instead —
    /// see <see cref="MatchRecordGenerationUtils"/> — and the payload-class form
    /// <c>0x9a50</c>/<c>0x9a5a</c> (<c>0x8000</c> along), which the capture
    /// writes alongside it on some of the repeated groups.
    /// </remarks>
    public const ushort ItemBoxIdentifier = 0x1a50;

    /// <summary>Measured identifier of the item box's second state record.</summary>
    public const ushort ItemBoxStateIdentifier = 0x1a5a;

    /// <summary>Length of an item box's recurring state body.</summary>
    public const int ItemBoxStateLength = 6;

    /// <summary>
    /// Records one item-box event writes under the group's own identifier, in
    /// sub-type order: <c>00</c> the box's head, <c>01</c> its item, <c>02</c>
    /// its position and <c>03</c> the event's tail.
    /// </summary>
    public const int ItemBoxEventRecords = 4;

    /// <summary>
    /// The complete bodies of the two events the recorded round opens with, box
    /// by box, in sub-type order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the item-box group's real content, and the six-byte pair below
    /// is the replay console's "filler" that rides beside it: a box is created,
    /// moved or emptied by one event of up to four records, and the all-zero pair
    /// that follows every eight frames is what says the box is still there. The
    /// recorded host writes the two events of its own round's boxes in the first
    /// two groups — 0.2 s and 0.46 s after the match-start run — and then the
    /// filler pair alone, so a host that replays the filler and not the event
    /// never tells the client its boxes exist.
    /// </para>
    /// <para>
    /// The bytes are the capture's own, not derived: the round's boxes stand
    /// where that round put them, and reproducing them is the same kind of replay
    /// as the rest of the recorded host's stream. The events the capture writes
    /// later in the round (a box taken and dropped again, which moves it) are not
    /// here — they are the live round's own play, and they arrive irregularly.
    /// </para>
    /// </remarks>
    private static readonly byte[][] OpeningEventBodies =
    [
        // The first box: index 0 at head 0x74, its item, its position, the tail.
        [
            0x00, 0x74, 0x01, 0x00, 0x00, 0x00,
        ],
        [
            0x01, 0x8c, 0x00, 0x01, 0x00, 0x00,
        ],
        [
            0x02, 0xb4, 0xfb, 0x08, 0x07, 0x72, 0x06, 0x00, 0x00, 0x00,
        ],
        [
            0x03, 0x00, 0x00, 0x00, 0x21, 0x00, 0x21, 0x00, 0x21,
        ],

        // The second box: index 0 at head 0x74 as well, and its own placement.
        [
            0x00, 0x74, 0x02, 0x00, 0x00, 0x00,
        ],
        [
            0x01, 0x71, 0x00, 0x02, 0x00, 0x00,
        ],
        [
            0x02, 0x26, 0x02, 0x82, 0x00, 0x5c, 0xf9, 0x00, 0x00, 0x00,
        ],
        [
            0x03, 0x00, 0x00, 0x00, 0x21, 0x00, 0x21, 0x00, 0x21,
        ],
    ];

    /// <summary>
    /// Events the recorded round opens with, one per box. It is stated rather
    /// than read off <see cref="OpeningEventBodies"/> so the group's record count
    /// stays a compile-time constant, and the table's own length is what a
    /// reader checks it against.
    /// </summary>
    public const int OpeningEventCount = 2;

    /// <summary>
    /// The recurring item-box state body: the box's index followed by five zero
    /// bytes, which is what the capture writes for both boxes on the eight-frame
    /// beat.
    /// </summary>
    /// <param name="index">Index of the box, 0 or 1 in the capture.</param>
    /// <returns>The record body.</returns>
    public static byte[] ItemBoxState(byte index) => [index, 0, 0, 0, 0, 0];

    /// <summary>
    /// The body of one record of one opening event.
    /// </summary>
    /// <param name="eventIndex">Event to read, from zero.</param>
    /// <param name="recordIndex">Sub-type within the event, 0 to 3.</param>
    /// <returns>The record's body, as the capture carries it.</returns>
    public static byte[] ItemBoxEvent(int eventIndex, int recordIndex) =>
        OpeningEventBodies[(eventIndex * ItemBoxEventRecords) + recordIndex];
}
