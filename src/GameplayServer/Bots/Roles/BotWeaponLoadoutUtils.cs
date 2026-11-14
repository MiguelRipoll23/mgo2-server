using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The weapons a bot of each role carries, chosen from the ELF's weapon table.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ids are the ELF's weapon master table</b> (<see cref="WeaponIdentifier"/>,
/// <c>docs/WEAPONS.md</c>), which is the table the client loads a carried weapon's
/// model from. The replay console's item table is not interchangeable: the two
/// agree for firearms but not for the knives — its <c>AK KNIFE</c> is item <c>89</c>
/// and weapon <c>96</c>, and item <c>89</c> is the attachment row <c>LIGHT</c>. A bot
/// announced with item ids therefore asked the client for a weapon model that is
/// not a weapon, which is the null model header that crashed it.
/// </para>
/// <para>
/// The sets follow each role's weapon categories: assault rifles for
/// <see cref="BotRole.Assault"/>, machine guns for <see cref="BotRole.Support"/>,
/// sniper rifles for <see cref="BotRole.Sniper"/>, submachine guns for
/// <see cref="BotRole.Recon"/> and sidearms for
/// <see cref="BotRole.Infiltrator"/>.
/// </para>
/// <para>
/// <b>The infiltrator carries the non-lethal tools its archetype uses.</b> The
/// ST KNIFE and the Mk.2 (RUGER) are its stun weapons, and the AK KNIFE is its
/// lethal option; the bot's hit builder writes <c>0x91</c> for a blade and
/// <c>0x41</c>/<c>0x42</c> for the Mk.2 rather than an ordinary bullet. The role
/// is close-range by its range and actions, which is what makes those tools fit.
/// </para>
/// </remarks>
public static class BotWeaponLoadoutUtils
{
    private static readonly WeaponIdentifier[] AssaultWeapons =
    [
        WeaponIdentifier.M4,
        WeaponIdentifier.AK102,
        WeaponIdentifier.G3A3,
        WeaponIdentifier.Fal,
        WeaponIdentifier.MK17,
        WeaponIdentifier.XM8,
    ];

    private static readonly WeaponIdentifier[] SupportWeapons =
    [
        WeaponIdentifier.M4,
        WeaponIdentifier.Lmg,
        WeaponIdentifier.HK21E,
        WeaponIdentifier.Pk,
        WeaponIdentifier.M60E4,
    ];

    private static readonly WeaponIdentifier[] SniperWeapons =
    [
        WeaponIdentifier.Vss,
        WeaponIdentifier.M82A2,
        WeaponIdentifier.DSR1,
        WeaponIdentifier.M14EBR,
        WeaponIdentifier.MosinN,
        WeaponIdentifier.Dragnov,
    ];

    private static readonly WeaponIdentifier[] ReconWeapons =
    [
        WeaponIdentifier.MP7,
        WeaponIdentifier.MP5,
        WeaponIdentifier.Ingram,
        WeaponIdentifier.P90,
        WeaponIdentifier.Bizon,
        WeaponIdentifier.Patriot,
        WeaponIdentifier.Skorpion,
    ];

    private static readonly WeaponIdentifier[] InfiltratorWeapons =
    [
        WeaponIdentifier.StKnife,
        WeaponIdentifier.Ruger,
        WeaponIdentifier.AkKnife,
    ];

    /// <summary>The weapons a role may carry.</summary>
    /// <param name="role">The role the bot plays.</param>
    /// <returns>The weapon ids its weapons are chosen from.</returns>
    public static IReadOnlyList<WeaponIdentifier> WeaponsFor(BotRole role) => role switch
    {
        BotRole.Assault => AssaultWeapons,
        BotRole.Support => SupportWeapons,
        BotRole.Sniper => SniperWeapons,
        BotRole.Recon => ReconWeapons,
        BotRole.Infiltrator => InfiltratorWeapons,
        _ => throw new ArgumentOutOfRangeException(
            nameof(role),
            role,
            "The role has no weapon set."),
    };

    /// <summary>Chooses one of a role's weapons for a bot.</summary>
    /// <param name="role">The role the bot plays.</param>
    /// <returns>The weapon id the bot carries.</returns>
    public static WeaponIdentifier Choose(BotRole role)
    {
        var weapons = WeaponsFor(role);
        return weapons[Random.Shared.Next(weapons.Count)];
    }
}
