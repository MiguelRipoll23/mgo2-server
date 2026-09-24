using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>One team's totals, aggregated from its members' reports.</summary>
/// <param name="Complete">Whether every occupied slot of the team reported.</param>
/// <param name="RoundsWon">Rounds the team's members won, summed.</param>
/// <param name="AbortedCount">Aborted reports the team filed.</param>
/// <param name="Score">Score the team's members reported, summed.</param>
/// <param name="Kills">Kills the team's members reported, summed.</param>
/// <param name="Deaths">Deaths the team's members reported, summed.</param>
public readonly record struct EventStatsAggregate(
    bool Complete,
    int RoundsWon,
    int AbortedCount,
    int Score,
    int Kills,
    int Deaths);

/// <summary>
/// The rule that decides who won a match from what the players reported. It is
/// separated from the service because it is a comparison, not a transaction, and
/// because a tie must stay a tie: inventing a winner for a draw is how a
/// tournament advances the wrong team.
/// </summary>
public static class EventOutcomeUtils
{
    /// <summary>Sums one team's reports over its occupied roster slots.</summary>
    /// <param name="team">Team whose members reported.</param>
    /// <param name="reports">Reports, keyed by character.</param>
    public static EventStatsAggregate Aggregate(
        EventSnapshot team,
        IReadOnlyDictionary<int, EventStatReport> reports)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(reports);

        var complete = true;
        var roundsWon = 0;
        var aborted = 0;
        var score = 0;
        var kills = 0;
        var deaths = 0;

        foreach (var participant in team.Participants)
        {
            if (participant.CharacterIdentifier == 0)
            {
                // An empty slot has nothing to report and must not make the team
                // look incomplete for the rest of time.
                continue;
            }

            if (!reports.TryGetValue(participant.CharacterIdentifier, out var report))
            {
                complete = false;
                continue;
            }

            roundsWon += report.RoundsWon;
            aborted += report.Aborted ? 1 : 0;
            score += report.Score;
            kills += report.Kills;
            deaths += report.Deaths;
        }

        return new EventStatsAggregate(complete, roundsWon, aborted, score, kills, deaths);
    }

    /// <summary>
    /// Orders two aggregates, highest first. Zero means the two are
    /// indistinguishable from their reports, which is reported as no result
    /// rather than resolved by a coin toss.
    /// </summary>
    /// <param name="first">First team's totals.</param>
    /// <param name="second">Second team's totals.</param>
    public static int Compare(EventStatsAggregate first, EventStatsAggregate second)
    {
        if (first.RoundsWon != second.RoundsWon)
        {
            return Math.Sign(first.RoundsWon - second.RoundsWon);
        }

        // Fewer aborts is better, so the comparison is the other way round: a
        // team whose members quit should not win on that alone.
        if (first.AbortedCount != second.AbortedCount)
        {
            return Math.Sign(second.AbortedCount - first.AbortedCount);
        }

        if (first.Score != second.Score)
        {
            return Math.Sign(first.Score - second.Score);
        }

        if (first.Kills != second.Kills)
        {
            return Math.Sign(first.Kills - second.Kills);
        }

        // Fewer deaths is better, for the same reason as aborts.
        if (first.Deaths != second.Deaths)
        {
            return Math.Sign(second.Deaths - first.Deaths);
        }

        return 0;
    }

    /// <summary>
    /// Decides the match from both teams' reports. Returns null while a member is
    /// still to report, and null when the reports do not separate the teams.
    /// </summary>
    /// <param name="firstTeamIdentifier">First team of the match.</param>
    /// <param name="secondTeamIdentifier">Second team of the match.</param>
    /// <param name="first">First team's totals.</param>
    /// <param name="second">Second team's totals.</param>
    public static (int Winner, int Loser)? InferWinner(
        int firstTeamIdentifier,
        int secondTeamIdentifier,
        EventStatsAggregate first,
        EventStatsAggregate second)
    {
        if (!first.Complete || !second.Complete)
        {
            return null;
        }

        var comparison = Compare(first, second);
        if (comparison == 0)
        {
            return null;
        }

        return comparison > 0
            ? (firstTeamIdentifier, secondTeamIdentifier)
            : (secondTeamIdentifier, firstTeamIdentifier);
    }

    /// <summary>
    /// Resolves an authoritative terminal report to the team that won.
    /// <para>
    /// A terminal report names the two teams by the identities the match-found
    /// notification carried, which are the characters that represent them — the
    /// teams' owners — in winner-then-loser order. The report is trusted only
    /// when both identities name the two teams of this match, in that order: a
    /// report naming anyone else would otherwise decide a match it was not played
    /// in.
    /// </para>
    /// </summary>
    /// <param name="firstTeamIdentifier">First team of the match.</param>
    /// <param name="firstTeamOwnerIdentifier">Character representing the first team.</param>
    /// <param name="secondTeamIdentifier">Second team of the match.</param>
    /// <param name="secondTeamOwnerIdentifier">Character representing the second team.</param>
    /// <param name="winnerIdentity">Identity the report named as the winner.</param>
    /// <param name="loserIdentity">Identity the report named as the loser.</param>
    /// <returns>The winning team, or zero when the report does not fit this match.</returns>
    public static int ResolveTerminalWinner(
        int firstTeamIdentifier,
        int firstTeamOwnerIdentifier,
        int secondTeamIdentifier,
        int secondTeamOwnerIdentifier,
        int winnerIdentity,
        int loserIdentity)
    {
        if (winnerIdentity == 0 || loserIdentity == 0 || winnerIdentity == loserIdentity)
        {
            return 0;
        }

        if (winnerIdentity == firstTeamOwnerIdentifier && loserIdentity == secondTeamOwnerIdentifier)
        {
            return firstTeamIdentifier;
        }

        return winnerIdentity == secondTeamOwnerIdentifier && loserIdentity == firstTeamOwnerIdentifier
            ? secondTeamIdentifier
            : 0;
    }

    /// <summary>
    /// Tests whether enough time has passed since the last report for the outcome
    /// to be decided from reports. The delay exists so a native terminal report
    /// can arrive and be authoritative instead of being raced by the inference.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="lastReportedAt">Time of the newest report, or null when none exists.</param>
    /// <param name="grace">Grace the reports are given.</param>
    public static bool IsDecisionDue(
        DateTimeOffset now,
        DateTimeOffset? lastReportedAt,
        TimeSpan grace)
    {
        if (lastReportedAt is not { } reportedAt)
        {
            return false;
        }

        return now - reportedAt >= grace;
    }
}
