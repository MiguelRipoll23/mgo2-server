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
/// <param name="Bearing">Bearing the shot came from, as the recorded word.</param>
public sealed record BotShot(
    byte VictimSlot,
    int Damage,
    byte Weapon,
    float ImpactX,
    float ImpactY,
    float ImpactZ,
    ushort Bearing);

/// <summary>
/// A death the room owes the client a kill-feed record for.
/// </summary>
/// <param name="KillerSlot">Slot the kill is credited to.</param>
/// <param name="VictimSlot">Slot that went down.</param>
public sealed record BotKill(byte KillerSlot, byte VictimSlot);
