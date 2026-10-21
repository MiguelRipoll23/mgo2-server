namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The round-state transfer a reference host writes for a joining player: one
/// field of state <c>0x00</c>, the number of players in the room.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a state transfer, not a settings body.</b> The ten bytes are the
/// dispatch key the client's registry is keyed by (<c>0x0b</c>), the state index
/// (<c>0x00</c>, the round's own state), the state's presence map — 55 fields, so
/// <c>ceil(55 / 8) = 7</c> bytes — and then one value for the one bit the map
/// sets. That is <c>1 + 1 + 7 + 1 = 10</c>, and it is the whole record:
/// `mgo2-game-started-on-briefing` t+93.147 and t+189.909, `mgo2-game2`
/// t+120.811 and the red-team capture all write
/// <c>0b 00 00 04 00 00 00 00 00 NN</c>, where the <c>0x04</c> at <c>+3</c> is
/// bit 2 of the map's second byte — field **10** at state offset <c>83</c> — and
/// <c>NN</c> is its one-byte value. All 188 copies in the fourteen captures fit
/// that parse with exactly one field
/// (<c>docs/analysis/join-stall-after-mode2.md</c> §34).
/// </para>
/// <para>
/// <b>The value is the room's population, the host counted.</b> It is not the
/// answered peer's slot byte, which is what this record was first read as: the
/// same captures hand out slots far above the values they write here — `game1`
/// places a joiner at key <c>0x14</c>, and its join-time value is <c>03</c> —
/// while the corpus' values are <c>02</c> to <c>09</c> and not one is higher.
/// What the values track is the number of players: `started-on-briefing` writes
/// <c>02</c>, <c>03</c>, <c>04</c>, <c>05</c> and <c>06</c> as one player joins
/// after another, and `game1` alternates <c>03</c> and <c>04</c> as its room
/// loses and regains a player. The red-team capture settles it: its t+106.610
/// join publishes the entry for key <c>8</c> and the loadout <c>0b 09</c>, and
/// the transfer in the same datagram reads <c>07</c> — a population of seven
/// players in a room whose keys have already run to eight. **[I]** for the name:
/// the schema does not name the field, and no call site of <c>0x27E480</c> in the
/// image passes the field's offset, so the consumer that would name it is not
/// read; the count is the reading every captured value fits.
/// </para>
/// <para>
/// <b>The reference does not always write it.</b> 7 of the 9 reference gameplay
/// captures hold the record, and one join (`mgo2-game-failed-memoryviolation`)
/// writes only the field-0 variant — so its absence is not a stall on its own.
/// This host wrote none of it before this record existed, and writes it on every
/// join because its value is the room's and not the peer's: a burst that could
/// not name the answering peer can still count the room.
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
    public const int PlayerCountFieldIndex = 10;

    /// <summary>Offset of the carried field's byte, behind the map.</summary>
    public const int ValueOffset = MapOffset + MapLength;

    /// <summary>
    /// The round-state transfer a join writes: state <c>0x00</c>, field <c>10</c>
    /// at the room's population.
    /// </summary>
    /// <param name="playerCount">
    /// How many players the room holds, the host counted — the same figure the
    /// client's own player list has once this burst is in.
    /// </param>
    /// <returns>The record body.</returns>
    public static byte[] JoinPlayerCountBody(byte playerCount)
    {
        var body = new byte[PayloadLength];
        body[0] = PayloadType;
        body[StateIndexOffset] = 0;
        body[MapOffset + (PlayerCountFieldIndex / 8)] = (byte)(1 << (PlayerCountFieldIndex % 8));
        body[ValueOffset] = playerCount;
        return body;
    }
}
