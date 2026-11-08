using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// One weapon as a bot sees it: the id the wire carries, the class the table
/// states, and the properties this server cannot state.
/// </summary>
/// <remarks>
/// <para>
/// <b>The unresolved members are null rather than zero.</b> The ELF's table has
/// numeric columns per weapon (<c>w3–w8</c>, unlabelled) and an ammo-name table
/// beside it, and none of them has been read as a rate of fire, a magazine size or
/// a range. A zero would read as "no ammunition" and a one as "one round"; null
/// says the figure is not known, and a decision that wants one has to say so.
/// </para>
/// <para>
/// <see cref="NonLethal"/> is the exception, because the table's own flag is
/// documented: bit <c>0x8000</c> marks ST KNIFE, MOSIN N, SLEEP CLAYMORE and SLEEP
/// C4 (<c>docs/WEAPONS.md</c>). It is still null for every weapon the document
/// does not name, which is most of them.
/// </para>
/// </remarks>
/// <param name="Identifier">The weapon's id, its row in the master table.</param>
/// <param name="Category">The class the table states, or unknown.</param>
/// <param name="NonLethal">Whether the weapon is non-lethal, or <c>null</c> when unstated.</param>
/// <param name="Ammunition">Magazine or ammunition capacity, or <c>null</c> when unread.</param>
/// <param name="EffectiveRangeMetres">Effective range, or <c>null</c> when unread.</param>
public sealed record BotWeaponProfile(
    WeaponIdentifier Identifier,
    BotWeaponCategory Category,
    bool? NonLethal,
    int? Ammunition,
    int? EffectiveRangeMetres)
{
    /// <summary>Whether the weapon is a firearm a bot could engage with.</summary>
    public bool IsFirearm => Category is
        BotWeaponCategory.Handgun
        or BotWeaponCategory.SubmachineGun
        or BotWeaponCategory.AssaultRifle
        or BotWeaponCategory.MachineGun
        or BotWeaponCategory.Shotgun
        or BotWeaponCategory.SniperRifle;
}
