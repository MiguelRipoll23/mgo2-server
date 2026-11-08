using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The weapons a bot can reason about, and which of them a role reaches for.
/// </summary>
/// <remarks>
/// <para>
/// <b>A profile is answered for every id, and only some of it is filled.</b> The
/// id and the class come from the master table; ammunition, range and the lethal
/// flag are unread for almost every row and stay null. A caller that needs one of
/// them has to handle the absence, which is the point of stating it.
/// </para>
/// <para>
/// <b>A role's preference resolves to nothing when nothing states it.</b> Five of
/// the five archetypes prefer categories this server has no classified weapon for
/// — the sniper's category is one of the four classified, the machine gunner's is
/// not — so the answer is an empty list and a debug line, not a weapon chosen by
/// its name.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class BotWeaponCatalogService(ILogger<BotWeaponCatalogService> logger)
{
    /// <summary>
    /// Everything this server states about a weapon.
    /// </summary>
    /// <param name="identifier">The weapon's id.</param>
    /// <returns>Its class, its lethality when stated, and the unread figures as null.</returns>
    public BotWeaponProfile Profile(WeaponIdentifier identifier) => new(
        identifier,
        BotWeaponClassifierUtils.CategoryOf(identifier),
        BotWeaponClassifierUtils.NonLethalOf(identifier),
        Ammunition: null,
        EffectiveRangeMetres: null);

    /// <summary>
    /// The weapons of the classes a role prefers, among those this server has
    /// classified.
    /// </summary>
    /// <param name="role">The archetype.</param>
    /// <returns>The matching weapons, or an empty list when none is classified.</returns>
    public IReadOnlyList<WeaponIdentifier> Preferred(BotRole role)
    {
        var definition = BotRoleCatalogUtils.Definition(role);
        var resolved = BotWeaponClassifierUtils.Classified
            .Where(identifier => definition.PreferredWeapons.Contains(
                BotWeaponClassifierUtils.CategoryOf(identifier)))
            .ToList();

        if (resolved.Count == 0)
        {
            logger.LogDebug(
                "Role {Role} prefers {Categories}, and no weapon with one of those classes has been read "
                + "from the master table; the role holds no weapon preference",
                role,
                string.Join(", ", definition.PreferredWeapons));
        }

        return resolved;
    }
}
