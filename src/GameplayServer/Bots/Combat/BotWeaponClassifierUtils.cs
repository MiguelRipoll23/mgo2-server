using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The class and the lethality of a weapon, as far as the documents state them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Four ids are classified and everything else is unknown, deliberately.</b>
/// The per-id category bitmask lives in the ELF's master table, which is not in
/// this repository; what the documents do state is the table's category values
/// (<c>docs/WEAPONS.md</c>) and three anchored weapons — the AK102 as an assault
/// rifle and the RUGER as the Mark 2 tranquiliser pistol, both verified on the
/// live UI, and the MOSIN N as the sniper rifle that fires tranq darts. The
/// knife's row is the table's own <c>ST KNIFE</c>, which the non-lethal flag
/// names. A fifth classification would be an inference, so there is none.
/// </para>
/// <para>
/// When the table is read — it is at vaddr <c>0x1035834</c>, stride <c>0x2c</c>,
/// 141 rows — this switch is what the read replaces, and nothing else in the bot
/// system moves.
/// </para>
/// </remarks>
public static class BotWeaponClassifierUtils
{
    /// <summary>
    /// The class of a weapon, or <see cref="BotWeaponCategory.Unknown"/>.
    /// </summary>
    /// <param name="identifier">The weapon's id.</param>
    /// <returns>The class the documents state.</returns>
    public static BotWeaponCategory CategoryOf(WeaponIdentifier identifier) => identifier switch
    {
        WeaponIdentifier.StKnife => BotWeaponCategory.Melee,
        WeaponIdentifier.Ruger => BotWeaponCategory.Handgun,
        WeaponIdentifier.AK102 => BotWeaponCategory.AssaultRifle,
        WeaponIdentifier.MosinN => BotWeaponCategory.SniperRifle,
        WeaponIdentifier.None or WeaponIdentifier.Unreported => BotWeaponCategory.Unknown,
        _ => BotWeaponCategory.Unknown,
    };

    /// <summary>
    /// Whether a weapon is non-lethal, or <c>null</c> when the table's flag has
    /// not been read for it.
    /// </summary>
    /// <param name="identifier">The weapon's id.</param>
    /// <returns>The flag the table states, or <c>null</c>.</returns>
    public static bool? NonLethalOf(WeaponIdentifier identifier) => identifier switch
    {
        WeaponIdentifier.StKnife
            or WeaponIdentifier.MosinN
            or WeaponIdentifier.SleepClaymore
            or WeaponIdentifier.SleepC4 => true,
        _ => null,
    };

    /// <summary>
    /// Every id whose class is stated by a document, which is the set a role's
    /// preference can resolve against today.
    /// </summary>
    public static IReadOnlyList<WeaponIdentifier> Classified { get; } =
    [
        WeaponIdentifier.StKnife,
        WeaponIdentifier.Ruger,
        WeaponIdentifier.AK102,
        WeaponIdentifier.MosinN,
    ];
}
