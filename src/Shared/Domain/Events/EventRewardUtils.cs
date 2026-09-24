using Mgo2Server.Shared.Options;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The pure reward rules: what a win is worth at a given streak, and what a
/// loss is worth. They are separated from the payout because a payout is a
/// transaction and this is a table lookup, and because an off-by-one in a streak
/// index pays the wrong row silently.
/// </summary>
public static class EventRewardUtils
{
    /// <summary>
    /// Reward for a win that takes a team to the given streak.
    /// <para>
    /// A row the operator left zero is not paid, and it does not absorb a longer
    /// streak either: the table is stateable as "the streaks I pay for", so a
    /// sixth win falls back to the highest reward that was configured rather than
    /// to a zero that was never meant to apply. Paying nothing there would punish
    /// a team for winning again.
    /// </para>
    /// </summary>
    /// <param name="rewardTable">Configured win rewards in streak order.</param>
    /// <param name="streak">Streak the win produces, counted from one.</param>
    public static int WinReward(IReadOnlyList<int> rewardTable, int streak)
    {
        ArgumentNullException.ThrowIfNull(rewardTable);
        if (streak <= 0 || rewardTable.Count == 0)
        {
            return 0;
        }

        var index = Math.Min(streak, rewardTable.Count) - 1;
        for (var candidate = index; candidate >= 0; candidate--)
        {
            if (rewardTable[candidate] > 0)
            {
                return rewardTable[candidate];
            }
        }

        return 0;
    }

    /// <summary>Reward for losing, which is the participation payment.</summary>
    /// <param name="options">Configuration carrying the participation reward.</param>
    public static int ParticipationReward(EventOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Math.Max(0, options.ParticipationReward);
    }

    /// <summary>
    /// Streak a team carries after a match. A win extends it and a loss ends it,
    /// because the reward table is about consecutive wins.
    /// </summary>
    /// <param name="currentStreak">Streak the team carried into the match.</param>
    /// <param name="won">Whether the team won.</param>
    public static int StreakAfter(int currentStreak, bool won) =>
        won ? Math.Max(0, currentStreak) + 1 : 0;
}
