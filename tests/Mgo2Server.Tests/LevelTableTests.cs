using Mgo2Server.Shared.Domain.Characters;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the level table against the client's own. The table lives on the disc
/// rather than in the binary, and the readings below are the live ones that pinned
/// it: a table that disagrees with any of them moves a level on screen, which is
/// a rank, a lobby's beginner ceiling and the automatch band all at once. It also
/// pins the table's inverse, which is where the starting experience of a new
/// character is taken from.
/// </summary>
public sealed class LevelTableTests
{
    /// <summary>Experience and the level a live client displayed for it.</summary>
    public static TheoryData<int, int> LiveReadings => new()
    {
        { 0, 0 },
        { 11, 0 },
        { 22, 0 },
        { 33, 0 },
        { 44, 0 },
        { 214, 1 },
        { 428, 3 },
        { 499, 3 },
        { 500, 4 },
        { 1450, 10 },
        { 1600, 10 },
        { 49250, 22 },
        { 50000, 22 },
    };

    [Theory]
    [MemberData(nameof(LiveReadings))]
    public void The_derived_level_matches_the_live_client(int experience, int level)
    {
        Assert.Equal(level, LevelUtils.CalculateLevel(experience));
    }

    /// <summary>
    /// The cap is the table's length rather than a separate clamp, and the last
    /// threshold is where the twenty-second level starts.
    /// </summary>
    [Fact]
    public void The_table_ends_at_the_twenty_second_level()
    {
        Assert.Equal(22, LevelUtils.MaximumLevel);
        Assert.Equal(21, LevelUtils.CalculateLevel(4599));
        Assert.Equal(LevelUtils.MaximumLevel, LevelUtils.CalculateLevel(4600));

        // Nothing above the last threshold moves, however much experience arrives.
        Assert.Equal(LevelUtils.MaximumLevel, LevelUtils.CalculateLevel(65535));
    }

    /// <summary>
    /// The inverse of the table. A level's first total is the entry it is reached at,
    /// so asking for a level and reading it back returns that level, and one point
    /// less reads as the level before — which is what makes the pair usable for a
    /// starting total rather than a near miss.
    /// </summary>
    [Fact]
    public void The_inverse_returns_the_first_total_that_reads_as_the_level()
    {
        Assert.Equal(0, LevelUtils.ExperienceAtLevel(0));
        Assert.Equal(1800, LevelUtils.ExperienceAtLevel(12));
        Assert.Equal(4600, LevelUtils.ExperienceAtLevel(LevelUtils.MaximumLevel));

        for (var level = 1; level <= LevelUtils.MaximumLevel; level++)
        {
            var threshold = LevelUtils.ExperienceAtLevel(level);
            Assert.Equal(level, LevelUtils.CalculateLevel(threshold));
            Assert.Equal(level - 1, LevelUtils.CalculateLevel(threshold - 1));
        }

        // Past the table the cap is the answer, and anything at or below zero is the
        // start, because zero is a real level rather than a missing entry.
        Assert.Equal(4600, LevelUtils.ExperienceAtLevel(LevelUtils.MaximumLevel + 1));
        Assert.Equal(0, LevelUtils.ExperienceAtLevel(-1));
    }

    /// <summary>
    /// A newly registered character starts at level 12. The total is pinned as a
    /// number rather than derived here, so an edit to the table has to move the
    /// starting level deliberately instead of moving it in passing.
    /// </summary>
    [Fact]
    public void A_new_character_starts_at_the_threshold_of_the_starting_level()
    {
        Assert.Equal(12, CharacterService.StartingLevel);
        Assert.Equal(1800, CharacterService.StartingExperience);
        Assert.Equal(
            CharacterService.StartingLevel,
            LevelUtils.CalculateLevel(CharacterService.StartingExperience));
    }
}
