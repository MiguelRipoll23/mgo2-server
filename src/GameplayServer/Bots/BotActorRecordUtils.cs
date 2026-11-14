using System.Buffers.Binary;
using Mgo2Server.GameplayServer.Combat;
using Mgo2Server.GameplayServer.Gameplay;
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
/// the state (<see cref="DamageIdentifierOf"/>).
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
/// as a sixteenth of a health point, weapon, impact point, the shot's pitch and
/// the shooter's heading.
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
    /// <c>22 1A</c> or <c>21 1A</c></b> — see <see cref="BotPositionBodiesUtils"/>;
    /// a living one is sixteen.
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
    /// Subtype of a headshot: the blow that ends its victim at once, whose damage word
    /// is the victim's whole remaining bar rather than the hit's own damage.
    /// </summary>
    /// <remarks>
    /// <b>Measured on <c>working-server-v2.pcapng</c>.</b> Every kill the feed credits a
    /// headshot for ends on a <c>0x12</c> record — twelve of twelve, at damage words of
    /// 209 to 250 — and none of the thirty-two kills that were not headshots does: they
    /// end on an ordinary <c>0x11</c> of about 56 points that ran the health out, with
    /// the victim's vitals reaching zero beside it. An earlier reading took <c>0x12</c> for
    /// "the killing blow" from nine records that all happened to be headshots, and wrote
    /// every death as one, so every bot kill by this host was announced as a headshot
    /// death. A client's own record is the same: its kill by headshot is <c>12 01 10 7d</c>.
    /// </remarks>
    public const byte HeadshotDamageSubtype = 0x12;

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
    /// state record.
    /// </summary>
    /// <remarks>
    /// <b>The arithmetic cannot settle this on its own, and the target byte does.</b>
    /// A slot's damage record is three below its state record, which is the *same
    /// number* as the state record before it plus seven: <c>0x0868</c> is slot 4's
    /// (<c>state(4) 0x086b − 3</c>) and equally slot 3's <c>state(3) 0x0861 + 7</c>.
    /// Reading it as <c>state + 7</c> is what filed a live player's fire on the slot
    /// before the one it was shooting. The client's own state body says which: on
    /// <c>.pcapngs/myserver.pcapng</c> it aims at slot 4 (its state's target byte
    /// reads <c>4</c>) while writing eight hits under <c>0x0868</c>, and its target
    /// byte reads <c>0xff</c> on the beat it writes <c>0x0890</c> — slot 8's
    /// <c>state(8) 0x0893 − 3</c>. A player who shot bot 4 instead killed slot 3,
    /// which stands on his own side, so the feed read as a teammate kill and the bot
    /// he was aiming at never took a point of damage.
    /// </remarks>
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
            // **Three below the state, for every family.** The next slot's
            // `state + 7` is the same identifier, so the client's own target byte is
            // what settles it (`DamageIdentifierOf`). On `actions.pcapng`, 415 of 415
            // host hits and 22 of 22 client hits land on the vitals that move.
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
    /// <param name="publishState">
    /// Whether the state record rides in this frame. An idle actor's standing form
    /// is written about once a second; an acting one's is written on every beat
    /// (<see cref="BotBodyState.NextStateMilliseconds"/>).
    /// </param>
    /// <param name="now">Monotonic milliseconds, which decides a reload or recovery beat.</param>
    /// <returns>The block's records, or an empty list for a slot of zero.</returns>
    public static IReadOnlyList<UdpMessage> Build(
        byte sessionSlot,
        MatchRecordGenerationUtils generation,
        BotBodyState body,
        bool firing,
        bool fireBeat,
        bool vitals,
        bool publishState = true,
        long now = 0)
    {
        if (sessionSlot == 0)
        {
            return [];
        }

        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(body);

        var state = StateIdentifierOf(generation, sessionSlot);
        var position = BotPositionBodiesUtils.Build(body);
        var records = new List<UdpMessage>(6);
        if (publishState)
        {
            records.Add(new(state, 4, StateAttributeClass, BuildStateBody(body, firing, fireBeat, now)));
        }

        records.Add(new(
            (ushort)(state + 2),
            (byte)(position.Length + 1),
            PositionAttributeClass,
            position));
        records.Add(new((ushort)(state + 4), 6, WeaponPairAttributeClass, BuildWeaponPair(body)));
        records.Add(new((ushort)(state + 4), 2, WeaponAttributeClass, [0x00]));
        records.Add(new((ushort)(state + 6), 7, AimAttributeClass, BuildAimBody(body)));

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
    /// The weapon pair a bot carries, which a corpse keeps.
    /// </summary>
    /// <remarks>
    /// <b>A dead bot still carries its gun.</b> Across `actions.pcapng`'s 231 dead
    /// beats the pair is `00 18 00 00 0f` on 197 and `00 19 00 00 0f` on 26, and zero
    /// only eight times; this host blanked it on all 2,449 of its own. The second
    /// byte is the weapon id the bot's role chose and the last is the magazine:
    /// `shooting-and-reloading.pcapng` falls it `0F -> 0E -> … -> 00` as the trigger
    /// spends rounds and refills it on the reload's last beat.
    /// </remarks>
    /// <param name="body">The bot whose weapon and magazine the pair reports.</param>
    /// <returns>The pair's five bytes.</returns>
    private static byte[] BuildWeaponPair(BotBodyState body) =>
    [
        WeaponPairUtils.NoFlag,
        (byte)body.Weapon,
        0x00,
        0x00,
        (byte)Math.Clamp(body.MagazineRounds, 0, CarriedAmmo),
    ];

    /// <summary>
    /// The damage record one bot's hit travels under, on the slot it hit.
    /// </summary>
    /// <param name="victimSlot">Player the hit landed on.</param>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="damage">Health points the hit takes.</param>
    /// <param name="weapon">Item id the hit carries.</param>
    /// <param name="impactX">Impact <c>x</c>, in stage units.</param>
    /// <param name="impactY">Impact height, in stage units.</param>
    /// <param name="impactZ">Impact <c>z</c>, in stage units.</param>
    /// <param name="bearing">Bearing the shot came from, as the recorded word.</param>
    /// <param name="headshot">
    /// Whether the hit is a headshot, which writes <see cref="HeadshotDamageSubtype"/>
    /// and a blow the size of a whole health bar as the damage word. A hit that only
    /// runs the last health out is an ordinary one.
    /// </param>
    /// <param name="pitch">The shot's vertical angle, as <see cref="BotShot.Pitch"/> states it.</param>
    /// <param name="kind">What the carried weapon makes the hit.</param>
    /// <returns>The fifteen-byte damage record.</returns>
    public static UdpMessage BuildShot(
        byte victimSlot,
        byte shooterSlot,
        MatchRecordGenerationUtils generation,
        int damage,
        byte weapon,
        float impactX,
        float impactY,
        float impactZ,
        ushort bearing,
        bool headshot = false,
        short pitch = 0,
        BotWeaponKind kind = BotWeaponKind.Firearm)
    {
        ArgumentNullException.ThrowIfNull(generation);

        var body = new byte[DamageBodyLength];
        body[0] = kind switch
        {
            BotWeaponKind.StunDart => headshot
                ? DamageRecordUtils.HeadStunSubtype
                : DamageRecordUtils.BodyStunSubtype,
            BotWeaponKind.StunKnife or BotWeaponKind.LethalKnife =>
                DamageRecordUtils.MeleeSubtype,
            _ => headshot ? HeadshotDamageSubtype : DamageSubtype,
        };

        // The knife's marker tells the stun stab from the critical stab.
        body[1] = kind is BotWeaponKind.LethalKnife ? (byte)0x01 : (byte)0x00;
        // The damage word encodes both the damage quantity and the shooter's actor
        // slot: (damage << 7) | shooterSlot. Measured, not inferred: every one of the
        // capture's client-written hits decodes to slot 16, its own, whatever victim it
        // names, and the high field equals the health or stamina the bar loses.
        var damageQuantity = kind is BotWeaponKind.StunKnife or BotWeaponKind.LethalKnife || headshot ? BotBodyState.MaximumVitals : damage;
        var damageWord = (Math.Clamp(damageQuantity, 0, 0x1FF) << 7) | (shooterSlot & 0x1F);

        WriteUInt16(body, 2, (ushort)damageWord);

        body[4] = weapon;
        WriteInt16(body, 5, ToTenths(impactX));
        WriteInt16(body, 7, ToTenths(impactY));
        WriteInt16(body, 9, ToTenths(impactZ));
        WriteInt16(body, 11, pitch);
        WriteUInt16(body, 13, bearing);

        return new UdpMessage(
            DamageIdentifierOf(generation, victimSlot),
            (byte)(DamageBodyLength + 1),
            DamageAttributeClass,
            body);
    }

    /// <summary>
    /// The state body: the capture's three-byte standing form, or the nine-byte
    /// acting form when the actor aims, fires or is held.
    /// </summary>
    /// <remarks>
    /// The bytes and the <c>ActorAction</c> values they carry are measured in
    /// <see cref="BotStateBodiesUtils"/>; the body is built there so this file keeps
    /// to the repository's size limit.
    /// </remarks>
    /// <param name="body">The bot's body.</param>
    /// <param name="firing">Whether it holds its trigger this tick.</param>
    /// <param name="fireBeat">Whether this tick is one a shot animation rides on.</param>
    /// <param name="now">Monotonic milliseconds, which decides a reload or recovery beat.</param>
    /// <returns>The three- or nine-byte body.</returns>
    private static byte[] BuildStateBody(BotBodyState body, bool firing, bool fireBeat, long now) =>
        BotStateBodiesUtils.Build(body, firing, fireBeat, now);

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
