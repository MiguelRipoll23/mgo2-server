using Mgo2Server.Shared.Types;

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
    /// Measured identifier of the object stream — the item boxes and dropped
    /// things (<see cref="WorldObjectStream.Object"/>) — one of the world-object
    /// group the recorded host carries in every eighth beat frame from 0.2 s
    /// after the match-start run on.
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

    /// <summary>
    /// Measured identifier of the group's second stream, the devices. In the recorded
    /// round it carries the same six-byte filler as the first, because that round placed
    /// no device; a bomb's or a trap's own event would travel under it
    /// (<see cref="WorldObjectStream.Device"/>).
    /// </summary>
    public const ushort ItemBoxStateIdentifier = 0x1a5a;

    /// <summary>Length of an item box's recurring state body.</summary>
    public const int ItemBoxStateLength = 6;

    /// <summary>
    /// Records one item-box event writes under the group's own identifier, in
    /// record-type order: <c>00</c> the box's head, <c>01</c> its item, <c>02</c>
    /// its position and <c>03</c> the event's tail
    /// (<see cref="WorldObjectRecordType"/>).
    /// </summary>
    public const int ItemBoxEventRecords = 4;

    /// <summary>The tail's three bytes behind the event's index, repeated: <c>21 00</c> and a final <c>21</c>.</summary>
    private const byte TailValue = 0x21;

    /// <summary>
    /// The complete bodies of the two events the recorded round opens with, box
    /// by box, in sub-type order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the item-box group's real content, and the six-byte pair of
    /// <see cref="ItemBoxState"/> is the replay console's "filler" that rides
    /// beside it: a box is created, moved or emptied by one event of up to four
    /// records, and the all-zero pair that follows every eight frames is what says
    /// the box is still there. The recorded host writes the two events of its own
    /// round's boxes in the first two groups — 0.2 s and 0.46 s after the
    /// match-start run — and then the filler pair alone, so a host that replays the
    /// filler and not the event never tells the client its boxes exist.
    /// </para>
    /// <para>
    /// Each record is built from the field it states rather than written as a run
    /// of bytes: the head bytes, the item ids and the two positions are the
    /// capture's own, and the position reads as <c>x</c>, <c>y</c> and <c>z</c>
    /// signed sixteens in the same tenths the position records use. The events the
    /// capture writes later in the round (a box taken and dropped again, which
    /// moves it) are not here — they are the live round's own play, and they
    /// arrive irregularly.
    /// </para>
    /// </remarks>
    private static readonly byte[][] OpeningEventBodies =
    [
        // Box 1: the ENVG, a small box, at (-1092, 1800, 1650).
        HeadRecord(WorldObjectEvent.Placed, boxNumber: 1),
        ItemRecord(WorldObjectItem.Envg, boxNumber: 1),
        PositionRecord(x: -1092, y: 1800, z: 1650),
        TailRecord(),

        // Box 2: the drum can, a large one, at (550, 130, -1700).
        HeadRecord(WorldObjectEvent.Placed, boxNumber: 2),
        ItemRecord(WorldObjectItem.DrumCan, boxNumber: 2),
        PositionRecord(x: 550, y: 130, z: -1700),
        TailRecord(),
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
    /// <param name="recordIndex">Record type within the event, 0 to 3.</param>
    /// <returns>The record's body, as the capture carries it.</returns>
    public static byte[] ItemBoxEvent(int eventIndex, int recordIndex) =>
        OpeningEventBodies[(eventIndex * ItemBoxEventRecords) + recordIndex];

    /// <summary>
    /// The event's head record: the record type, the event code, the box's number, and the
    /// object id, slot and fourth byte the measured events leave at nought.
    /// </summary>
    /// <param name="placed">What happened, <see cref="WorldObjectEvent.Placed"/> in both measured events.</param>
    /// <param name="boxNumber">Which box of the round's two, 1 then 2.</param>
    /// <returns>The record body.</returns>
    private static byte[] HeadRecord(WorldObjectEvent placed, byte boxNumber) =>
    [
        (byte)WorldObjectRecordType.Head,
        (byte)placed,
        boxNumber,
        0x00, 0x00, 0x00,
    ];

    /// <summary>
    /// The event's item record: the record type, the item's id and the box it belongs to,
    /// then the three bytes the measured events leave at nought.
    /// </summary>
    /// <param name="item">Item the box holds.</param>
    /// <param name="boxNumber">Which box of the round's two, 1 then 2.</param>
    /// <returns>The record body.</returns>
    private static byte[] ItemRecord(WorldObjectItem item, byte boxNumber) =>
    [
        (byte)WorldObjectRecordType.Item,
        (byte)item,
        0x00,
        boxNumber,
        0x00, 0x00,
    ];

    /// <summary>
    /// The event's position record: its record type, the box's point as three
    /// signed sixteens in wire tenths, and three zero bytes.
    /// </summary>
    /// <param name="x">Coordinate along the axis the spawns separate on.</param>
    /// <param name="y">Height above the stage's floor.</param>
    /// <param name="z">The remaining horizontal coordinate.</param>
    /// <returns>The record body.</returns>
    private static byte[] PositionRecord(short x, short y, short z) =>
    [
        (byte)WorldObjectRecordType.Position,
        (byte)x, (byte)(x >> 8),
        (byte)y, (byte)(y >> 8),
        (byte)z, (byte)(z >> 8),
        0x00, 0x00, 0x00,
    ];

    /// <summary>The event's tail record: its record type, three zeros and the repeated <c>21</c> values.</summary>
    /// <returns>The record body.</returns>
    private static byte[] TailRecord() =>
    [
        (byte)WorldObjectRecordType.Tail,
        0x00, 0x00, 0x00,
        TailValue, 0x00, TailValue, 0x00, TailValue,
    ];
}
