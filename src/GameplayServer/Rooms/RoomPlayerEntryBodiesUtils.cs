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
/// instead is the tail this file cannot build — see
/// <see cref="RecordedBody"/> — which is why it is a record of the room's state
/// and not a copy of the roster's own entry.
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
/// team byte is the raw <c>0</c>/<c>1</c> the arrays carry and not the doubled
/// value the loadout carries, and it follows the room: the record for slot
/// <c>0b</c> is written <c>01</c> only after the arrays put that player on side
/// one.
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
/// <b>The last fourteen bytes are recorded, and that is the whole of what this
/// file claims about them.</b> They move per capture, per player and per
/// rejoin — <c>mgo2-game1</c> writes slot <c>06</c> as <c>ff 5f 0c fe …</c> while
/// it sat at roster index 4 and as <c>01 c0 78 ec …</c> after re-joining under
/// the same slot — so they are per-player values this host has no source for, and
/// they are carried as the recording has them rather than invented. The recorded
/// body is <c>mgo2-started-briefing-then-solo-game</c>'s, the answer to this
/// request in a room of one; see <c>docs/analysis/join-stall-after-mode2.md</c>
/// §4.
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

    /// <summary>Offset of the raw team byte, <c>0</c> or <c>1</c>.</summary>
    public const int TeamByteOffset = 0x09;

    /// <summary>
    /// The team byte a player the room's rows put on no side carries: the value
    /// the captured record for a lone player in a room carries, which is side zero
    /// under the arrays' own coding and is not a side chosen for it.
    /// </summary>
    public const byte NoTeamByte = 0x00;

    /// <summary>
    /// A recorded copy of the record, supplying the fourteen bytes behind the team
    /// byte. See the type's remarks for why they are a recording's and what
    /// measured them as per-player.
    /// </summary>
    private static readonly byte[] RecordedBody =
    [
        0x07, 0x12, 0x00, 0x04, 0x00, 0x03, 0x00, 0x03, 0x03, 0x00,
        0xff, 0x5f, 0x0c, 0xfe, 0xff, 0xff, 0xe4, 0x0c, 0x00, 0x00,
        0xdc, 0x82, 0x00, 0x00, 0x00,
    ];

    /// <summary>
    /// Builds the room's entry for one player: the recorded body with the index,
    /// the player's own key and its side written into it.
    /// </summary>
    /// <param name="index">The entry's index, written as given.</param>
    /// <param name="slot">
    /// The player's key in the room's 24-entry player table, 1 to 23 — the byte
    /// <see cref="RosterMember.SessionSlot"/> holds and the arrays mark occupied.
    /// It is written raw and not as the key plus one the loadout record carries.
    /// </param>
    /// <param name="team">The raw team byte, <c>0</c> or <c>1</c>, or <see cref="NoTeamByte"/>.</param>
    /// <returns>The record body.</returns>
    public static byte[] Build(byte index, byte slot, byte team)
    {
        var body = (byte[])RecordedBody.Clone();
        body[IndexByteOffset] = index;
        body[SlotByteOffset] = slot;
        body[RepeatSlotByteOffset] = slot;
        body[SecondRepeatSlotByteOffset] = slot;
        body[TeamByteOffset] = team;
        return body;
    }

    /// <summary>
    /// The team byte a record carries for a side the room's rows state.
    /// </summary>
    /// <param name="team">Team slot the room holds for the player, or <c>null</c>.</param>
    /// <returns>The byte to write.</returns>
    public static byte TeamByteOf(short? team) =>
        team is 0 or 1 ? (byte)team.Value : NoTeamByte;
}
