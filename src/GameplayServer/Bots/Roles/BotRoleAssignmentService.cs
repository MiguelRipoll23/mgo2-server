namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Decides which role a new bot plays.
/// </summary>
/// <remarks>
/// <b>Every bot takes a random kind from the catalog.</b> The choice is uniform
/// over <see cref="BotRoleCatalogUtils.All"/> and is made once, when the bot is
/// seated, so a room can hold any mix of the five rather than a fixed rotation.
/// The role then stays with that bot for its lifetime.
/// </remarks>
public sealed class BotRoleAssignmentService
{
    /// <summary>The role a new bot plays.</summary>
    /// <returns>One of the catalog's archetypes.</returns>
    public BotRole Choose()
    {
        var roles = BotRoleCatalogUtils.All;
        return roles[Random.Shared.Next(roles.Count)];
    }
}
