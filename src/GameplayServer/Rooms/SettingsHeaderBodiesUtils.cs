namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The twenty-four-byte settings header the recorded host writes for a player in
/// the answer to a request to be placed in the room.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it is.</b> The settings payload type (<c>0x0b</c>, the byte the burst's
/// rotation and loadout records open with), the player's slot, and then a
/// <b>state transfer</b> of that player's own slot state: a twenty-one-byte
/// presence map and the single byte of the one field whose bit is set.
/// `mgo2-game-started-on-briefing` t+93.127, `mgo2-game2` t+78.987 and
/// `mgo2-started-on-solo-game` t+101.405 write <c>0b SS 02 00 …</c> as the last
/// record of the answer.
/// </para>
/// <para>
/// <b><c>SS</c> is the player's key plus one</b>, the slot byte the burst's loadout
/// records carry (<see cref="LoadoutBodiesUtils"/>) and not the raw table key the
/// entry writes: `started-on-solo-game`'s request is answered for the player in
/// table key <c>03</c>, whose entry reads <c>07 12 00 04 … 03 …</c> and whose
/// header opens <c>0b 04</c>.
/// </para>
/// <para>
/// <b>The <c>0x02</c> behind the slot is a presence bit, not a phase.</b> The
/// twenty-two bytes behind the slot are a state transfer of the same kind the
/// <c>0x0002</c> blob carries (`MatchStateBodiesUtils.StateBlobBody`, and
/// `docs/analysis/join-stall-after-mode2.md` §28.7 for the parse): twenty-one bytes
/// of map, then the byte of the field the map names, and the map is read low bit
/// first. <c>0x02</c> in the map's first byte is therefore field **1**, which is one
/// byte at slot-state offset 1 — and the byte the tail of the body carries is that
/// field's value. Every copy measured through a briefing sets that field and nothing
/// else.
/// </para>
/// <para>
/// <b>The value is the player's own, and written zero here.</b> Field 1 reads
/// <c>0x00</c> and <c>0x01</c> for the same slot in the same briefing
/// (`started-on-briefing`'s slot <c>04</c> at t+156.885 and slot <c>05</c> at
/// t+198.371) and <c>0xe0</c> once (`mgo2-game1` t+449.043), which is the byte the
/// team array writes for a player whose side is not known — but the two do not agree
/// slot by slot in the same capture (slot <c>05</c> reads <c>01</c> here and <c>0</c>
/// in the array), so the side is not the field and this host has no source for it.
/// It is written zero. **[V]** the type, the length, the slot byte and the carried
/// field; **[U]** the value's meaning.
/// </para>
/// <para>
/// <b>There is a second family, and it carries a different field.</b> The copies whose
/// tail is <c>… 01 00 02</c> set the map's bit in byte nineteen, which is field
/// **152** — one byte at slot-state offset 289 — with the value <c>0x02</c>
/// (`mgo2-game-joined-when-red-team-then-briefing` t+307.917). So the two families
/// are one record answered with two different fields, not a phase byte; this builder
/// writes the first family's shape.
/// </para>
/// <para>
/// <b>It is written in a briefing's answer and not in a round's.</b> A round's
/// answer carries the player's entry and the roster close, and the reference writes
/// the header beside them in one of its three (`started-on-solo-game` t+101.405)
/// and not the other two; a briefing's answer carries it in both captures that hold
/// one. See the analysis file's "what is not written" section for the rest.
/// </para>
/// </remarks>
public static class SettingsHeaderBodiesUtils
{
    /// <summary>Body length of the settings header, as measured.</summary>
    public const int PayloadLength = 24;

    /// <summary>Type byte the body opens with: the settings payload type.</summary>
    public const byte SettingsPayloadType = PlayerSettingsBurstService.SettingsPayloadType;

    /// <summary>Offset of the player's slot byte.</summary>
    public const int SlotIndex = 1;

    /// <summary>Offset of the carried slot state's presence map.</summary>
    public const int MapOffset = 2;

    /// <summary>Length of that map: one bit per field, <c>ceil(164 / 8)</c> bytes.</summary>
    public const int MapLength = 21;

    /// <summary>
    /// The slot-state field every briefing copy carries: one byte at state offset 1,
    /// whose map bit is the <c>0x02</c> at <see cref="MapOffset"/>.
    /// </summary>
    public const int CarriedFieldIndex = 1;

    /// <summary>Offset of the carried field's byte, behind the map.</summary>
    public const int ValueOffset = MapOffset + MapLength;

    /// <summary>
    /// A player's settings header: the settings payload type, the player's slot, and
    /// a state transfer carrying field 1 at zero.
    /// </summary>
    /// <param name="slot">
    /// The player's key plus one, the slot byte the burst's loadout records carry
    /// (the room's table is zero-based, <see cref="RoomRosterService.SessionSlotCount"/>).
    /// </param>
    /// <returns>The record body.</returns>
    public static byte[] Build(byte slot)
    {
        var body = new byte[PayloadLength];
        body[0] = SettingsPayloadType;
        body[SlotIndex] = slot;
        body[MapOffset + (CarriedFieldIndex / 8)] = (byte)(1 << (CarriedFieldIndex % 8));
        return body;
    }
}
