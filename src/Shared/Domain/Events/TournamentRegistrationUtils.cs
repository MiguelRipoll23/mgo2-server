using Mgo2Server.Shared.Options;

namespace Mgo2Server.Shared.Domain.Events;
/// <summary>
/// The pure rules of Tournament registration: who may reserve, and which place
/// they get. They are separated from the service because they are decisions
/// rather than storage, so they can be stated once and tested directly.
/// </summary>
public static class TournamentRegistrationUtils
{
    /// <summary>
    /// Tests a character level against the configured limits. Zero means "no
    /// bound" on either side, because a limit of zero is how an operator says
    /// the event is open to everyone.
    /// </summary>
    /// <param name="level">Level to test.</param>
    /// <param name="minimumLevel">Lowest accepted level, or zero.</param>
    /// <param name="maximumLevel">Highest accepted level, or zero.</param>
    public static bool IsLevelEligible(int level, int minimumLevel, int maximumLevel) =>
        (minimumLevel == 0 || level >= minimumLevel)
        && (maximumLevel == 0 || level <= maximumLevel);

    /// <summary>Tests a level against the configured entry limits.</summary>
    /// <param name="level">Level to test.</param>
    /// <param name="options">Configuration carrying the limits.</param>
    public static bool IsLevelEligible(int level, EventOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return IsLevelEligible(level, options.TournamentMinimumLevel, options.TournamentMaximumLevel);
    }

    /// <summary>Total player places a field of the given team count holds.</summary>
    /// <param name="teamCapacity">Number of teams the field holds.</param>
    public static int PlaceCapacity(int teamCapacity)
    {
        // Six players per team is the roster the event screens advertise, so the
        // capacity is stated in teams and counted in players.
        return teamCapacity * EventConstants.TeamMemberLimit;
    }

    /// <summary>
    /// Returns the lowest unused place index. Reusing a released index keeps the
    /// occupied set contiguous, which is what lets a frozen seed order stay
    /// independent of who reserved first.
    /// </summary>
    /// <param name="usedSlots">Indexes already taken.</param>
    /// <param name="capacity">Number of places available.</param>
    public static int NextSlot(IEnumerable<int> usedSlots, int capacity)
    {
        ArgumentNullException.ThrowIfNull(usedSlots);

        var taken = new HashSet<int>(usedSlots);
        for (var slot = 0; slot < capacity; slot++)
        {
            if (!taken.Contains(slot))
            {
                return slot;
            }
        }

        return capacity;
    }
}
