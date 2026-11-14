using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The item id a bot's damage record carries, converted from the weapon id its body
/// carries.
/// </summary>
/// <remarks>
/// <b>The two tables name different things at the low ids.</b> An actor's weapon pair
/// names the carried model and uses the ELF's <see cref="WeaponIdentifier"/>; a damage
/// record names the protocol item and uses <see cref="ItemIdentifier"/>. Firearms share
/// the same id in both tables, but the knives do not — the ELF has ST KNIFE at 1,
/// RUGER at 2 and AK KNIFE at 96, while the protocol item table has them at 2, 3 and
/// 89. This is the only place a bot weapon crosses between the two.
/// </remarks>
public static class BotDamageWeaponUtils
{
    /// <summary>The item id a damage record carries for a bot's carried weapon.</summary>
    /// <param name="weapon">The weapon id the bot's body carries.</param>
    /// <returns>The matching protocol item id.</returns>
    public static byte DamageIdOf(WeaponIdentifier weapon) => weapon switch
    {
        WeaponIdentifier.StKnife => (byte)ItemIdentifier.StKnife,
        WeaponIdentifier.Ruger => (byte)ItemIdentifier.Ruger,
        WeaponIdentifier.AkKnife => (byte)ItemIdentifier.AkKnife,
        _ => (byte)weapon,
    };
}
