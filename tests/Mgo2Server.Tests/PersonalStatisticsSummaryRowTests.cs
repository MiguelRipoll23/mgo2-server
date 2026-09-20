using Mgo2Server.GameLobbyServer.Commands.Game.Characters;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the summary row of the cumulative statistics matrix. The eighth wire block
/// is not a game mode: it is the row the player-details card reads, and its column
/// 13 is the client's total-rewards cell, so a value taken from anywhere else is
/// rendered as the player's rewards.
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class PersonalStatisticsSummaryRowTests
{
    /// <summary>Number of columns in a matrix row.</summary>
    private const int StatColumns = 18;

    /// <summary>Wire offset the summary row starts at: the two header words and seven full rows.</summary>
    private const int SummaryRowOffset = 8 + 7 * StatColumns * 4;

    /// <summary>Summary column carrying the total rewards.</summary>
    private const int TotalRewardsColumn = 13;

    /// <summary>Summary column carrying the play time.</summary>
    private const int PlaySecondsColumn = 17;

    /// <summary>Wire offset of the total-rewards cell.</summary>
    private const int TotalRewardsOffset = SummaryRowOffset + TotalRewardsColumn * 4;

    /// <summary>Full size of a matrix payload.</summary>
    private const int MatrixSize = 8 + 8 * StatColumns * 4;

    [Fact]
    public void The_summary_row_carries_the_characters_total_rewards()
    {
        var character = new Character
        {
            Identifier = 4,
            Name = "Someone",
            Experience = 4000,
            TotalRewards = 1234,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1),
        };

        var payload = PersonalStatisticsPayloadBuilder.BuildMatrix(null, 0, character);

        Assert.Equal(MatrixSize, payload.Length);
        Assert.Equal(1234u, BinaryUtility.ReadUInt32BigEndian(payload, TotalRewardsOffset));
    }

    /// <summary>
    /// The level is derived by the client from the experience the header carries,
    /// so the cell must not be spent on it — a level there reads as a reward total.
    /// </summary>
    [Fact]
    public void The_summary_row_does_not_carry_the_level()
    {
        var character = new Character
        {
            Identifier = 5,
            Name = "Someone",
            Experience = 4000,
            TotalRewards = 7,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1),
        };

        var payload = PersonalStatisticsPayloadBuilder.BuildMatrix(null, 0, character);

        // The character's level is 20, so a cell still holding the level would
        // read 20 rather than the total rewards of 7.
        Assert.Equal(20, LevelUtils.CalculateLevel(character.Experience));
        Assert.Equal(7u, BinaryUtility.ReadUInt32BigEndian(payload, TotalRewardsOffset));
    }

    [Fact]
    public void Only_the_cumulative_page_fills_the_summary_row()
    {
        var character = new Character
        {
            Identifier = 6,
            Name = "Someone",
            Experience = 1000,
            TotalRewards = 4321,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1),
        };

        var payload = PersonalStatisticsPayloadBuilder.BuildMatrix(null, 1, character);

        Assert.Equal(0u, BinaryUtility.ReadUInt32BigEndian(payload, TotalRewardsOffset));
        Assert.Equal(0u, BinaryUtility.ReadUInt32BigEndian(payload, SummaryRowOffset + PlaySecondsColumn * 4));
    }

    [Fact]
    public void The_cumulative_page_carries_the_play_time_beside_it()
    {
        var character = new Character
        {
            Identifier = 7,
            Name = "Someone",
            Experience = 100,
            TotalRewards = 99,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1),
        };
        var statistics = new CharacterStatistics { TotalTime = 60200 };

        var payload = PersonalStatisticsPayloadBuilder.BuildMatrix(statistics, 0, character);

        Assert.Equal(60200u, BinaryUtility.ReadUInt32BigEndian(payload, SummaryRowOffset + PlaySecondsColumn * 4));
        Assert.Equal(99u, BinaryUtility.ReadUInt32BigEndian(payload, TotalRewardsOffset));
    }
}
