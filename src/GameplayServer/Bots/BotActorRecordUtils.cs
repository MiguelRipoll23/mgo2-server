using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// One player slot's actor block on the gameplay channel: the state, vitals,
/// position, weapon and aim records that give a player a body in the round.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is an identifier block, not one record.</b> The recorded host writes six
/// records per actor per tick, and their identifiers are one base plus a fixed
/// offset: the state record at the base, vitals at <c>+1</c>, position at
/// <c>+2</c>, the weapon pair and the weapon byte at <c>+4</c>, and aim at
/// <c>+6</c> (<c>docs/protocol/UDP_COMMANDS.md</c>, the class table in
/// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §5). The offsets are the class
/// values, which is why the fourth byte of each record is its kind.
/// </para>
/// <para>
/// <b>The base follows the round's identifier generation.</b> Slot one is
/// <c>0x084d</c> in the first generation and <c>0x004d</c> in its sibling;
/// every next slot is ten identifiers along. Deriving the state identifier
/// from the generation-aware vitals identifier keeps all six records in the
/// same generation the round's opening run uses.
/// </para>
/// <para>
/// <b>The bodies are the capture's, and the position is the map's own spawn.</b>
/// A bot has no simulated position and no client to send one, so it stands where
/// its side spawns: <see cref="MapSpawnUtils"/> holds the pair the round's own
/// entry writes, in world units, and the position record carries those thirds at
/// a tenth of a unit. The remaining fields are constants the capture repeats —
/// <c>00 ff 00</c> for a living actor that is not acting on anyone, <c>fa fa</c>
/// for untouched vitals, a zero weapon pair and byte, and the spawn's heading as
/// the aim yaw. Nothing is claimed about a field the capture does not show.
/// </para>
/// </remarks>
public static class BotActorRecordUtils
{
    /// <summary>Distance between two slots' blocks, measured across sessions.</summary>
    public const int SlotStride = 0x0a;

    /// <summary>Attribute class of the actor's state record.</summary>
    public const byte StateAttributeClass = 0x01;

    /// <summary>Attribute class of the actor's position record.</summary>
    public const byte PositionAttributeClass = 0x02;

    /// <summary>Attribute class of the actor's vitals record.</summary>
    public const byte VitalsAttributeClass = 0x03;

    /// <summary>Attribute class of the actor's weapon pair record.</summary>
    public const byte WeaponPairAttributeClass = 0x04;

    /// <summary>Attribute class of the actor's aim record.</summary>
    public const byte AimAttributeClass = 0x05;

    /// <summary>Attribute class of the actor's single weapon byte.</summary>
    public const byte WeaponAttributeClass = 0x06;

    /// <summary>Length of the walking position body the capture carries.</summary>
    public const int PositionBodyLength = 16;

    /// <summary>Length of the vitals body: health, then stamina.</summary>
    public const int VitalsBodyLength = 2;

    /// <summary>
    /// The state record's identifier for a session slot.
    /// </summary>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="sessionSlot">Key the player holds in the room's player table.</param>
    /// <returns>The slot's state identifier; the other five records sit behind it.</returns>
    public static ushort StateIdentifierOf(MatchRecordGenerationUtils generation, byte sessionSlot) =>
        (ushort)(PlayerVitalsFramesUtils.IdentifierFor(generation, sessionSlot) - 1);

    /// <summary>
    /// Builds one slot's whole actor block, in the order the capture writes it.
    /// </summary>
    /// <param name="sessionSlot">Key the player holds in the room's player table.</param>
    /// <param name="team">Side the player is on, or <c>null</c> for none.</param>
    /// <param name="map">Map the round plays, which holds the spawn.</param>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <returns>The six records, or an empty list for a slot of zero.</returns>
    public static IReadOnlyList<UdpMessage> Build(
        byte sessionSlot,
        short? team,
        int map,
        MatchRecordGenerationUtils generation)
    {
        if (sessionSlot == 0)
        {
            return [];
        }

        ArgumentNullException.ThrowIfNull(generation);

        var state = StateIdentifierOf(generation, sessionSlot);
        var side = team == 1 ? (byte)1 : (byte)0;
        return
        [
            new UdpMessage(state, 4, StateAttributeClass, [0x00, 0xff, 0x00]),
            new UdpMessage((ushort)(state + 1), 3, VitalsAttributeClass, [0xfa, 0xfa]),
            new UdpMessage(
                (ushort)(state + 2),
                (byte)(PositionBodyLength + 1),
                PositionAttributeClass,
                BuildPositionBody(side, map)),
            new UdpMessage((ushort)(state + 4), 6, WeaponPairAttributeClass, [0x00, 0x00, 0x00, 0x00, 0x00]),
            new UdpMessage((ushort)(state + 4), 2, WeaponAttributeClass, [0x00]),
            new UdpMessage(
                (ushort)(state + 6),
                7,
                AimAttributeClass,
                BuildAimBody(side, map)),
        ];
    }

    /// <summary>
    /// The position body for one side's spawn: the standing head, three zero
    /// words, then <c>x</c>, height and <c>z</c> at a tenth of a unit and the
    /// spawn's heading.
    /// </summary>
    /// <remarks>
    /// <b>The shape is read off a live client's own records.</b> A client relays
    /// its position under this record, and one local run carries 903 of them. The
    /// body it writes at Blood Bath's red spawn is <c>20 00 00 00 00 00 b9 f6 50
    /// 00 1b 08 ff 5f 00 00</c>: <c>-2375</c> at <c>+6</c>, <c>80</c> at <c>+8</c>,
    /// <c>2075</c> at <c>+10</c> and <c>24575</c> at <c>+12</c> — that stage's
    /// <c>x = -23750</c>, its stand height <c>800</c> and its <c>z = 20750</c> at a
    /// tenth of a unit, then the spawn's own heading (<see cref="MapSpawnUtils"/>).
    /// Writing a coordinate unscaled, or in another pair of slots, puts a bot
    /// hundreds of metres from the player it is meant to stand beside.
    /// </remarks>
    /// <param name="team">Side whose spawn is written, <c>0</c> or <c>1</c>.</param>
    /// <param name="map">Map the round plays.</param>
    /// <returns>The sixteen-byte body.</returns>
    private static byte[] BuildPositionBody(byte team, int map)
    {
        var spawns = MapSpawnUtils.For(map);
        var spawn = spawns.SpawnOf(team);
        var facing = spawns.FacingOf(team);

        var body = new byte[PositionBodyLength];

        // The head of the standing form: the flags byte, a zero and a zero. It is
        // the recorded host's own shape for a bot that is not acting — `21 00 00
        // 00` on 22,666 of its position records across the bots' slots. The words
        // at +4 and +6 are zero on that form and carry no measured meaning.
        body[0] = 0x21;
        body[1] = 0x00;
        body[2] = 0x00;
        body[3] = 0x00;
        body[4] = 0x00;
        body[5] = 0x00;

        // x, the stand height, then z — the last from the spawn's look-at point,
        // which is where the room entry carries that side's z as well — at a tenth
        // of a unit, and the heading itself, which is not scaled.
        WriteInt16(body, 6, (short)(spawn.Along / 10));
        WriteInt16(body, 8, (short)(facing.First / 10));
        WriteInt16(body, 10, (short)(facing.Along / 10));
        WriteInt16(body, 12, spawn.First);
        return body;
    }

    /// <summary>
    /// The aim body: the spawn's heading as the first of its three signed
    /// sixteen-bit values, and the other two at rest.
    /// </summary>
    /// <param name="team">Side whose spawn heading is written.</param>
    /// <param name="map">Map the round plays.</param>
    /// <returns>The six-byte body.</returns>
    private static byte[] BuildAimBody(byte team, int map)
    {
        var body = new byte[6];
        WriteInt16(body, 0, MapSpawnUtils.For(map).SpawnOf(team).First);
        return body;
    }

    /// <summary>Writes one signed sixteen-bit value little-endian.</summary>
    /// <param name="body">Body to write into.</param>
    /// <param name="offset">Offset of the low byte.</param>
    /// <param name="value">Value to write.</param>
    private static void WriteInt16(byte[] body, int offset, short value)
    {
        body[offset] = (byte)(value & 0xff);
        body[offset + 1] = (byte)((value >> 8) & 0xff);
    }
}
