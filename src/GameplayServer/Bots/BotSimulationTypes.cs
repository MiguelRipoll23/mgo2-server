using Mgo2Server.GameplayServer.Gameplay;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// One actor a bot can place against it: a slot, the side it holds, and where it
/// is in the stage's own units.
/// </summary>
/// <param name="SessionSlot">Key the actor holds in the room's player table.</param>
/// <param name="Side">Side it stands on.</param>
/// <param name="X">Stage <c>x</c>.</param>
/// <param name="Y">Stage height.</param>
/// <param name="Z">Stage <c>z</c>.</param>
/// <param name="IsBot">Whether it is one of the room's bots.</param>
public sealed record BotActorView(byte SessionSlot, short Side, float X, float Y, float Z, bool IsBot)
{
    /// <summary>
    /// The stance the actor's own position record reports, so a bot shoots a
    /// crouched, prone or lying player at the height that player actually holds.
    /// </summary>
    public ActorStance Stance { get; init; } = ActorStance.Standing;
}

/// <summary>
/// What one bot's tick did: the body it now holds, whether it fired, and the hit
/// that fire landed.
/// </summary>
/// <param name="Body">The bot's body after the tick.</param>
/// <param name="Fired">Whether it fired this tick.</param>
/// <param name="Shot">The hit it landed, when it fired.</param>
public sealed record BotAdvance(BotBodyState Body, bool Fired, BotShot? Shot);

/// <summary>
/// The way a bot's carried weapon writes its hit.
/// </summary>
public enum BotWeaponKind
{
    /// <summary>An ordinary bullet.</summary>
    Firearm,

    /// <summary>A Mk.2 dart: stamina, and sleep when the bar empties.</summary>
    StunDart,

    /// <summary>The ST KNIFE: a stun stab with no health loss.</summary>
    StunKnife,

    /// <summary>A lethal blade: the whole health bar.</summary>
    LethalKnife,
}

/// <summary>
/// One hit a bot landed on another actor: what the damage record carries.
/// </summary>
/// <param name="VictimSlot">Slot the hit landed on.</param>
/// <param name="Damage">Health points it takes.</param>
/// <param name="Weapon">Item id the damage record carries.</param>
/// <param name="ImpactX">Impact <c>x</c>, in stage units.</param>
/// <param name="ImpactY">Impact height, in stage units.</param>
/// <param name="ImpactZ">Impact <c>z</c>, in stage units.</param>
/// <param name="Bearing">The shooter's own heading, the word its position record carries.</param>
public sealed record BotShot(
    byte VictimSlot,
    int Damage,
    byte Weapon,
    float ImpactX,
    float ImpactY,
    float ImpactZ,
    ushort Bearing)
{
    /// <summary>
    /// Whether this hit is a headshot: the <c>0x12</c> damage subtype, which ends its
    /// victim at once. The reference writes it on exactly the kills its feed credits a
    /// headshot for; a kill that ran the victim's health out is an ordinary <c>0x11</c>.
    /// </summary>
    public bool Headshot { get; init; }

    /// <summary>
    /// What the carried weapon makes this hit: a bullet, a tranquilliser dart, a
    /// stun stab or a lethal blade.
    /// </summary>
    public BotWeaponKind Kind { get; init; } = BotWeaponKind.Firearm;

    /// <summary>
    /// The shot's vertical angle as the record carries it: a sixteen-bit turn,
    /// positive when the shooter looks down on the point it hit.
    /// </summary>
    public short Pitch { get; init; }

    /// <summary>
    /// Whether the blow was one a body could take: <c>false</c> when the victim was
    /// already down when it was fired.
    /// </summary>
    /// <remarks>
    /// <b>A hit is never written on a corpse.</b> The working host's 350 host-written
    /// damage records all name a living actor, and a record written on a slot this
    /// host already holds at zero is what puts the body back on its feet on the
    /// client: the client applies the word it reads, so the actor the player keeps
    /// shooting is one this host drops every bullet on. Measured on
    /// `.pcapngs/myserver.pcapng`, 54 of 85 damage records had no vitals answer and
    /// 14 of the 18 client-written hits named a slot at zero, where
    /// `.pcapngs/actions/actions.pcapng` answers 48 of its 49 client-written hits on
    /// a living slot and holds one on a corpse.
    /// </remarks>
    public bool Landed { get; init; } = true;
}

/// <summary>
/// A death the room owes the client a kill-feed record for.
/// </summary>
/// <param name="KillerSlot">Slot the kill is credited to.</param>
/// <param name="VictimSlot">Slot that went down.</param>
/// <param name="Headshot">Whether the blow that ended it was a headshot.</param>
public sealed record BotKill(byte KillerSlot, byte VictimSlot, bool Headshot = false);
