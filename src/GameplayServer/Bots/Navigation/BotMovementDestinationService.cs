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

    /// <summary>
    /// The next point of a bot's patrol, which is what it walks to while it holds
    /// no enemy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A bot patrols the stage's own points rather than standing on one.</b>
    /// The working host's bots cross the whole map between engagements: slot 1 of
    /// <c>game-with-bots-workingserver</c> runs from <c>x 20750</c> to
    /// <c>25160</c> and <c>z -23750</c> to <c>-14960</c> in twenty-one seconds, in
    /// bursts of about two hundred units, and every slot's range is its own. A bot
    /// that walks to one placement and waits there is the room of 2026-11-10, whose
    /// bots stood where they spawned until a player came within range.
    /// </para>
    /// <para>
    /// <b>The phase is the slot's and the cursor's, so two bots do not walk the
    /// same tour.</b> The placement families the stage states are used first; a
    /// stage that states fewer than two falls back to its own cell centres, which
    /// is the lattice the router runs over. Two strides coprime with the list
    /// length keep a tour from repeating a short cycle.
    /// </para>
    /// </remarks>
    /// <param name="map">The round's map, in the replay decoder's numbering.</param>
    /// <param name="sessionSlot">The slot the bot holds, which is its phase.</param>
    /// <param name="cursor">How many patrol points it has taken so far.</param>
    /// <returns>A point to walk to, or no position and why.</returns>
    public BotMovementDestination ChoosePatrol(int map, byte sessionSlot, int cursor)
    {
        if (!catalog.TryRead(map, out var data, out var reason))
        {
            return new BotMovementDestination(null, reason);
        }

        // The track the working host's bots walked comes first, one per slot: it
        // is a path somebody actually walked, and no two slots share one. A slot
        // with no track falls back to the stage's own points below.
        if (BotPatrolRouteUtils.RouteOf(map, sessionSlot) is { Count: > 0 } track)
        {
            var point = track[(int)(((long)cursor % track.Count + track.Count) % track.Count)];
            return new BotMovementDestination(
                point,
                $"Patrol point {cursor} of slot {sessionSlot} is point {cursor % track.Count} of its "
                + $"{track.Count}-point recorded track.");
        }

        var points = data.Spawns
            .SelectMany(set => set.Placements)
            .Distinct()
            .ToList();
        if (points.Count < 2 && data.Cells.Count >= 2)
        {
            points = [.. data.Cells];
        }

        if (points.Count == 0)
        {
            return new BotMovementDestination(
                null,
                $"Stage {data.Stage} states no placement and no cell, so it has no patrol point.");
        }

        var index = (int)(((long)cursor % points.Count + points.Count) % points.Count);
        return new BotMovementDestination(
            points[index],
            $"Patrol point {cursor} of slot {sessionSlot} is point {index} of the {points.Count} "
            + $"stage {data.Stage} states.");
    }
}
