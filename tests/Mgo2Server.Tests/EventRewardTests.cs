using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the reward rules. A streak index decides which row of the table is
/// paid, so an off-by-one pays the wrong amount and nothing about the packet
/// would show it.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventRewardTests
{
    private static readonly int[] Table = [100, 200, 300, 400, 500, 0, 0, 0, 0, 0];

    [Fact]
    public void The_first_win_pays_the_first_row()
    {
        Assert.Equal(100, EventRewardUtils.WinReward(Table, streak: 1));
        Assert.Equal(500, EventRewardUtils.WinReward(Table, streak: 5));
    }

    [Fact]
    public void A_streak_beyond_the_configured_rows_keeps_paying_the_top_reward()
    {
        // The unfilled slots are zero because the table is a fixed length, not
        // because a sixth win should earn nothing: dropping to zero would punish
        // a team for winning again.
        Assert.Equal(500, EventRewardUtils.WinReward(Table, streak: 6));
        Assert.Equal(500, EventRewardUtils.WinReward(Table, streak: 500));
    }

    [Fact]
    public void A_table_that_states_only_two_streaks_keeps_paying_the_second()
    {
        var shortTable = new[] { 100, 200, 0, 0, 0 };

        Assert.Equal(100, EventRewardUtils.WinReward(shortTable, streak: 1));
        Assert.Equal(200, EventRewardUtils.WinReward(shortTable, streak: 2));
        Assert.Equal(200, EventRewardUtils.WinReward(shortTable, streak: 3));
    }

    [Fact]
    public void A_table_of_zeroes_pays_nothing()
    {
        Assert.Equal(0, EventRewardUtils.WinReward([0, 0, 0], streak: 1));
    }

    [Fact]
    public void A_streak_below_one_earns_nothing()
    {
        Assert.Equal(0, EventRewardUtils.WinReward(Table, streak: 0));
        Assert.Equal(0, EventRewardUtils.WinReward(Table, streak: -3));
    }

    [Fact]
    public void An_empty_table_pays_nothing_rather_than_failing()
    {
        Assert.Equal(0, EventRewardUtils.WinReward([], streak: 1));
    }

    [Fact]
    public void A_win_extends_the_streak_and_a_loss_ends_it()
    {
        Assert.Equal(1, EventRewardUtils.StreakAfter(0, won: true));
        Assert.Equal(4, EventRewardUtils.StreakAfter(3, won: true));
        Assert.Equal(0, EventRewardUtils.StreakAfter(7, won: false));
    }

    [Fact]
    public void The_participation_reward_is_what_a_loss_pays()
    {
        var options = new EventOptions { ParticipationReward = 25 };

        Assert.Equal(25, EventRewardUtils.ParticipationReward(options));
    }

    [Fact]
    public void A_negative_participation_reward_configuration_is_refused()
    {
        var options = new EventOptions { ParticipationReward = -1 };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void The_configured_table_fills_missing_slots_with_zero()
    {
        var options = new EventOptions { WinRewards = "100,200" };
        var table = options.WinRewardTable();

        Assert.Equal(EventOptions.WinRewardSlots, table.Length);
        Assert.Equal(100, table[0]);
        Assert.Equal(200, table[1]);
        Assert.Equal(0, table[2]);
    }
}
