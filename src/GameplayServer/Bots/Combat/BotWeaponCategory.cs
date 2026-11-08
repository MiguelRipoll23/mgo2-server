namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The kind of weapon a bot is holding, as far as this server can state one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The categories are the ELF table's own, plus the task's groupings.</b>
/// <c>docs/WEAPONS.md</c> reads the master table's category bitmask as
/// <c>1</c> knife, <c>2</c> pistol, <c>4</c> SMG, <c>8</c> AR, <c>0x10</c> LMG,
/// <c>0x20</c> shotgun, <c>0x40</c> sniper, <c>0x80</c> grenade launcher,
/// <c>0x100</c> launcher, <c>0x200</c> grenade, <c>0x400</c> placeable and
/// <c>0x800</c> attachment, with <c>0x1000</c> shield. Those become the firearm
/// and equipment members below rather than a second taxonomy.
/// </para>
/// <para>
/// <b><see cref="Unknown"/> is where most ids sit today, and that is the honest
/// number.</b> The per-id bitmask lives in the ELF's table at
/// <c>0x1035834</c>, which is not in this repository, so only the ids the
/// documents state a class for are classified
/// (<see cref="BotWeaponClassifierUtils"/>). A category invented from a weapon's
/// display name would be a guess wearing a table's clothes.
/// </para>
/// </remarks>
public enum BotWeaponCategory
{
    /// <summary>No class is stated for this weapon.</summary>
    Unknown,

    /// <summary>A sidearm, the table's pistol bit.</summary>
    Handgun,

    /// <summary>A submachine gun, the table's SMG bit.</summary>
    SubmachineGun,

    /// <summary>An assault rifle, the table's AR bit.</summary>
    AssaultRifle,

    /// <summary>A light machine gun, the table's LMG bit.</summary>
    MachineGun,

    /// <summary>A shotgun, the table's shotgun bit.</summary>
    Shotgun,

    /// <summary>A sniper rifle, the table's sniper bit.</summary>
    SniperRifle,

    /// <summary>A grenade launcher, the table's grenade-launcher bit.</summary>
    GrenadeLauncher,

    /// <summary>A launcher, the table's launcher bit.</summary>
    Launcher,

    /// <summary>A thrown grenade, the table's grenade bit.</summary>
    Grenade,

    /// <summary>A placed item, the table's placeable bit.</summary>
    Placeable,

    /// <summary>A weapon attachment, the table's attachment bit.</summary>
    Attachment,

    /// <summary>A shield, the table's shield bit.</summary>
    Shield,

    /// <summary>A blade or a blow: the table's knife bit and the melee ids.</summary>
    Melee,

    /// <summary>Explosive ordnance a bot throws or places.</summary>
    Explosives,

    /// <summary>Equipment that helps the team rather than hurting the other side.</summary>
    SupportEquipment,

    /// <summary>A trap: a placed device that waits for a player.</summary>
    Traps,

    /// <summary>Equipment the game groups on its own.</summary>
    SpecialEquipment,
}
