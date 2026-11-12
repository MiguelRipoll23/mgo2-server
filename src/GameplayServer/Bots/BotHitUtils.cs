namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// What kind of blow a client's damage record is.
/// </summary>
public enum BotHitKind
{
    /// <summary>An ordinary body hit: a share of the victim's health.</summary>
    Body,

    /// <summary>A headshot: the whole of the victim's health.</summary>
    Headshot,

    /// <summary>A stun: the whole stun bar, and no health at all.</summary>
    Stun,

    /// <summary>A knife blow, which takes the whole bar and lands point-blank.</summary>
    Melee,

    /// <summary>A blast: a thrown grenade's damage at the point it went off.</summary>
    Blast,
}

/// <summary>
/// One hit a client wrote: the slot it landed on, what kind of blow it was, the
/// health it takes and the weapon it came from.
/// </summary>
/// <param name="VictimSlot">Slot the record's identifier names.</param>
/// <param name="Kind">What kind of blow it is.</param>
/// <param name="Damage">Health points it takes, from the record's damage word.</param>
/// <param name="Weapon">Weapon id the record carries at <c>+4</c>.</param>
public sealed record BotHit(byte VictimSlot, BotHitKind Kind, int Damage, byte Weapon)
{
    /// <summary>Whether the blow knocks the victim out instead of wounding it.</summary>
    public bool IsStun => Kind == BotHitKind.Stun;

    /// <summary>Whether the blow ends the victim whatever health it holds.</summary>
    public bool IsHeadshot => Kind == BotHitKind.Headshot;

    /// <summary>
    /// Whether the blow spends the victim's whole bar rather than the points its
    /// damage word names: a headshot's <c>12</c>, a knife's <c>91</c>, and the
    /// <c>42</c> a stun carries all write the bar whole.
    /// </summary>
    public bool TakesWholeBar => Kind is BotHitKind.Headshot or BotHitKind.Melee;
}

/// <summary>
/// The damage records a client writes, decoded from the wire.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on <c>.pcapngs/actions/actions.pcapng</c></b>, the working host's
/// dump of one round, on every class-<c>0x00</c> record the client wrote in it.
/// The standard record is fifteen bytes — subtype, a one, the damage word at
/// <c>+2</c> as a hundred-and-twenty-eighth of a health point, the weapon at
/// <c>+4</c>, the impact point, the shot's pitch and the shooter's heading — and
/// the subtype byte says what the blow is:
/// </para>
/// <list type="bullet">
/// <item><description><c>11</c> — a body hit: thirty to sixty-eight points of the
/// victim's two hundred and fifty (the capture's <c>f0 1b</c> is 55.9), which is
/// five or six of them to put an actor down.</description></item>
/// <item><description><c>12</c> — a headshot: the whole bar, <c>10 7d</c>, 250.1
/// (the client's own <c>12 01 10 7d 18</c> at t+48.35).</description></item>
/// <item><description><c>42</c> — a stun, weapon <c>02</c>, the Mk.2
/// tranquilliser, carrying the same 250.1 in its damage word and <b>taking no
/// health</b>: the victim is knocked out and the round counts a stun, not a death
/// (<c>42 01 10 7d 02</c> at t+221.45).</description></item>
/// <item><description><c>91</c> — a knife blow, weapon <c>01</c> (ST KNIFE), the
/// whole bar again (<c>91 00 10 7d 01</c> at t+409.36); the high bit is the melee
/// class and every such event in the capture is point-blank.</description></item>
/// <item><description><c>13</c> — a blast, <b>twenty-eight bytes</b>, weapon
/// <c>34</c> (52, the grenade): the standard fifteen and the blast's own point as
/// three <c>f32</c> at <c>+14</c> (<c>23559.6, -3200.0, -13918.3</c> at
/// t+160.68), with the damage word naming what the blast took —
/// <c>d0 5d</c>, 187.6 — and no shooter anywhere in it.</description></item>
/// </list>
/// <para>
/// <b>A blast names no shooter, so the peer that reports it is the thrower.</b>
/// The record's attacker is the session it arrived on and not a byte in the body;
/// that is what makes a grenade a teammate's damage and a grenade thrown at your
/// own feet a suicide both readable. The capture's two blasts before the round's
/// rejoin are reported by the client that threw them, on slots 15 and 15 again.
/// </para>
/// <para>
/// <b>The rest of the class is not a hit.</b> The operations and objective
/// streams ride the same class with lengths from twelve to thirty
/// (<c>0x0261</c>, <c>0x0a61</c>) and their first byte is a count, not a subtype.
/// They are refused here rather than read as a damage word, which is why the
/// identifiers are decoded as well: a rule record's identifier is not a slot's.
/// </para>
/// </remarks>
public static class BotHitUtils
{
    /// <summary>Subtype of an ordinary body hit.</summary>
    public const byte BodySubtype = BotActorRecordUtils.DamageSubtype;

    /// <summary>Subtype of a headshot.</summary>
    public const byte HeadshotSubtype = BotActorRecordUtils.HeadshotDamageSubtype;

    /// <summary>Subtype of a stun: the Mk.2's dart, or the knife's hold-up.</summary>
    public const byte StunSubtype = 0x42;

    /// <summary>Subtype of a knife blow.</summary>
    public const byte MeleeSubtype = 0x91;

    /// <summary>Subtype of a blast.</summary>
    public const byte BlastSubtype = 0x13;

    /// <summary>Length of the capture's blast record, fifteen bytes longer than a hit's.</summary>
    public const int BlastBodyLength = 28;

    /// <summary>Weapon a stun record carries: <c>02</c>, the Mk.2 tranquilliser.</summary>
    public const byte StunWeapon = 0x02;

    /// <summary>Weapon a knife blows record carries: <c>01</c>, ST KNIFE.</summary>
    public const byte MeleeWeapon = 0x01;

    /// <summary>
    /// Weapon a blast record carries: <c>34</c>, which is the grenade the weapon
    /// table names at id 52.
    /// </summary>
    public const byte BlastWeapon = 0x34;

    /// <summary>Offset of the blast's own point in a blast record.</summary>
    public const int BlastPointOffset = 14;

    /// <summary>
    /// Decodes a class-<c>0x00</c> record as a hit.
    /// </summary>
    /// <param name="identifier">Identifier the record arrived under, which names the victim's slot.</param>
    /// <param name="body">The record's body.</param>
    /// <returns>The hit, or <c>null</c> when the record is not a hit at all.</returns>
    public static BotHit? Decode(ushort identifier, ReadOnlySpan<byte> body)
    {
        if (body.Length is not (BotActorRecordUtils.DamageBodyLength or BlastBodyLength))
        {
            return null;
        }

        if (KindOf(body[0]) is not { } kind
            || BotActorRecordUtils.SlotOfDamageIdentifier(identifier) is not { } victim)
        {
            return null;
        }

        var points = (body[2] | (body[3] << 8)) / BotActorRecordUtils.DamageScale;
        return new BotHit(victim, kind, Math.Max(points, 1), body[4]);
    }

    /// <summary>
    /// The kind of blow a subtype names.
    /// </summary>
    /// <param name="subtype">The record's first byte.</param>
    /// <returns>The kind, or <c>null</c> for a byte that names no blow.</returns>
    public static BotHitKind? KindOf(byte subtype) => subtype switch
    {
        BodySubtype => BotHitKind.Body,
        HeadshotSubtype => BotHitKind.Headshot,
        StunSubtype => BotHitKind.Stun,
        MeleeSubtype => BotHitKind.Melee,
        BlastSubtype => BotHitKind.Blast,
        _ => null,
    };
}
