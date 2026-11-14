using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The weapons a bot of each role carries, chosen from the item table the wire
/// uses.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ids are the replay console's <c>ITEM_NAMES</c> rows</b>
/// (<c>.gametools/replay-console/rpdt.js</c>), which
/// <see cref="ItemIdentifier"/> ports and which a record's weapon byte names.
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
    private static readonly ItemIdentifier[] AssaultWeapons =
    [
        ItemIdentifier.M4,
        ItemIdentifier.Ak102,
        ItemIdentifier.G3a3,
        ItemIdentifier.Fal,
        ItemIdentifier.Mk17,
        ItemIdentifier.Xm8,
    ];

    private static readonly ItemIdentifier[] SupportWeapons =
    [
        ItemIdentifier.M4,
        ItemIdentifier.Lmg,
        ItemIdentifier.Hk21e,
        ItemIdentifier.Pk,
        ItemIdentifier.M60e4,
    ];

    private static readonly ItemIdentifier[] SniperWeapons =
    [
        ItemIdentifier.Vss,
        ItemIdentifier.M82a2,
        ItemIdentifier.Dsr1,
        ItemIdentifier.M14ebr,
        ItemIdentifier.MosinN,
        ItemIdentifier.Dragnov,
    ];

    private static readonly ItemIdentifier[] ReconWeapons =
    [
        ItemIdentifier.Mp7,
        ItemIdentifier.Mp5,
        ItemIdentifier.Ingram,
        ItemIdentifier.P90,
        ItemIdentifier.Bizon,
        ItemIdentifier.Patriot,
        ItemIdentifier.Skorpion,
    ];

    private static readonly ItemIdentifier[] InfiltratorWeapons =
    [
        ItemIdentifier.StKnife,
        ItemIdentifier.Ruger,
        ItemIdentifier.AkKnife,
    ];

    /// <summary>The weapons a role may carry.</summary>
    /// <param name="role">The role the bot plays.</param>
    /// <returns>The item ids its weapons are chosen from.</returns>
    public static IReadOnlyList<ItemIdentifier> WeaponsFor(BotRole role) => role switch
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
    /// <returns>The item id the bot carries.</returns>
    public static ItemIdentifier Choose(BotRole role)
    {
        var weapons = WeaponsFor(role);
        return weapons[Random.Shared.Next(weapons.Count)];
    }
}
