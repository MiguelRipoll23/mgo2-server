using Mgo2Server.GameplayServer.Bots;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Combat;

/// <summary>
/// The damage records a client writes, decoded from the wire for a player or bot
/// victim alike.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three body lengths share the record.</b> The standard hit is fifteen bytes
/// — subtype, a marker byte at <c>+1</c>, the damage word at <c>+2</c>, the
/// weapon at <c>+4</c>, the impact point, pitch and bearing. The word packs the
/// points in the high bits and a low field of five
/// (<c>(points &lt;&lt; 7) | field</c>). <b>The low field is not the shooter.</b> It
/// reads <c>16</c> on every client record of both captures, and in both the client
/// itself held slot 16, so the captures never distinguish a slot from a constant;
/// a live run whose player held another slot had its kills credited to slot 16 when
/// this host preferred the field. The reporting peer is the shooter. A blast is twenty-eight
/// (<c>0x13</c>), with its own
/// point as three <c>f32</c> at <c>+14</c>. A short hit is eleven: the same first
/// eleven bytes and no pitch or bearing, which is how gas, a non-lethal device
/// blast, a critical knife and the stun stab arrive.
/// </para>
/// <para>
/// <b>The subtypes are measured, not inferred from the nibble alone.</b> The
/// console's <c>damageClass</c> gives the family, and the captures in
/// <c>.pcapngs/</c> give the row. A <c>0x41</c> or <c>0xC1</c> spends the victim's
/// stamina and leaves health at <c>250</c> (<c>friendly-fire-enabled.pcapng</c>
/// slot 1, weapon <c>0x02</c>); a <c>0x21</c> or <c>0x24</c> is the CQC impact
/// that does the same (<c>cqc.pcapng</c> slot 1, weapons <c>0x6b</c>–<c>0x6d</c>);
/// a <c>0x40</c> is a sleeping device's blast at eleven bytes
/// (<c>different-explosives-mines-grenades-on-me.pcapng</c> slot 7, weapons
/// <c>0x41</c>/<c>0x43</c>); an <c>0xAD</c> is the stun stab at 250 points
/// (<c>auto-aim-enabled.pcapng</c> slot 2, weapon <c>0x01</c>). Reading any of
/// these as health damage puts a body down that the client was told was only
/// knocked out.
/// </para>
/// <para>
/// <b>The rest of class <c>0x00</c> is not a hit.</b> The operations and
/// objective streams write the same class with lengths from ten to thirty
/// (<c>0x0261</c>, <c>0x0a61</c>) and their first byte is a count, not a subtype.
/// They are refused here as well as by their identifier, which is not a slot's
/// damage record.
/// </para>
/// </remarks>
public static class DamageRecordUtils
{
    /// <summary>Short record length: eleven bytes, with no pitch or bearing.</summary>
    public const int ShortBodyLength = 11;

    /// <summary>Length of the standard damage body.</summary>
    public const int BodyLength = BotActorRecordUtils.DamageBodyLength;

    /// <summary>Length of a blast's body, fifteen bytes longer than a hit's.</summary>
    public const int BlastBodyLength = 28;

    /// <summary>Subtype of an ordinary body hit.</summary>
    public const byte BodySubtype = BotActorRecordUtils.DamageSubtype;

    /// <summary>
    /// Subtype of a body hit from a shotgun, measured on a M870 (<c>0x25</c>) in
    /// <c>pcapng3</c> with 109.9 points.
    /// </summary>
    public const byte AlternateBodySubtype = 0x15;

    /// <summary>Subtype of a headshot.</summary>
    public const byte HeadshotSubtype = BotActorRecordUtils.HeadshotDamageSubtype;

    /// <summary>Subtype of a lethal blast, measured with grenade, WP, claymore and C4.</summary>
    public const byte ExplosiveSubtype = 0x13;

    /// <summary>
    /// Subtype of a non-lethal blast: a sleeping claymore or sleeping C4. Its
    /// eleven-byte body spends stamina and takes no health.
    /// </summary>
    public const byte NonLethalExplosiveSubtype = 0x40;

    /// <summary>Subtype of gas, whose short record names the damage source at <c>+4</c>.</summary>
    public const byte GasSubtype = 0x0E;

    /// <summary>Subtype of a critical knife blow, an eleven-byte whole-bar record.</summary>
    public const byte CriticalKnifeSubtype = 0x10;

    /// <summary>Subtype of a CQC reach or impact (the console's non-lethal class 2).</summary>
    public const byte CqcSubtype = 0x21;

    /// <summary>Subtype of a CQC throw's impact (the console's non-lethal class 2).</summary>
    public const byte CqcImpactSubtype = 0x24;

    /// <summary>Subtype of a body-zone tranquilliser hit.</summary>
    public const byte BodyStunSubtype = 0x41;

    /// <summary>Subtype of a head-zone tranquilliser hit.</summary>
    public const byte HeadStunSubtype = 0x42;

    /// <summary>
    /// Subtype of the non-lethal hit the console calls the ordinary knife hit:
    /// <c>0xC1</c>, measured on the MK2 with stamina falling and health untouched.
    /// </summary>
    public const byte NonLethalSubtype = 0xC1;

    /// <summary>Subtype of a knife blow whose marker byte decides the kind.</summary>
    public const byte MeleeSubtype = 0x91;

    /// <summary>
    /// Subtype of the stun stab: a whole stun bar at eleven bytes, weapon
    /// <c>0x01</c>. The console's own notes name this damage type
    /// <c>45</c>/<c>173</c> — <c>0x2D</c>/<c>0xAD</c>.
    /// </summary>
    public const byte StunStabSubtype = 0xAD;

    /// <summary>The stun stab's other recorded form, <c>0x2D</c>.</summary>
    public const byte StunStabAlternativeSubtype = 0x2D;

    /// <summary>Weapon a stun record carries: <c>0x02</c>, the MK2 tranquilliser.</summary>
    public const byte StunWeapon = (byte)ItemIdentifier.StKnife;

    /// <summary>Weapon a blast record carries: <c>0x34</c>, the grenade.</summary>
    public const byte ExplosiveWeapon = (byte)ItemIdentifier.Grenade;

    /// <summary>Offset of a blast's own point in its twenty-eight-byte body.</summary>
    public const int BlastPointOffset = 14;

    /// <summary>
    /// Shift from the damage word to its damage quantity; the low five bits name the
    /// shooter.
    /// </summary>
    /// <remarks>
    /// <b>The high field is the damage, measured against the bar it moves.</b> Over
    /// `myserver.pcapng`, a `0x11` of 56 takes 250 health to 194 and one of 34 takes 194
    /// to 160, and a `0x42` of 250 takes 250 stamina to 0. The low field reads `16` on
    /// every client record of both captures — the client's own slot in both, so it is
    /// kept as a field and never used to name a killer.
    /// </remarks>
    private const int DamageShift = 7;

    /// <summary>Mask of the word's low field, which is recorded and not credited.</summary>
    private const int ShooterSlotMask = 0x1F;

    /// <summary>
    /// Decodes a class-<c>0x00</c> record as a hit.
    /// </summary>
    /// <param name="identifier">Identifier the record arrived under, which names the victim's slot.</param>
    /// <param name="body">The record's body.</param>
    /// <returns>The hit, or <c>null</c> when the record is not a hit at all.</returns>
    public static DamageHit? Decode(ushort identifier, ReadOnlySpan<byte> body)
    {
        if (body.Length is not (ShortBodyLength or BodyLength or BlastBodyLength))
        {
            return null;
        }

        if (KindOf(body) is not { } kind
            || BotActorRecordUtils.SlotOfDamageIdentifier(identifier) is not { } victim)
        {
            return null;
        }

        var word = body[2] | (body[3] << 8);
        var damage = word >> DamageShift;
        var shooter = (byte)(word & ShooterSlotMask);
        return new DamageHit(
            victim,
            kind,
            Math.Max(damage, 1),
            body[4],
            shooter is > 0 and < 24 ? shooter : null);
    }

    /// <summary>
    /// The kind of blow a record is, from its subtype, marker and length.
    /// </summary>
    /// <param name="body">The record's body, which must hold at least the header.</param>
    /// <returns>The kind, or <c>null</c> for a body that names no blow.</returns>
    public static DamageKind? KindOf(ReadOnlySpan<byte> body)
    {
        if (body.Length < 5)
        {
            return null;
        }

        return body[0] switch
        {
            BodySubtype when body.Length == BodyLength => DamageKind.Body,
            AlternateBodySubtype when body.Length == BodyLength => DamageKind.Body,
            HeadshotSubtype when body.Length == BodyLength => DamageKind.Headshot,
            ExplosiveSubtype when body.Length == BlastBodyLength => DamageKind.Explosive,
            NonLethalExplosiveSubtype when body.Length == ShortBodyLength => DamageKind.ExplosiveStun,
            GasSubtype when body.Length == ShortBodyLength => DamageKind.Gas,
            CriticalKnifeSubtype when body.Length == ShortBodyLength => DamageKind.MeleeKill,
            CqcSubtype or CqcImpactSubtype when body.Length == BodyLength => DamageKind.Knockout,
            BodyStunSubtype or HeadStunSubtype when body.Length == BodyLength => DamageKind.Stun,
            NonLethalSubtype when body.Length == BodyLength => DamageKind.Stun,
            StunStabSubtype or StunStabAlternativeSubtype when body.Length == ShortBodyLength
                => DamageKind.Stun,
            MeleeSubtype when body.Length == BodyLength => MeleeKindOf(body),
            _ => null,
        };
    }

    /// <summary>
    /// The knife's three blows: a stun stab, a critical stab and a slice.
    /// </summary>
    /// <remarks>
    /// The marker byte at <c>+1</c> and the damage word decide. A zero marker with
    /// a whole stun bar is the stun stab (<c>91 00 10 7d</c>); a one marker with a
    /// whole bar is the critical stab; anything else is the slice, which takes the
    /// points its word names — measured over <c>auto-aim-enabled.pcapng</c> as
    /// <c>0x91</c> hits of 25 to 42 points that move health.
    /// </remarks>
    /// <param name="body">The fifteen-byte knife record.</param>
    /// <returns>The knife blow's kind.</returns>
    private static DamageKind MeleeKindOf(ReadOnlySpan<byte> body)
    {
        var damage = (body[2] | (body[3] << 8)) >> DamageShift;
        if (damage < BotBodyState.MaximumVitals)
        {
            return DamageKind.Melee;
        }

        return body[1] == 0 ? DamageKind.Stun : DamageKind.MeleeKill;
    }
}
