using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// One bot as the room holds it: the identity it was given, the slots it fills,
/// the side it stands on and the role it plays.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the room's record of a bot and not the bot's brain.</b> The roster
/// owns the name and the slots (a bot is a member like any other, filed under
/// <see cref="BotRosterKeyUtils"/>), and this object is what the population keeps
/// beside that member so a decision can be taken for it: which side it holds,
/// which role it plays, which weapon it carries and which preferences it was
/// created with.
/// </para>
/// <para>
/// <see cref="Team"/> is <c>null</c> while no side has been resolved for the
/// bot, which is a real state and not a default: a match whose players are all
/// on one side leaves <c>ENEMY</c> with no side to place a bot on, and the bot is
/// then in the room on no side rather than on a side this server invented.
/// </para>
/// </remarks>
/// <param name="Identity">The name the bot holds for its whole lifetime.</param>
/// <param name="SessionSlot">Key in the room's 24-entry player table, 1 to 23.</param>
/// <param name="RosterIndex">Slot the bot fills in the roster run, counted from zero.</param>
/// <param name="Team">Side the bot stands on, or <c>null</c> while none is resolved.</param>
/// <param name="Role">Tactical archetype the bot plays.</param>
/// <param name="Weapon">Weapon id it carries for that role.</param>
/// <param name="Configuration">The bot's own preferences.</param>
public sealed record BotParticipant(
    BotIdentity Identity,
    byte SessionSlot,
    sbyte RosterIndex,
    short? Team,
    BotRole Role,
    WeaponIdentifier Weapon,
    BotConfiguration Configuration)
{
    /// <summary>The bot's character name, which is its identity's name.</summary>
    public string Name => Identity.Name;

    /// <summary>The roster key the bot's member is filed under.</summary>
    public string RosterKey => BotRosterKeyUtils.KeyFor(Identity.Name);

    /// <summary>The same participant on another side, or on none.</summary>
    /// <param name="team">Side to stand on, or <c>null</c>.</param>
    /// <returns>The participant with the side restated.</returns>
    public BotParticipant OnTeam(short? team) => this with { Team = team };
}
