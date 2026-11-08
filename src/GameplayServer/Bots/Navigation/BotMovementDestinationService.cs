namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Chooses where a bot walks to, from the placements this build extracted for the
/// map and the round's mode.
/// </summary>
/// <remarks>
/// <para>
/// <b>A destination is a placement a stage states, extracted and shipped.</b> The
/// stages name their spawns per mode — <c>PRP_RES_TEAM_DEATHMATCH_A01</c> and its
/// siblings — so the round's rule picks a family and the bot takes one of that
/// family's points by its slot. The room's bots spread over the placements the map
/// offers and the same bot keeps the same one while it lives.
/// </para>
/// <para>
/// A map this build has no data for, or a stage that states none of the round's
/// placements, leaves the bot with no destination and a sentence saying so. That is
/// the honest half of the seam: the provider can route what it is asked, and
/// nothing is asked that no stage stated.
/// </para>
/// </remarks>
/// <param name="catalog">The build's stage navigation data.</param>
public sealed class BotMovementDestinationService(StageNavigationCatalogService catalog)
{
    /// <summary>
    /// Where a bot in a slot would move to on a map.
    /// </summary>
    /// <param name="map">The round's map, in the replay decoder's numbering.</param>
    /// <param name="sessionSlot">The slot the bot holds, which picks its placement.</param>
    /// <param name="rule">The round's rule, which picks the placement family.</param>
    /// <returns>A placement the stage states, or no position and why.</returns>
    public BotMovementDestination Choose(int map, byte sessionSlot, int rule)
    {
        if (!catalog.TryRead(map, out var data, out var reason))
        {
            return new BotMovementDestination(null, reason);
        }

        var placements = StageSpawnRuleUtils.Select(data, rule);
        if (placements.Count == 0)
        {
            return new BotMovementDestination(
                null,
                $"Stage {data.Stage} states no placement for rule {rule}, so no destination is derived.");
        }

        var index = sessionSlot % placements.Count;
        return new BotMovementDestination(
            placements[index],
            $"Slot {sessionSlot} takes placement {index} of the {placements.Count} stage {data.Stage} "
            + $"states for rule {rule}.");
    }
}
