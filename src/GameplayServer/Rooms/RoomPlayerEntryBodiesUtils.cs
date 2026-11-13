using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The room's own player entry: the 25-byte <c>07 12 00 04 …</c> record a peer is
/// answered with when it asks to be placed in the room.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is not a roster entry.</b> It travels on the roster identifiers, which is
/// what makes it look like one, but it is a fifth the length, it names a player by
/// the key of the room's 24-entry player table rather than by a roster index, and
/// it carries no name, no character identifier and no endpoint. What it carries
/// instead is the map's spawn tail (<see cref="MapSpawnUtils"/>), which is why it
/// is a record of the room's state and not a copy of the roster's own entry.
/// </para>
/// <para>
/// <b>Four fields are read, and the rest is a recorded body's.</b> Measured over
/// the ten copies the captures hold, the first four bytes are always
/// <c>07 12 00 04</c>, and the six bytes behind them are
/// <c>&lt;index&gt; &lt;slot&gt; 00 &lt;slot&gt; &lt;slot&gt; &lt;team&gt;</c>:
/// </para>
/// <code>
/// mgo2-game1    slot 0f, team 0   07 12 00 04 00 0f 00 0f 0f 00 ff 3f 84 6d ff ff 91 02 00 00 78 ec ff ff 00
/// mgo2-game1    slot 0b, team 1   07 12 00 04 03 0b 00 0b 0b 01 01 c0 d4 ad 00 00 71 02 00 00 0c fe ff ff 00
/// started-on-solo       slot 03   07 12 00 04 01 03 00 03 03 00 fe 7f b9 fd ff ff 22 03 00 00 97 e7 00 00 00
/// </code>
/// <para>
/// The slot is the same key the lobby-slot arrays mark occupied — in
/// <c>mgo2-game1</c> the record's <c>0b</c> is a slot the same frame's occupancy
/// array writes <c>07</c> at — so it is the raw key and not the slot plus one the
/// loadout record carries (<see cref="LoadoutFramesUtils.SlotByteOffset"/>). The
/// team byte is the raw side id the arrays carry — <c>0</c>, <c>1</c> or the
/// orange side's <c>2</c> — and not the doubled value the loadout carries, and it
/// follows the room: the record for slot <c>0b</c> is written <c>01</c> only after
/// the arrays put that player on side one.
/// </para>
/// <para>
/// <b>The index is unresolved.</b> It runs 0, 1, 2, 3 down the copies a room
/// writes at one moment and a single-entry answer carries <c>00</c> in the
/// briefing capture and <c>01</c> in two others, so neither the entry's place in
/// a run nor the room's population explains it. It is written as the caller
/// states it and no meaning is claimed for it; see
/// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> and the analysis file named below.
/// </para>
/// <para>
/// <b>The last fifteen bytes are the map's: a heading and one point.</b>
/// <c>+10</c> is a signed sixteen-bit turn, <c>+12</c> is the spawn's <c>x</c>,
/// <c>+16</c> and <c>+20</c> are the point the side faces' <c>y</c> and <c>z</c>, and
/// <c>+24</c> is a zero. Reading them as a position and a facing of three shorts each
/// is what §76 of <c>docs/analysis/join-stall-after-mode2.md</c> corrects: the
/// "third short" of the spawn is the high half of <c>x</c> and the "tail" behind the
/// facing is the high half of <c>z</c>, so both are the sign of those coordinates and
/// not a side's flag. The solo Blood Bath body decodes as heading <c>5fff</c>,
/// <c>x</c> <c>-23750</c>, <c>y</c> <c>800</c>, <c>z</c> <c>20750</c>, and the other
/// side of the full room as heading <c>0000</c>, <c>x</c> <c>20750</c>, <c>y</c>
/// <c>800</c>, <c>z</c> <c>-23750</c>. They are not map-independent — a body recorded on one stage puts
/// the client's briefing camera outside the map when the room plays another —
/// so the pair is resolved from the map the room plays, not from one recording.
/// </para>
/// <para>    /// <b>Each stage's pair is its <c>PRP_HOMEGATE_A01</c>/<c>B01</c> placement.</b>
    /// <see cref="MapSpawnUtils"/> holds the map id to spawn table and the gate each
    /// row was read from; a map with no gate falls back to Blood Bath.
    /// <c>docs/analysis/join-stall-after-mode2.md</c> §4 carries the original
    /// measurement of the tail and the map 5 body that put a Blood Bath room's camera
    /// out of bounds.
/// </para>
/// </remarks>
public static class RoomPlayerEntryBodiesUtils
{
    /// <summary>Length of the record, in bytes.</summary>
    public const int BodyLength = 25;

    /// <summary>First byte of the record, which every captured copy opens with.</summary>
    public const byte Opcode = 0x07;

    /// <summary>Second byte of the record, which every captured copy carries.</summary>
    public const byte EntryIdentifier = 0x12;

    /// <summary>Third byte of the record, which every captured copy carries as a zero.</summary>
    public const byte ReservedByte = 0x00;

    /// <summary>
    /// Fourth byte of the record. It is <c>04</c> in all ten captured copies,
    /// across three captures and six players, so it is written as the constant it
    /// is and no meaning is claimed for it.
    /// </summary>
    public const byte FourthByte = 0x04;

    /// <summary>Offset of the entry's index, whose meaning is unresolved.</summary>
    public const int IndexByteOffset = 0x04;

    /// <summary>Offset of the player's key in the room's 24-entry player table.</summary>
    public const int SlotByteOffset = 0x05;

    /// <summary>Offset of the zero between the slot bytes.</summary>
    public const int SeparatorByteOffset = 0x06;

    /// <summary>Offset of the repetition of the player's key.</summary>
    public const int RepeatSlotByteOffset = 0x07;

    /// <summary>Offset of the second repetition of the player's key.</summary>
    public const int SecondRepeatSlotByteOffset = 0x08;

    /// <summary>
    /// Offset of the side id: <c>0</c> blue, <c>1</c> red, <c>2</c> orange
    /// (<see cref="RoomSideUtils"/>).
    /// </summary>
    public const int TeamByteOffset = 0x09;

    /// <summary>
    /// The team byte a player the room's rows put on no side carries: the value
    /// the captured record for a lone player in a room carries, which is side zero
    /// under the arrays' own coding and is not a side chosen for it.
    /// </summary>
    public const byte NoTeamByte = 0x00;

    /// <summary>Offset of the spawn's heading: a signed sixteen-bit turn.</summary>
    public const int HeadingOffset = 0x0a;

    /// <summary>Offset of the spawn's <c>x</c>, a signed thirty-two.</summary>
    public const int XOffset = 0x0c;

    /// <summary>Offset of the point the side faces' <c>y</c>, a signed thirty-two.</summary>
    public const int YOffset = 0x10;

    /// <summary>Offset of the point the side faces' <c>z</c>, a signed thirty-two.</summary>
    public const int ZOffset = 0x14;

    /// <summary>Offset of the record's last byte, zero in every measured body.</summary>
    public const int ClosingByteOffset = 0x18;


    /// <summary>
    /// Builds the room's entry for one player: the map's spawn and facing pair
    /// for the player's side, with the index, the player's own key and its side
    /// written into it.
    /// </summary>
    /// <param name="index">The entry's index, written as given.</param>
    /// <param name="slot">
    /// The player's key in the room's 24-entry player table, 1 to 23 — the byte
    /// <see cref="RosterMember.SessionSlot"/> holds and the arrays mark occupied.
    /// It is written raw and not as the key plus one the loadout record carries.
    /// </param>
    /// <param name="team">The raw team byte, <c>0</c> or <c>1</c>, or <see cref="NoTeamByte"/>.</param>
    /// <param name="map">Map id the room plays; the side's spawn comes from it.</param>
    /// <returns>The record body.</returns>
    public static byte[] Build(byte index, byte slot, byte team, int map)
    {
        var spawns = MapSpawnUtils.For(map);
        var spawn = spawns.SpawnOf(team);
        var facing = spawns.FacingOf(team);

        var body = new byte[BodyLength];
        body[0] = Opcode;
        body[1] = EntryIdentifier;
        body[2] = ReservedByte;
        body[3] = FourthByte;
        body[IndexByteOffset] = index;
        body[SlotByteOffset] = slot;
        body[SeparatorByteOffset] = 0;
        body[RepeatSlotByteOffset] = slot;
        body[SecondRepeatSlotByteOffset] = slot;
        body[TeamByteOffset] = team;

        WriteCoordinate(body, HeadingOffset, spawn.First);
        WriteInt32(body, XOffset, spawn.Along);
        WriteInt32(body, YOffset, facing.First);
        WriteInt32(body, ZOffset, facing.Along);
        body[ClosingByteOffset] = 0;
        return body;
    }

    /// <summary>Writes one signed 16-bit coordinate little-endian.</summary>
    /// <param name="body">Body to write into.</param>
    /// <param name="offset">Offset of the low byte.</param>
    /// <param name="value">Coordinate to write.</param>
    private static void WriteCoordinate(byte[] body, int offset, short value)
    {
        body[offset] = (byte)(value & 0xff);
        body[offset + 1] = (byte)((value >> 8) & 0xff);
    }

    /// <summary>Writes one signed 32-bit coordinate little-endian.</summary>
    /// <param name="body">Body to write into.</param>
    /// <param name="offset">Offset of the lowest byte.</param>
    /// <param name="value">Coordinate to write.</param>
    private static void WriteInt32(byte[] body, int offset, int value)
    {
        body[offset] = (byte)(value & 0xff);
        body[offset + 1] = (byte)((value >> 8) & 0xff);
        body[offset + 2] = (byte)((value >> 16) & 0xff);
        body[offset + 3] = (byte)((value >> 24) & 0xff);
    }

    /// <summary>
    /// The team byte a record carries for a side the room's rows state.
    /// </summary>
    /// <param name="team">Team slot the room holds for the player, or <c>null</c>.</param>
    /// <returns>The byte to write.</returns>
    public static byte TeamByteOf(short? team) =>
        RoomSideUtils.IsSide(team) ? (byte)team!.Value : NoTeamByte;
}
