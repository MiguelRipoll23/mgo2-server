namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// The level a character displays, derived from experience exactly as the client
/// derives it.
/// <para>
/// The table is not in <c>MGO2.ELF</c>. The client walks a 128-entry array in
/// <c>.bss</c> at <c>0x1659D24</c> that is zero at load and filled at runtime by a
/// GCX native from a stage script; the numbers below are that script's argument
/// list, read byte-identically from six stages. The client's walk counts the
/// entries at or below the experience and returns the count, so a level is the
/// number of thresholds cleared and the table's length is the cap: level 22 at
/// 4,600 experience, everything above it still 22.
/// </para>
/// <para>
/// Twenty-two entries is why the automatch panel's gauge is 23 columns wide — levels
/// zero through twenty-two — and why the client clamps its own centre column to that
/// range at <c>0x93C348</c>.
/// </para>
/// </summary>
public static class LevelUtils
{
    /// <summary>
    /// Experience at which each level is reached, ascending. Level <i>n</i> is
    /// reached at <c>Thresholds[n - 1]</c>.
    /// </summary>
    private static readonly int[] Thresholds =
    [
        125, 250, 375, 500, 650, 800, 950, 1100, 1275, 1450,
        1625, 1800, 2000, 2200, 2400, 2600, 2850, 3100, 3350, 3600,
        4100, 4600,
    ];

    /// <summary>
    /// Highest level the table defines, and the client's own display clamp. Zero is a
    /// real level rather than an error: it is what everyone below 125 experience shows.
    /// </summary>
    public const int MaximumLevel = 22;

    /// <summary>Derives the level shown for an experience total.</summary>
    /// <param name="experience">Experience of the character.</param>
    public static int CalculateLevel(int experience)
    {
        var level = 0;
        while (level < Thresholds.Length && Thresholds[level] <= experience)
        {
            level++;
        }

        return level;
    }

    /// <summary>
    /// First experience total that displays as a level, which is the entry that level
    /// is reached at rather than one above it: the level is the count of entries at or
    /// below the total, so <c>CalculateLevel(ExperienceAtLevel(n))</c> is <i>n</i>.
    /// <para>
    /// The client carries the same inverse, which clamps a level past the table to its
    /// last entry instead of refusing it — level 0 is below the first threshold rather
    /// than a missing entry — and this clamps the same way.
    /// </para>
    /// </summary>
    /// <param name="level">Level to find the first total of.</param>
    public static int ExperienceAtLevel(int level) => level <= 0
        ? 0
        : Thresholds[Math.Min(level, Thresholds.Length) - 1];
}
