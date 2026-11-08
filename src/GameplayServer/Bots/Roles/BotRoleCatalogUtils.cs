namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The five archetypes as design data, and the weight each one puts on an action.
/// </summary>
/// <remarks>
/// <para>
/// <b>One weight per (role, action) pair, and every pair has one.</b> The table is
/// exhaustive by construction — a pair not listed reads as neutral — so adding an
/// action cannot silently give one role an advantage over another.
/// </para>
/// <para>
/// The preferred weapon categories name kinds rather than ids: which weapons this
/// server has evidence for is <see cref="BotWeaponCatalogService"/>'s question,
/// and a role that prefers a category with no verified weapon resolves to no
/// preference rather than to a guessed id.
/// </para>
/// </remarks>
public static class BotRoleCatalogUtils
{
    private static readonly BotRoleDefinition[] Definitions =
    [
        new(
            BotRole.Assault,
            "Aggressive frontline combat and objective pressure.",
            BotEngagementRange.Medium,
            [BotWeaponCategory.AssaultRifle, BotWeaponCategory.SubmachineGun],
            BotAction.Advance,
            BotAction.Engage),
        new(
            BotRole.Support,
            "Team assistance, covering fire and defensive support.",
            BotEngagementRange.Medium,
            [BotWeaponCategory.MachineGun, BotWeaponCategory.AssaultRifle],
            BotAction.Support,
            BotAction.Hold),
        new(
            BotRole.Sniper,
            "Long-range engagements and overwatch.",
            BotEngagementRange.Long,
            [BotWeaponCategory.SniperRifle],
            BotAction.Engage,
            BotAction.Hold),
        new(
            BotRole.Recon,
            "Scouting, enemy detection and information gathering.",
            BotEngagementRange.Medium,
            [BotWeaponCategory.SubmachineGun, BotWeaponCategory.Handgun],
            BotAction.Patrol,
            BotAction.Advance),
        new(
            BotRole.Infiltrator,
            "Stealth, flanking and close-range engagements.",
            BotEngagementRange.Close,
            [BotWeaponCategory.Handgun, BotWeaponCategory.Melee],
            BotAction.Advance,
            BotAction.CaptureObjective),
    ];

    /// <summary>Every role, in the order the catalog lists them.</summary>
    public static IReadOnlyList<BotRole> All { get; } = [.. Definitions.Select(entry => entry.Role)];

    /// <summary>
    /// The design of a role.
    /// </summary>
    /// <param name="role">The archetype.</param>
    /// <returns>Its description, range, weapons and leanings.</returns>
    public static BotRoleDefinition Definition(BotRole role) =>
        Definitions.First(entry => entry.Role == role);

    /// <summary>
    /// How much a role favours an action: one is neutral, above favours, below
    /// discourages.
    /// </summary>
    /// <param name="role">The archetype.</param>
    /// <param name="action">The action being scored.</param>
    /// <returns>The role's weight for the action.</returns>
    public static double Weight(BotRole role, BotAction action)
    {
        var definition = Definition(role);
        if (definition.PrimaryAction == action)
        {
            return 1.15;
        }

        if (definition.SecondaryAction == action)
        {
            return 1.05;
        }

        // Covering fire is a support role's second action in spirit even when its
        // stated pair is elsewhere, so a support leans towards it wherever it is
        // proposed rather than only when its own objective offers it.
        return role == BotRole.Support && action == BotAction.Support ? 1.2 : 1.0;
    }
}
