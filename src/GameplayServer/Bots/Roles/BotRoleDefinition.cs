namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// One role's design: what it favours, how far it likes to fight, and the weapon
/// categories it reaches for.
/// </summary>
/// <remarks>
/// <para>
/// <b>The weights are design parameters, not measurements.</b> They say how much
/// more a role values one action than another; no capture states a bot's
/// preference, and a figure here is tunable rather than evidence. What is
/// evidence is the weapon categories, which come from the ELF's own table where
/// they are known (<see cref="BotWeaponClassifierUtils"/>).
/// </para>
/// <para>
/// A role describes leanings rather than a separate AI: every role runs the same
/// objective, perception, decision and combat systems, and differs only in the
/// weights and the weapon categories it prefers. That is what lets a bot adapt to
/// a match without five implementations of the same behaviour.
/// </para>
/// </remarks>
/// <param name="Role">The archetype described.</param>
/// <param name="Description">What the role is for, in prose.</param>
/// <param name="Range">The distance band the role prefers.</param>
/// <param name="PreferredWeapons">Weapon categories the role reaches for first.</param>
/// <param name="PrimaryAction">The action the role leans hardest towards.</param>
/// <param name="SecondaryAction">The action it leans towards next.</param>
public sealed record BotRoleDefinition(
    BotRole Role,
    string Description,
    BotEngagementRange Range,
    IReadOnlyList<BotWeaponCategory> PreferredWeapons,
    BotAction PrimaryAction,
    BotAction SecondaryAction);
