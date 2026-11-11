using System.Buffers.Binary;
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
/// <b>It is an identifier block, not one record.</b> Six records per actor per
/// tick share one base: state at it, vitals at <c>+1</c>, position at <c>+2</c>,
/// the weapon pair and byte at <c>+4</c>, aim at <c>+6</c>, and a hit three below
/// it at <c>−3</c>.
/// </para>
/// <para>
/// <b>The base follows the round's identifier generation.</b> Slot one is
/// <c>0x084d</c> in the first generation and <c>0x004d</c> in its sibling; each
/// next slot is ten identifiers along.
/// </para>
/// <para>
/// <b>The bodies are the working-server capture's.</b> A standing actor writes
/// <c>00 ff 00</c> and the head <c>21 00 00 00</c>; one that is walking or aiming
/// writes the nine-byte state and the head <c>41 81 00 00</c>. Coordinates are a
/// tenth of a stage unit and a hit is fifteen bytes: subtype, damage at <c>+2</c>
/// as a sixteenth of a health point, weapon, impact point, two unread bytes and
/// the bearing.
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

    /// <summary>
    /// Length of the corpse's position body, which replaces the living one from
    /// the beat a bot's health reaches zero.
    /// </summary>
    /// <remarks>
    /// <b>A dead actor's position record is thirty-two bytes and opens on
    /// <c>0x22</c></b> — see <see cref="BotPositionBodiesUtils"/>; a living one is sixteen.
    /// </remarks>
    public const int CorpsePositionBodyLength = 32;

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
    /// Subtype of the hit that takes a victim's last health, which carries that
    /// health as its damage word instead of the hit's own damage.
    /// </summary>
    /// <remarks>
    /// <b>Measured on <c>game-with-bots-workingserver.pcapng</c>.</b> Nine hits
    /// there open <c>0x12</c> where 341 open <c>0x11</c>, and each sits on a death
    /// the feed names for the same victim within a fifth of a second — t+40.83 on
    /// slot 6, t+45.59 on slot 7, t+89.01 on slot 6, t+310.81 on slot 8 — with a
    /// damage word of <c>7d 08</c>, <c>07 7d</c> or <c>8d 07</c>: the victim's whole
    /// remaining health (250.1, 250.0, 125.1) rather than a hit's 50. A host that
    /// writes a death as an ordinary 50-point hit leaves the client told that the
    /// actor took a hit but never that it died.
    /// </remarks>
    public const byte FatalDamageSubtype = 0x12;

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

    /// <summary>Reads the slot a damage identifier names, in either generation.</summary>
    /// <param name="identifier">Identifier a hit arrived under.</param>
    /// <returns>The slot it names, or <c>null</c> for another record.</returns>
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

    /// <summary>Weapon a live bot carries: <c>0x18</c> in the capture's pairs.</summary>
    public const byte CarriedWeapon = 0x18;

    /// <summary>Magazine count the capture writes beside the weapon.</summary>
    public const byte CarriedAmmo = 0x0f;

    /// <summary>
    /// Builds one slot's whole actor block, in the order the capture writes it.
    /// </summary>
    /// <param name="sessionSlot">Key the player holds in the room's player table.</param>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="body">The bot's body: pose, aim, health and target.</param>
    /// <param name="firing">Whether the bot holds its trigger this tick.</param>
    /// <param name="fireBeat">Whether this tick is one a shot animation rides on.</param>
    /// <param name="vitals">
    /// Whether the vitals record rides in this frame. The capture writes it about
    /// once a second and on a change, not on every beat.
    /// </param>
    /// <returns>The block's records, or an empty list for a slot of zero.</returns>
    public static IReadOnlyList<UdpMessage> Build(
        byte sessionSlot,
        MatchRecordGenerationUtils generation,
        BotBodyState body,
        bool firing,
        bool fireBeat,
        bool vitals)
    {
        if (sessionSlot == 0)
        {
            return [];
        }

        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(body);

        var state = StateIdentifierOf(generation, sessionSlot);
        var position = BotPositionBodiesUtils.Build(body);
        var records = new List<UdpMessage>
        {
            new(state, 4, StateAttributeClass, BuildStateBody(body, firing, fireBeat)),
            new(
                (ushort)(state + 2),
                (byte)(position.Length + 1),
                PositionAttributeClass,
                position),
            new((ushort)(state + 4), 6, WeaponPairAttributeClass, BuildWeaponPair(body)),
            new((ushort)(state + 4), 2, WeaponAttributeClass, [0x00]),
            new((ushort)(state + 6), 7, AimAttributeClass, BuildAimBody(body)),
        };

        if (vitals)
        {
            records.Insert(
                1,
                new UdpMessage(
                    (ushort)(state + 1),
                    3,
                    VitalsAttributeClass,
                    [(byte)Math.Clamp((int)body.Health, 0, (int)BotBodyState.MaximumVitals), (byte)body.Stamina]));
        }

        return records;
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
    /// <param name="lethal">
    /// Whether the hit took the victim's last health, which writes
    /// <see cref="FatalDamageSubtype"/> and a blow the size of a whole health bar
    /// as the damage word.
    /// </param>
    /// <returns>The fifteen-byte damage record.</returns>
    public static UdpMessage BuildShot(
        byte victimSlot,
        MatchRecordGenerationUtils generation,
        int damage,
        byte weapon,
        float impactX,
        float impactY,
        float impactZ,
        ushort bearing,
        bool lethal = false)
    {
        ArgumentNullException.ThrowIfNull(generation);

        var body = new byte[DamageBodyLength];
        body[0] = lethal ? FatalDamageSubtype : DamageSubtype;

        // The killing blow states a whole health bar and not what was left of it:
        // the working host writes `7d 02` (250.02) on a victim whose own vitals
        // record read `68` (104) one beat earlier (t+310.81), so a client that
        // applies the word to its own bar reaches zero whatever that bar held. A
        // word of the host's tracked remainder leaves the client alive and then
        // contradicts it with the zero the vitals record carries — the state its
        // kill log crashes on.
        var points = lethal ? BotBodyState.MaximumVitals : damage;
        WriteUInt16(body, 2, (ushort)Math.Clamp(points * DamageScale, 0, ushort.MaxValue));
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
    /// The acting form is <c>42 ff 00 bd 08 78 00 d9 ff</c>: the first byte's low
    /// six bits are the action (<c>2</c> walking and aiming, <c>4</c> the held
    /// trigger, <c>7</c> the shot's beat), the second the target slot or
    /// <c>0xff</c>, the third a one exactly on the firing records, and the rest the
    /// point looked at. <b>The acting form is written only while an enemy is
    /// held</b>: the capture writes the standing <c>00 ff 00</c> on about eight
    /// beats in ten, its walking bots included, where this host wrote the acting
    /// form whenever a bot moved — a bot aiming down every corridor it patrolled.
    /// </remarks>
    /// <param name="body">The bot's body.</param>
    /// <param name="firing">Whether it holds its trigger this tick.</param>
    /// <param name="fireBeat">Whether this tick is one a shot animation rides on.</param>
    /// <returns>The three- or nine-byte body.</returns>
    private static byte[] BuildStateBody(BotBodyState body, bool firing, bool fireBeat)
    {
        if (body.TargetSlot == BotBodyState.NoTarget)
        {
            return [0x00, 0xff, 0x00];
        }

        var state = new byte[ActingStateBodyLength];
        state[0] = (byte)(0x40 | (fireBeat ? 7 : firing ? 4 : 2));
        state[1] = body.TargetSlot;
        state[2] = (byte)(firing ? 1 : 0);
        WriteInt16(state, 3, ToTenths(body.AimX));
        WriteInt16(state, 5, ToTenths(body.AimY));
        WriteInt16(state, 7, ToTenths(body.AimZ));
        return state;
    }

    /// <summary>Writes one float little-endian.</summary>
    /// <param name="body">Body to write into.</param>
    /// <param name="offset">Offset of its first byte.</param>
    /// <param name="value">Value to write.</param>
    internal static void WriteFloat(byte[] body, int offset, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(offset, 4), value);

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
    internal static short ToTenths(float value) =>
        (short)Math.Clamp(MathF.Round(value / 10f), short.MinValue, short.MaxValue);

    /// <summary>Writes one signed sixteen-bit value little-endian.</summary>
    /// <param name="body">Body to write into.</param>
    /// <param name="offset">Offset of the low byte.</param>
    /// <param name="value">Value to write.</param>
    internal static void WriteInt16(byte[] body, int offset, short value) =>
        WriteUInt16(body, offset, (ushort)value);

    /// <summary>Writes one unsigned sixteen-bit value little-endian.</summary>
    /// <param name="body">Body to write into.</param>
    /// <param name="offset">Offset of the low byte.</param>
    /// <param name="value">Value to write.</param>
    internal static void WriteUInt16(byte[] body, int offset, ushort value)
    {
        body[offset] = (byte)(value & 0xff);
        body[offset + 1] = (byte)((value >> 8) & 0xff);
    }
}
