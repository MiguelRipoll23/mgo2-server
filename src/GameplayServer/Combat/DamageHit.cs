namespace Mgo2Server.GameplayServer.Combat;

/// <summary>
/// What a damage record's subtype says the blow is, for a player or a bot.
/// </summary>
/// <remarks>
/// <b>The console's damage class is the subtype's high nibble</b> (<c>rpdt.js</c>
/// <c>damageClass</c>): <c>1</c> is lethal, <c>2</c> and <c>4</c> are non-lethal,
/// and <c>0</c> is gas. The rows below are the measured subtypes, each named by
/// the capture and the console table in <c>docs/protocol/ITEM_AND_DAMAGE_TABLE.md</c>.
/// </remarks>
public enum DamageKind
{
    /// <summary>An ordinary body hit: a share of the victim's health.</summary>
    Body,

    /// <summary>A headshot: the whole of the victim's health.</summary>
    Headshot,

    /// <summary>A knife or melee blow that takes the points its damage word names.</summary>
    Melee,

    /// <summary>A critical knife blow, which takes the whole bar.</summary>
    MeleeKill,

    /// <summary>A lethal explosion: a grenade, satchel or placed device.</summary>
    Explosive,

    /// <summary>
    /// A non-lethal explosion: a sleeping claymore or sleeping C4. It knocks the
    /// victim out and takes no health.
    /// </summary>
    ExplosiveStun,

    /// <summary>A tranquilliser dart, stun stab or other knock-out blow.</summary>
    Stun,

    /// <summary>
    /// A CQC throw or impact against a drum can, ground fire or the cardboard
    /// box. It spends stamina and takes no health.
    /// </summary>
    Knockout,

    /// <summary>Gas, which takes the small amount its damage word names.</summary>
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
public sealed record DamageHit(
    byte VictimSlot,
    DamageKind Kind,
    int Damage,
    byte Weapon,
    byte? ShooterSlot = null)
{
    /// <summary>
    /// Whether the blow knocks the victim out instead of wounding it. A stun, a
    /// CQC impact and a sleeping device are all this kind of blow.
    /// </summary>
    public bool IsStun => Kind is DamageKind.Stun
        or DamageKind.Knockout
        or DamageKind.ExplosiveStun;

    /// <summary>Whether the blow is a headshot, which the score counts.</summary>
    public bool IsHeadshot => Kind == DamageKind.Headshot;

    /// <summary>Whether the blow ends the victim whatever health it holds.</summary>
    public bool TakesWholeBar => Kind is DamageKind.Headshot or DamageKind.MeleeKill;

    /// <summary>Whether the damage record is an explosion.</summary>
    public bool IsExplosive => Kind is DamageKind.Explosive or DamageKind.ExplosiveStun;

    /// <summary>
    /// Whether the blow takes health, which is every kind but the knock-out family.
    /// </summary>
    public bool IsLethal => !IsStun;
}
