using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>What one team was paid for a match.</summary>
/// <param name="TeamIdentifier">Team that was paid.</param>
/// <param name="Reward">Amount paid per member.</param>
/// <param name="Streak">Streak the team carries afterwards.</param>
/// <param name="IsParticipation">Whether the payment was for taking part.</param>
public readonly record struct EventPayout(
    int TeamIdentifier,
    int Reward,
    int Streak,
    bool IsParticipation);

/// <summary>
/// Pays a finished match and moves the two teams' streaks. The payment is a
/// ledger row per character under a unique guard, so a replayed completion pays
/// nothing twice — which is what makes a result that arrives from two sources
/// harmless rather than double-paying.
/// <para>
/// The streak the confirmation reply advertises comes from the team row this
/// service writes, so the client is never told a streak the ledger disagrees
/// with.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="options">Event configuration carrying the reward table.</param>
public sealed class EventRewardService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    IOptions<EventOptions> options)
    : DomainService(contextFactory)
{
    /// <summary>Pays both teams of a completed match.</summary>
    /// <param name="matchIdentifier">Match that finished.</param>
    /// <param name="winningTeamIdentifier">Team that won, or zero for a draw.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>What each team was paid.</returns>
    public async Task<List<EventPayout>> PayAsync(
        int matchIdentifier,
        int winningTeamIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
            .FirstOrDefaultAsync(candidate => candidate.Identifier == matchIdentifier, cancellationToken);
        if (match is null)
        {
            return [];
        }

        var teams = await context.EventTeams
            .Include(team => team.Members)
            .Where(team => team.Identifier == match.FirstTeamIdentifier
                || team.Identifier == match.SecondTeamIdentifier)
            .ToListAsync(cancellationToken);

        var payouts = new List<EventPayout>();
        var now = DateTimeOffset.UtcNow;
        var table = options.Value.WinRewardTable();

        foreach (var team in teams)
        {
            var won = winningTeamIdentifier != 0 && team.Identifier == winningTeamIdentifier;
            var newStreak = EventRewardUtils.StreakAfter(team.ConsecutiveWins, won);
            var reward = winningTeamIdentifier == 0
                ? EventRewardUtils.ParticipationReward(options.Value)
                : won
                    ? EventRewardUtils.WinReward(table, newStreak)
                    : EventRewardUtils.ParticipationReward(options.Value);

            team.ConsecutiveWins = newStreak;
            team.PaidReward = Math.Max(0, team.PaidReward) + reward;
            team.UpdatedAt = now;

            foreach (var member in team.Members)
            {
                context.EventRoundRewards.Add(new EventRoundReward
                {
                    MatchIdentifier = matchIdentifier,
                    CharacterIdentifier = member.CharacterIdentifier,
                    TeamIdentifier = team.Identifier,
                    Reward = reward,
                    IsParticipation = winningTeamIdentifier == 0 || !won,
                    CreatedAt = now,
                });
            }

            payouts.Add(new EventPayout(team.Identifier, reward, newStreak, winningTeamIdentifier == 0 || !won));
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique guard on (match, character) refused a second payment,
            // which means this completion was already paid. The teams were not
            // updated in the same transaction, so read the recorded streaks back
            // rather than leaving the two disagreeing.
            return await ReadRecordedPayoutsAsync(matchIdentifier, cancellationToken);
        }

        return payouts;
    }

    /// <summary>Lists the payments already recorded for a match.</summary>
    /// <param name="matchIdentifier">Match to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<EventRoundReward>> ListPaymentsAsync(
        int matchIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventRoundRewards
            .Where(reward => reward.MatchIdentifier == matchIdentifier)
            .OrderBy(reward => reward.Identifier)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<EventPayout>> ReadRecordedPayoutsAsync(
        int matchIdentifier,
        CancellationToken cancellationToken)
    {
        var recorded = await ListPaymentsAsync(matchIdentifier, cancellationToken);
        return [.. recorded
            .GroupBy(reward => reward.TeamIdentifier)
            .Select(group => new EventPayout(
                group.Key,
                group.First().Reward,
                StreakFor(group.Key, recorded),
                group.All(reward => reward.IsParticipation)))];
    }

    private int StreakFor(int teamIdentifier, List<EventRoundReward> recorded)
    {
        // The ledger records payments rather than streaks, so the streak is
        // whatever the paid rows imply: consecutive win payments at the end.
        var streak = 0;
        foreach (var reward in recorded.Where(row => row.TeamIdentifier == teamIdentifier))
        {
            streak = reward.IsParticipation ? 0 : streak + 1;
        }

        return streak;
    }
}
