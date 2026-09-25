namespace Mgo2Server.Shared.Domain.Events;

/// <summary>One registration that has a team behind it.</summary>
/// <param name="SlotIndex">Place the team was given when it was submitted.</param>
/// <param name="TeamIdentifier">Team the place holds.</param>
public readonly record struct TournamentEntrant(int SlotIndex, int TeamIdentifier);

/// <summary>
/// The rule that turns submitted teams into a seed order.
/// <para>
/// The seed order is taken from the places the teams were given, not from when
/// they were submitted or from any draw. That is what makes the bracket
/// reproducible: the same places seed the same way every time the bracket is
/// read, which is the property a restart depends on.
/// </para>
/// </summary>
public static class TournamentSeedingUtils
{
    /// <summary>Orders the submitted teams into the frozen seed order.</summary>
    /// <param name="entrants">Submitted teams and their places.</param>
    /// <returns>Team identifiers in seed order, one per team.</returns>
    public static IReadOnlyList<int> OrderTeamSeeds(IEnumerable<TournamentEntrant> entrants)
    {
        ArgumentNullException.ThrowIfNull(entrants);

        var seeds = new List<int>();
        var seen = new HashSet<int>();
        foreach (var entrant in entrants
                     .Where(entrant => entrant.TeamIdentifier > 0)
                     .OrderBy(entrant => entrant.SlotIndex))
        {
            // A team registered twice occupies one place in the bracket: a
            // duplicate seed would otherwise pair a team with itself.
            if (seen.Add(entrant.TeamIdentifier))
            {
                seeds.Add(entrant.TeamIdentifier);
            }
        }

        return seeds;
    }

    /// <summary>
    /// Whether a set of teams can be seeded. An empty field has nothing to play
    /// and an oversized one could be seeded but never drawn.
    /// </summary>
    /// <param name="seeds">Teams in seed order.</param>
    public static bool IsSeedable(IReadOnlyCollection<int> seeds)
    {
        ArgumentNullException.ThrowIfNull(seeds);

        return seeds.Count >= 1 && seeds.Count <= EventConstants.BracketMaximumEntrants;
    }

    /// <summary>
    /// Whether a field has stopped accepting entries, which is the moment it is
    /// seeded and its first fixtures become matches.
    /// <para>
    /// Two things close a field: it being full, because nothing else could be
    /// admitted, and every team in it having committed to play — the same
    /// readiness the Survival field waits on, so one signal means the same thing
    /// in both. A field of fewer than two teams never closes, because there is
    /// nobody to play and a bracket of one would crown an entrant that never
    /// played a fixture.
    /// </para>
    /// </summary>
    /// <param name="entrantCount">Teams submitted to the field.</param>
    /// <param name="teamCapacity">Places the field holds.</param>
    /// <param name="entrantReadiness">Whether each entrant has committed to play.</param>
    public static bool EntriesClosed(
        int entrantCount,
        int teamCapacity,
        IReadOnlyCollection<bool> entrantReadiness)
    {
        ArgumentNullException.ThrowIfNull(entrantReadiness);

        if (entrantCount < EventConstants.MinimumFieldSize)
        {
            return false;
        }

        if (entrantCount >= teamCapacity)
        {
            return true;
        }

        // A readiness list shorter than the field means an entrant whose
        // readiness was never read, which is not consent to start.
        return entrantReadiness.Count == entrantCount
            && entrantReadiness.All(ready => ready);
    }
}
