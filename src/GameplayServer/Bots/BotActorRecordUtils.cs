using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// One player slot's actor block on the gameplay channel: the state, vitals,
/// position, weapon and aim records that give a player a body in the round, and
/// the damage record a hit lands under.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is an identifier block, not one record.</b> The recorded host writes six
/// records per actor per tick, and their identifiers are one base plus a fixed
/// offset: the state record at the base, vitals at <c>+1</c>, position at
/// <c>+2</c>, the weapon pair and the weapon byte at <c>+4</c>, and aim at
/// <c>+6</c>. A hit on that actor is written three below the state record, which
/// is the base <c>−3</c> the working-server capture's damage records sit at.
/// </para>
/// <para>
/// <b>The base follows the round's identifier generation.</b> Slot one is
/// <c>0x084d</c> in the first generation and <c>0x004d</c> in its sibling;
/// every next slot is ten identifiers along.
/// </para>
/// <para>
/// <b>The bodies are the working-server capture's.</b> A standing actor writes
/// <c>00 ff 00</c> and the head <c>21 00 00 00</c>; one that is walking or aiming
/// writes the nine-byte state — action, target slot, a one when firing, and the
/// aim point — and the head <c>41 81 00 00</c>. The position's three coordinates
/// and the aim's are a tenth of a stage unit, the heading is the recorded word,
/// and the vitals are health and stamina. A hit is fifteen bytes: subtype, the
/// damage at <c>+2</c> as a sixteenth of a health point, the weapon, the impact
/// point, two unread bytes, and the bearing.
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

    /// <summary>Attribute class of a hit, which is the damage record.</summary>
    public const byte DamageAttributeClass = 0x00;

    /// <summary>Length of the walking position body the capture carries.</summary>
    public const int PositionBodyLength = 16;

    /// <summary>Length of the vitals body: health, then stamina.</summary>
    public const int VitalsBodyLength = 2;

    /// <summary>Length of the damage body the capture's hits carry.</summary>
    public const int DamageBodyLength = 15;

    /// <summary>Length of the state body an actor that is acting carries.</summary>
    public const int ActingStateBodyLength = 9;

    /// <summary>Length of the aim body: the point looked at, three words.</summary>
    public const int AimBodyLength = 6;

    /// <summary>
    /// Scaling of the damage word: one health point is <c>0x80</c>, measured over
    /// the capture's hits (<c>4327</c> is 33.80 points).
    /// </summary>
    public const int DamageScale = 0x80;

    /// <summary>Subtype the capture's ordinary hits carry.</summary>
    public const byte DamageSubtype = 0x11;

    /// <summary>
    /// The state record's identifier for a session slot.
    /// </summary>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="sessionSlot">Key the player holds in the room's player table.</param>
    /// <returns>The slot's state identifier; the other five records sit behind it.</returns>
    public static ushort StateIdentifierOf(MatchRecordGenerationUtils generation, byte sessionSlot) =>
        (ushort)(PlayerVitalsFramesUtils.IdentifierFor(generation, sessionSlot) - 1);

    /// <summary>
    /// The identifier a hit on a session slot travels under: three below the
    /// state record, which is where the capture's damage records sit.
    /// </summary>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="sessionSlot">Key the player holds in the room's player table.</param>
    /// <returns>The slot's damage identifier.</returns>
    public static ushort DamageIdentifierOf(MatchRecordGenerationUtils generation, byte sessionSlot) =>
        (ushort)(StateIdentifierOf(generation, sessionSlot) - 3);

    /// <summary>
    /// Reads the slot a damage identifier names, in either generation.
    /// </summary>
    /// <param name="identifier">Identifier a hit arrived under.</param>
    /// <returns>
    /// The slot the hit names, or <c>null</c> when the identifier is not a damage
    /// record of any slot.
    /// </returns>
    public static byte? SlotOfDamageIdentifier(ushort identifier)
    {
        for (var generation = 0; generation < 2; generation++)
        {
            var value = (ushort)(identifier ^ (generation * MatchRecordGenerationUtils.SiblingBit));
            var offset = (int)value - (StateIdentifierOf(MatchRecordGenerationUtils.First, 0) - 3);
            if (offset < SlotStride)
            {
                continue;
            }

            if (offset % SlotStride == 0 && offset / SlotStride is > 0 and < 24)
            {
                return (byte)(offset / SlotStride);
            }
        }

        return null;
    }

    /// <summary>
    /// Weapon a live bot's actor block carries: the working host writes <c>0x18</c>
    /// in the pair for slots 1, 2, 3, 5, 6 and 7 and <c>0x19</c> for slot 4.
    /// </summary>
    public const byte CarriedWeapon = 0x18;

    /// <summary>Magazine count the working host writes beside the weapon.</summary>
    public const byte CarriedAmmo = 0x0f;

    /// <summary>
    /// Builds one slot's whole actor block, in the order the capture writes it.
    /// </summary>
    /// <param name="sessionSlot">Key the player holds in the room's player table.</param>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="body">The bot's body: pose, aim, health and target.</param>
    /// <param name="firing">Whether the bot fires this tick.</param>
    /// <returns>The six records, or an empty list for a slot of zero.</returns>
    public static IReadOnlyList<UdpMessage> Build(
        byte sessionSlot,
        MatchRecordGenerationUtils generation,
        BotBodyState body,
        bool firing)
    {
        if (sessionSlot == 0)
        {
            return [];
        }

        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(body);

        var state = StateIdentifierOf(generation, sessionSlot);
        return
        [
            new UdpMessage(state, 4, StateAttributeClass, BuildStateBody(body, firing)),
            new UdpMessage(
                (ushort)(state + 1),
                3,
                VitalsAttributeClass,
                [(byte)Math.Clamp((int)body.Health, 0, (int)BotBodyState.MaximumVitals), (byte)body.Stamina]),
            new UdpMessage(
                (ushort)(state + 2),
                (byte)(PositionBodyLength + 1),
                PositionAttributeClass,
                BuildPositionBody(body)),
            new UdpMessage((ushort)(state + 4), 6, WeaponPairAttributeClass, BuildWeaponPair(body)),
            new UdpMessage((ushort)(state + 4), 2, WeaponAttributeClass, [0x00]),
            new UdpMessage((ushort)(state + 6), 7, AimAttributeClass, BuildAimBody(body)),
        ];
    }

    /// <summary>
    /// The weapon pair a live bot carries, <c>00 18 00 00 0f</c> in the capture,
    /// and the zero pair a corpse carries.
    /// </summary>
    /// <param name="body">The bot's body.</param>
    /// <returns>The pair's five bytes.</returns>
    private static byte[] BuildWeaponPair(BotBodyState body) =>
        body.IsDead
            ? [0x00, 0x00, 0x00, 0x00, 0x00]
            : [0x00, CarriedWeapon, 0x00, 0x00, CarriedAmmo];

    /// <summary>
    /// The damage record one bot's hit travels under, on the slot it hit.
    /// </summary>
    /// <param name="victimSlot">Player the hit landed on.</param>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="damage">Health points the hit takes.</param>
    /// <param name="weapon">Weapon id the hit carries.</param>
    /// <param name="impactX">Impact <c>x</c>, in stage units.</param>
    /// <param name="impactY">Impact height, in stage units.</param>
    /// <param name="impactZ">Impact <c>z</c>, in stage units.</param>
    /// <param name="bearing">Bearing the shot came from, as the recorded word.</param>
    /// <returns>The fifteen-byte damage record.</returns>
    public static UdpMessage BuildShot(
        byte victimSlot,
        MatchRecordGenerationUtils generation,
        int damage,
        byte weapon,
        float impactX,
        float impactY,
        float impactZ,
        ushort bearing)
    {
        ArgumentNullException.ThrowIfNull(generation);

        var body = new byte[DamageBodyLength];
        body[0] = DamageSubtype;
        WriteUInt16(body, 2, (ushort)Math.Clamp(damage * DamageScale, 0, ushort.MaxValue));
        body[4] = weapon;
        WriteInt16(body, 5, ToTenths(impactX));
        WriteInt16(body, 7, ToTenths(impactY));
        WriteInt16(body, 9, ToTenths(impactZ));
        WriteUInt16(body, 13, bearing);

        return new UdpMessage(
            DamageIdentifierOf(generation, victimSlot),
            (byte)(DamageBodyLength + 1),
            DamageAttributeClass,
            body);
    }

    /// <summary>
    /// The state body: the capture's three-byte standing form, or the nine-byte
    /// acting form when the actor is walking, aiming or firing.
    /// </summary>
    /// <remarks>
    /// The acting form is <c>42 ff 00 bd 08 78 00 d9 ff</c> in the capture: the
    /// first byte's low six bits are the action (<c>2</c> walking and aiming,
    /// <c>4</c> firing) and its <c>0x40</c> is set on every one of them, the second
    /// is the target slot or <c>0xff</c>, the third is a one exactly on the firing
    /// records, and the rest is the point looked at.
    /// </remarks>
    /// <param name="body">The bot's body.</param>
    /// <param name="firing">Whether it fires this tick.</param>
    /// <returns>The three- or nine-byte body.</returns>
    private static byte[] BuildStateBody(BotBodyState body, bool firing)
    {
        if (!body.Moving && body.TargetSlot == BotBodyState.NoTarget && !firing)
        {
            return [0x00, 0xff, 0x00];
        }

        var state = new byte[ActingStateBodyLength];
        state[0] = (byte)(0x40 | (firing ? 4 : 2));
        state[1] = body.TargetSlot;
        state[2] = (byte)(firing ? 1 : 0);
        WriteInt16(state, 3, ToTenths(body.AimX));
        WriteInt16(state, 5, ToTenths(body.AimY));
        WriteInt16(state, 7, ToTenths(body.AimZ));
        return state;
    }

    /// <summary>
    /// The position body: the walking or standing head, then <c>x</c>, height and
    /// <c>z</c> at a tenth of a unit, then the heading.
    /// </summary>
    /// <remarks>
    /// The heads are the capture's: <c>21 00 00 00</c> on a bot standing at its
    /// spawn, <c>41 81 00 00</c> on one walking. The three coordinates are the
    /// stage's at a tenth, which is how the recorded host writes a bot from
    /// <c>-23750</c> as <c>-2375</c>.
    /// </remarks>
    /// <param name="body">The bot's body.</param>
    /// <returns>The sixteen-byte body.</returns>
    private static byte[] BuildPositionBody(BotBodyState body)
    {
        var position = new byte[PositionBodyLength];
        position[0] = body.Moving ? (byte)0x41 : (byte)0x21;
        position[1] = body.Moving ? (byte)0x81 : (byte)0x00;
        WriteInt16(position, 6, ToTenths(body.X));
        WriteInt16(position, 8, ToTenths(body.Y));
        WriteInt16(position, 10, ToTenths(body.Z));
        WriteInt16(position, 12, (short)body.Yaw);
        return position;
    }

    /// <summary>
    /// The aim body: the point the bot looks at, three words at a tenth of a
    /// unit. The capture's are absolute points near the actor, at its own height.
    /// </summary>
    /// <param name="body">The bot's body.</param>
    /// <returns>The six-byte body.</returns>
    private static byte[] BuildAimBody(BotBodyState body)
    {
        var aim = new byte[AimBodyLength];
        WriteInt16(aim, 0, ToTenths(body.AimX));
        WriteInt16(aim, 2, ToTenths(body.AimY));
        WriteInt16(aim, 4, ToTenths(body.AimZ));
        return aim;
    }

    /// <summary>Rounds a stage coordinate to the wire's tenth of a unit.</summary>
    /// <param name="value">Coordinate in stage units.</param>
    /// <returns>The coordinate at a tenth of a unit, saturated to the wire's word.</returns>
    private static short ToTenths(float value) =>
        (short)Math.Clamp(MathF.Round(value / 10f), short.MinValue, short.MaxValue);

    /// <summary>Writes one signed sixteen-bit value little-endian.</summary>
    /// <param name="body">Body to write into.</param>
    /// <param name="offset">Offset of the low byte.</param>
    /// <param name="value">Value to write.</param>
    private static void WriteInt16(byte[] body, int offset, short value) =>
        WriteUInt16(body, offset, (ushort)value);

    /// <summary>Writes one unsigned sixteen-bit value little-endian.</summary>
    /// <param name="body">Body to write into.</param>
    /// <param name="offset">Offset of the low byte.</param>
    /// <param name="value">Value to write.</param>
    private static void WriteUInt16(byte[] body, int offset, ushort value)
    {
        body[offset] = (byte)(value & 0xff);
        body[offset + 1] = (byte)((value >> 8) & 0xff);
    }
}
