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
public sealed record BotActorView(byte SessionSlot, short Side, float X, float Y, float Z, bool IsBot);

/// <summary>
/// What one bot's tick did: the body it now holds, whether it fired, and the hit
/// that fire landed.
/// </summary>
/// <param name="Body">The bot's body after the tick.</param>
/// <param name="Fired">Whether it fired this tick.</param>
/// <param name="Shot">The hit it landed, when it fired.</param>
public sealed record BotAdvance(BotBodyState Body, bool Fired, BotShot? Shot);

/// <summary>
/// One hit a bot landed on another actor: what the damage record carries.
/// </summary>
/// <param name="VictimSlot">Slot the hit landed on.</param>
/// <param name="Damage">Health points it takes.</param>
/// <param name="Weapon">Weapon id the record carries.</param>
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
    /// The shot's vertical angle as the record carries it: a sixteen-bit turn,
    /// positive when the shooter looks down on the point it hit.
    /// </summary>
    public short Pitch { get; init; }

}

/// <summary>
/// A death the room owes the client a kill-feed record for.
/// </summary>
/// <param name="KillerSlot">Slot the kill is credited to.</param>
/// <param name="VictimSlot">Slot that went down.</param>
/// <param name="Headshot">Whether the blow that ended it was a headshot.</param>
public sealed record BotKill(byte KillerSlot, byte VictimSlot, bool Headshot = false);
