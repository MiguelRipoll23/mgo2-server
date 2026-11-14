using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// How a bot's carried weapon is used: an ordinary firearm, a tranquilliser, a
/// stun knife, or a lethal blade.
/// </summary>
/// <remarks>
/// <b>The id is the item table's, not the ELF's weapon table.</b> The infiltrator
/// carries <see cref="ItemIdentifier.StKnife"/> and <see cref="ItemIdentifier.Ruger"/>
/// — the ST KNIFE and the Mk.2 — and <see cref="ItemIdentifier.AkKnife"/> as its
/// lethal option. The two knife ids use the recorded <c>0x91</c> record; the
/// tranquilliser uses the recorded <c>0x41</c>/<c>0x42</c> pair.
/// </remarks>
public static class BotWeaponUseUtils
{
    /// <summary>Stamina a tranquilliser body hit spends, measured in the capture.</summary>
    public const int TranquilliserBodyPoints = 36;

    /// <summary>What a carried weapon does when it lands.</summary>
    /// <param name="weapon">The item id the bot carries.</param>
    /// <returns>The kind of blow its hit writes.</returns>
    public static BotWeaponKind KindOf(ItemIdentifier weapon) => weapon switch
    {
        ItemIdentifier.StKnife => BotWeaponKind.StunKnife,
        ItemIdentifier.Ruger => BotWeaponKind.StunDart,
        ItemIdentifier.AkKnife => BotWeaponKind.LethalKnife,
        _ => BotWeaponKind.Firearm,
    };

    /// <summary>Whether a hit of this kind can be a headshot.</summary>
    /// <param name="kind">The blow.</param>
    /// <returns><c>true</c> for a firearm or a tranquilliser.</returns>
    public static bool CanHeadshot(BotWeaponKind kind) =>
        kind is BotWeaponKind.Firearm or BotWeaponKind.StunDart;

    /// <summary>Whether an emptied stun bar sleeps the victim.</summary>
    /// <param name="kind">The blow.</param>
    /// <returns><c>true</c> for a tranquilliser.</returns>
    public static bool Sleeps(BotWeaponKind kind) => kind is BotWeaponKind.StunDart;

    /// <summary>Whether an emptied stun bar shows the stun pose.</summary>
    /// <param name="kind">The blow.</param>
    /// <returns><c>true</c> for a knife.</returns>
    public static bool ShowsStunPose(BotWeaponKind kind) => kind is BotWeaponKind.StunKnife;

    /// <summary>The health or stamina points a blow carries.</summary>
    /// <param name="kind">The blow.</param>
    /// <param name="headshot">Whether it is a headshot.</param>
    /// <returns>The points the damage word carries.</returns>
    public static int DamageOf(BotWeaponKind kind, bool headshot) => kind switch
    {
        BotWeaponKind.StunDart => headshot
            ? BotBodyState.MaximumVitals
            : TranquilliserBodyPoints,
        BotWeaponKind.StunKnife or BotWeaponKind.LethalKnife => BotBodyState.MaximumVitals,
        _ => headshot ? BotBodyState.MaximumVitals : BotShotUtils.RollDamage(),
    };
}
