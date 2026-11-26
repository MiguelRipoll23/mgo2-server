namespace Mgo2Server.GameplayServer.Combat;

/// <summary>
/// Where a blow landed. The wire's hit zone is the subtype itself:
/// <c>0x11</c>/<c>0x41</c> are body hits and <c>0x12</c>/<c>0x42</c> head hits.
/// </summary>
public enum HitZone
{
    /// <summary>No body zone: a blast or a gas cloud.</summary>
    None,

    /// <summary>The body.</summary>
    Body,

    /// <summary>The head.</summary>
    Head,
}

/// <summary>
/// What a blow does when it lands. This is the distinction the old single
/// <c>Stun</c> kind hid: a tranquilliser and a sleeping device sleep an emptied
/// stamina bar, while CQC and the knife family leave the stun pose.
/// </summary>
public enum DamageEffect
{
    /// <summary>Takes the health points its damage word names.</summary>
    Wound,

    /// <summary>Takes the whole health bar, whatever it held.</summary>
    Critical,

    /// <summary>Spends stamina; an emptied bar shows the stun pose.</summary>
    Stun,

    /// <summary>Spends stamina; an emptied bar sleeps.</summary>
    Sleep,
}

/// <summary>
/// What a damage record's subtype says the blow is, for a player or a bot.
/// </summary>
/// <remarks>
/// <para>
/// <b>The console's damage class is the subtype's high nibble</b> (<c>rpdt.js</c>
/// <c>damageClass</c>): <c>1</c> is lethal, <c>2</c> and <c>4</c> are non-lethal,
/// and <c>0</c> is gas. That class is a family, not the effect: <c>0x40</c> and
/// the <c>0x41</c>/<c>0x42</c> darts all sit in the non-lethal family, but the
/// first two sleep and the CQC/knife rows do not.
/// </para>
/// <para>
/// <b>The names below are the measured wire rows</b>, from
/// <c>docs/protocol/ITEM_AND_DAMAGE_TABLE.md</c>. Use <see cref="DamageHit.Zone"/>
/// and <see cref="DamageHit.Effect"/> for behaviour; the kind is the source row.
/// </para>
/// </remarks>
public enum DamageKind
{
    /// <summary>An ordinary body hit (<c>0x11</c>, or the shotgun's <c>0x15</c>).</summary>
    BodyShot,

    /// <summary>A headshot (<c>0x12</c>): the whole health bar.</summary>
    Headshot,

    /// <summary>A knife slice (<c>0x91</c> below a whole bar), taking its word's points.</summary>
    Melee,

    /// <summary>
    /// A critical knife blow: the short <c>0x10</c> record, or <c>0x91</c> with
    /// marker <c>01</c> and a whole bar in its damage word.
    /// </summary>
    CriticalMelee,

    /// <summary>A lethal blast (<c>0x13</c>, 28-byte body): a grenade, satchel or placed device.</summary>
    Explosion,

    /// <summary>
    /// A sleeping device's blast (<c>0x40</c>): a sleeping claymore, sleeping C4
    /// or S.G. mine. It spends stamina, takes no health and sleeps an emptied bar.
    /// </summary>
    SleepingExplosion,

    /// <summary>A tranquilliser dart in the body (<c>0x41</c>): stamina and sleep.</summary>
    TranquilliserBody,

    /// <summary>A tranquilliser dart in the head (<c>0x42</c>): the whole stun bar and sleep.</summary>
    TranquilliserHead,

    /// <summary>
    /// A stun stab: <c>0x91</c> with marker <c>00</c>, or the short
    /// <c>0xAD</c>/<c>0x2D</c> form. Stamina, no health, stun pose.
    /// </summary>
    StunStab,

    /// <summary>
    /// The non-lethal knife hit the console calls the ordinary knife hit
    /// (<c>0xC1</c>): stamina, no health, stun pose.
    /// </summary>
    NonLethalMelee,

    /// <summary>
    /// A CQC reach or throw impact (<c>0x21</c>/<c>0x24</c>), or the drum can,
    /// ground fire or cardboard box: stamina, no health, stun pose.
    /// </summary>
    Cqc,

    /// <summary>Gas (<c>0x0E</c>), which takes the small amount its word names.</summary>
    Gas,
}

/// <summary>
/// One hit a client wrote: the slot it landed on, what kind of blow it was, the
/// health or stamina it takes and the weapon it came from.
/// </summary>
/// <param name="VictimSlot">Slot the record's identifier names.</param>
/// <param name="Kind">What kind of blow it is.</param>
/// <param name="Damage">
/// Health points it takes, or the stamina the stun spends, from the record's
/// damage word.
/// </param>
/// <param name="Weapon">Weapon or damage-source id the record carries at <c>+4</c>.</param>
/// <param name="ShooterSlot">
/// The field the record's damage word carries in its low five bits, or <c>null</c>
/// when it encodes none. The word is <c>(points &lt;&lt; 7) | field</c>. It is
/// recorded and <b>not credited</b>: every client record in both captures reads
/// <c>16</c> there, and in both the client itself held slot 16, so the field has
/// never been shown to name the shooter. The peer that reported the record is the
/// shooter (<c>GameplayServerService.NoteCombatRecord</c>).
/// </param>
/// <param name="Subtype">The record's first byte, kept for the echo and for logs.</param>
public sealed record DamageHit(
    byte VictimSlot,
    DamageKind Kind,
    int Damage,
    byte Weapon,
    byte? ShooterSlot = null,
    byte Subtype = 0)
{
    /// <summary>Where the blow landed.</summary>
    public HitZone Zone => Kind switch
    {
        DamageKind.Headshot or DamageKind.TranquilliserHead => HitZone.Head,
        DamageKind.BodyShot or DamageKind.Melee or DamageKind.CriticalMelee
            or DamageKind.TranquilliserBody or DamageKind.StunStab
            or DamageKind.NonLethalMelee or DamageKind.Cqc => HitZone.Body,
        _ => HitZone.None,
    };

    /// <summary>What the blow does: wound, take the whole bar, stun or sleep.</summary>
    public DamageEffect Effect => Kind switch
    {
        DamageKind.Headshot or DamageKind.CriticalMelee => DamageEffect.Critical,
        DamageKind.TranquilliserBody or DamageKind.TranquilliserHead
            or DamageKind.SleepingExplosion => DamageEffect.Sleep,
        DamageKind.StunStab or DamageKind.NonLethalMelee or DamageKind.Cqc
            => DamageEffect.Stun,
        _ => DamageEffect.Wound,
    };

    /// <summary>
    /// Whether the blow knocks the victim out instead of wounding it: a stun, a
    /// sleeping device or a CQC impact.
    /// </summary>
    public bool IsKnockout => Effect is DamageEffect.Stun or DamageEffect.Sleep;

    /// <summary>Whether an emptied bar sleeps the victim: a device or a tranquilliser.</summary>
    public bool SleepsWhenEmpty => Effect == DamageEffect.Sleep;

    /// <summary>
    /// Whether an emptied bar shows the stun pose rather than lying asleep: CQC
    /// and the knife family.
    /// </summary>
    public bool ShowsStunPose => Effect == DamageEffect.Stun;

    /// <summary>Whether the blow is a headshot, which the score counts.</summary>
    public bool IsHeadshot => Kind == DamageKind.Headshot;

    /// <summary>Whether the blow ends the victim whatever health it holds.</summary>
    public bool TakesWholeBar => Effect == DamageEffect.Critical;

    /// <summary>Whether the damage record is an explosion.</summary>
    public bool IsExplosive => Kind is DamageKind.Explosion or DamageKind.SleepingExplosion;

    /// <summary>Whether the explosion is a sleeping device's.</summary>
    public bool IsSleepingDevice => Kind == DamageKind.SleepingExplosion;

    /// <summary>Whether the blow is the tranquilliser pistol's body or head hit.</summary>
    public bool IsTranquilliser => Kind is DamageKind.TranquilliserBody
        or DamageKind.TranquilliserHead;

    /// <summary>
    /// Whether the blow takes health, which is every kind but the stun and sleep
    /// family.
    /// </summary>
    public bool TakesHealth => Effect is DamageEffect.Wound or DamageEffect.Critical;
}
