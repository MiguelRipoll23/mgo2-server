using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Chooses the placements a round's rule plays on, from the families the stage data
/// holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each mode's spawns are its own family, and the match says which mode it
/// is.</b> A rescue round starts its players on the rescue placements, a sneaking
/// round on the sneaking set, a base round on the base and gate markers, a bomb
/// round on the bomb set. The families come from the stages' own names, so this is
/// a mapping from the room's rule to a family the stage states, not a guess about
/// where a mode spawns.
/// </para>
/// <para>
/// <b>Nothing is refused when a stage states only one family.</b> A stage whose
/// round's family is missing falls back to its team deathmatch set and then to its
/// deathmatch set, which every stage read for this build states; a stage that
/// states none of those leaves the bot with no destination and a sentence saying
/// so, rather than a point no file stated.
/// </para>
/// </remarks>
public static class StageSpawnRuleUtils
{
    /// <summary>The family a round's rule is played on.</summary>
    /// <param name="rule">The round's rule.</param>
    /// <returns>The family.</returns>
    public static StageSpawnFamily FamilyFor(int rule) => rule switch
    {
        MatchRuleUtils.Rescue => StageSpawnFamily.Rescue,
        MatchRuleUtils.Capture => StageSpawnFamily.Capture,
        MatchRuleUtils.Sneaking or MatchRuleUtils.TeamSneaking => StageSpawnFamily.Sneaking,
        MatchRuleUtils.Base => StageSpawnFamily.Base,
        MatchRuleUtils.Bomb => StageSpawnFamily.Bomb,
        MatchRuleUtils.Deathmatch => StageSpawnFamily.Deathmatch,
        _ => StageSpawnFamily.TeamDeathmatch,
    };

    /// <summary>
    /// The placements a round's rule plays on, falling back as far as the stage
    /// allows.
    /// </summary>
    /// <param name="map">The map's navigation data.</param>
    /// <param name="rule">The round's rule.</param>
    /// <returns>The placements, empty when the stage states none.</returns>
    public static IReadOnlyList<BotPosition> Select(StageNavigationMap map, int rule)
    {
        ArgumentNullException.ThrowIfNull(map);

        var family = FamilyFor(rule);
        var placements = map.Placements(family);
        if (placements.Count > 0)
        {
            return placements;
        }

        placements = map.Placements(StageSpawnFamily.TeamDeathmatch);
        if (placements.Count > 0)
        {
            return placements;
        }

        placements = map.Placements(StageSpawnFamily.Deathmatch);
        return placements.Count > 0 ? placements : map.Placements(StageSpawnFamily.Other);
    }
}
