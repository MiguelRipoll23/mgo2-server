using Mgo2Server.Shared.Domain.Characters;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the level table against the client's own. The table lives on the disc
/// rather than in the binary, and the readings below are the live ones that pinned
/// it: a table that disagrees with any of them moves a level on screen, which is a
/// rank, a lobby's beginner ceiling and the automatch band all at once.
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
}
