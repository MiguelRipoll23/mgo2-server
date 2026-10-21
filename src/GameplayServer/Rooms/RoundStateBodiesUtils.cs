namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The round-state transfer a reference host writes for a joining player: one
/// field of state <c>0x00</c>, the player's place in the room.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a state transfer, not a settings body.</b> The ten bytes are the
/// dispatch key the client's registry is keyed by (<c>0x0b</c>), the state index
/// (<c>0x00</c>, the round's 168-byte state), the state's presence map — 55
/// fields, so <c>ceil(55 / 8) = 7</c> bytes — and then one value for the one bit
/// the map sets. That is <c>1 + 1 + 7 + 1 = 10</c>, and it is the whole record:
/// `mgo2-game-started-on-briefing` t+93.147 and t+189.909, `mgo2-game2` t+120.811,
/// `mgo2-game-failed-memoryviolation`'s siblings and the red-team capture all
/// write <c>0b 00 00 04 00 00 00 00 00 NN</c>, where the <c>0x04</c> at
/// <c>+3</c> is bit 2 of the map's second byte — field **10** at state offset
/// <c>83</c> — and <c>NN</c> is its one-byte value. All 188 copies in the
/// fourteen captures fit that parse with exactly one field
/// (<c>docs/analysis/join-stall-after-mode2.md</c> §34).
/// </para>
/// <para>
/// <b>The value is the answered peer's slot byte.</b> It is the same byte the
/// loadout records carry — key plus one: <c>started-on-briefing</c> t+93.147
/// sends <c>0b 02</c> beside <c>02</c>, t+189.909 <c>0b 03</c> beside <c>03</c>,
/// and the red-team capture t+148.371 <c>0b 06</c> beside <c>06</c> — so it rises
/// with the room because a joiner takes the next slot, not because it counts the
/// players. **[I]**: the schema names neither field, and one burst
/// (`joined-final-round…` t+606.647) sends <c>03</c> beside a <c>0b 12</c>
/// loadout, which the slot reading explains only if that loadout belongs to
/// another player. A peer the roster cannot name is written no record rather than
/// a slot byte of zero, which would claim the host's own slot.
/// </para>
/// <para>
/// <b>The reference does not always write it.</b> 7 of the 9 reference gameplay
/// captures hold the record, and one fresh-room join
/// (`mgo2-game-failed-memoryviolation`) holds none of this family at all — only
/// the field-0 variant — so its absence is not a stall on its own. This host
/// wrote none of it before this record existed; it is written here because the
/// join burst is where the reference puts it and the value is one this host
/// already knows (the same slot byte the loadout and the closing blob carry).
/// </para>
/// </remarks>
public static class RoundStateBodiesUtils
{
    /// <summary>Body length of the join's round-state transfer, as measured.</summary>
    public const int PayloadLength = 10;

    /// <summary>Type byte the body opens with: the settings payload type.</summary>
    public const byte PayloadType = PlayerSettingsBurstService.SettingsPayloadType;

    /// <summary>Offset of the state index the transfer names: state <c>0x00</c>.</summary>
    public const int StateIndexOffset = 1;

    /// <summary>Offset of the state's presence map.</summary>
    public const int MapOffset = 2;

    /// <summary>Length of that map: one bit per field, <c>ceil(55 / 8)</c> bytes.</summary>
    public const int MapLength = 7;

    /// <summary>
    /// The state field the record carries: one byte at state offset <c>83</c>,
    /// whose map bit is the <c>0x04</c> of the map's second byte.
    /// </summary>
    public const int SlotFieldIndex = 10;

    /// <summary>Offset of the carried field's byte, behind the map.</summary>
    public const int ValueOffset = MapOffset + MapLength;

    /// <summary>
    /// The round-state transfer for a joining player: state <c>0x00</c>, field
    /// <c>10</c> at the player's slot byte.
    /// </summary>
    /// <param name="slot">
    /// The player's key plus one, the slot byte the burst's loadout records and
    /// its closing state blob carry.
    /// </param>
    /// <returns>The record body.</returns>
    public static byte[] JoinSlotBody(byte slot)
    {
        var body = new byte[PayloadLength];
        body[0] = PayloadType;
        body[StateIndexOffset] = 0;
        body[MapOffset + (SlotFieldIndex / 8)] = (byte)(1 << (SlotFieldIndex % 8));
        body[ValueOffset] = slot;
        return body;
    }
}
