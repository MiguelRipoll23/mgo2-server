using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>What finishing a match produced.</summary>
/// <param name="Winner">Team that won.</param>
/// <param name="Loser">Team that lost.</param>
/// <param name="Delivered">Sessions the outcome was delivered to.</param>
/// <param name="Bracket">How a Tournament bracket moved, or null when the match had none.</param>
/// <param name="BracketDelivered">Sessions the bracket was delivered to.</param>
public readonly record struct EventOutcomeDecision(
    int Winner,
    int Loser,
    int Delivered,
    TournamentAdvance? Bracket,
    int BracketDelivered);

/// <summary>Outcome of recording one player's report.</summary>
public enum EventReportOutcome
{
    /// <summary>The report was stored.</summary>
    Recorded,

    /// <summary>An identical report was already stored, so nothing changed.</summary>
    Duplicate,

    /// <summary>A different report was already stored for this player.</summary>
    Conflicting,

    /// <summary>The report names a character who is not in the match.</summary>
    NotAParticipant,
}

/// <summary>
/// Decides a finished event match from the players' own reports.
/// <para>
/// The match is completed by inference rather than by a single terminal report,
/// because only one of the two kinds of event host sends that report while both
/// send the per-player statistics. Waiting for the terminal report alone leaves
/// a team that played and won still waiting for an answer that never comes.
/// </para>
/// <para>
/// Nothing here is held in memory between reports. A match is played across two
/// processes and outlives either of them, so the reports are rows and the
/// decision is taken when they are complete and have settled.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="assignmentService">Service that pays and releases a completed match.</param>
/// <param name="outcomePushService">Service that tells both teams how the match ended.</param>
/// <param name="bracketService">Service that advances a Tournament bracket.</param>
/// <param name="bracketPushService">Service that shows the advanced bracket to its entrants.</param>
/// <param name="options">Event configuration, which carries the settling grace.</param>
public sealed class EventOutcomeService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    EventAssignmentService assignmentService,
    EventOutcomePushService outcomePushService,
    TournamentBracketService bracketService,
    EventBracketPushService bracketPushService,
    IOptions<EventOptions> options)
    : DomainService(contextFactory)
{
    /// <summary>Stores one player's report for a leased game.</summary>
    /// <param name="matchIdentifier">Match the game is playing.</param>
    /// <param name="characterIdentifier">Character the report describes.</param>
    /// <param name="roundsWon">Rounds the character's team won.</param>
    /// <param name="aborted">Whether the report was for an aborted match.</param>
    /// <param name="score">Score the character reported.</param>
    /// <param name="kills">Kills the character reported.</param>
    /// <param name="deaths">Deaths the character reported.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventReportOutcome> RecordAsync(
        int matchIdentifier,
        int characterIdentifier,
        int roundsWon,
        bool aborted,
        int score,
        int kills,
        int deaths,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
            .FirstOrDefaultAsync(candidate => candidate.Identifier == matchIdentifier, cancellationToken);
        if (match is null)
        {
            return EventReportOutcome.NotAParticipant;
        }

        var teams = await context.EventTeams
            .Include(team => team.Members)
            .Where(team => team.Identifier == match.FirstTeamIdentifier
                || team.Identifier == match.SecondTeamIdentifier)
            .ToListAsync(cancellationToken);

        var teamIdentifier = 0;
        foreach (var team in teams)
        {
            if (team.Members.Any(member => member.CharacterIdentifier == characterIdentifier))
            {
                teamIdentifier = team.Identifier;
            }
        }

        if (teamIdentifier == 0)
        {
            return EventReportOutcome.NotAParticipant;
        }

        var existing = await context.EventStatReports
            .FirstOrDefaultAsync(
                report => report.MatchIdentifier == matchIdentifier
                    && report.CharacterIdentifier == characterIdentifier,
                cancellationToken);
        if (existing is not null)
        {
            // A repeated report of the same match is the same report. A different
            // one for a player who already reported is refused rather than
            // overwriting, so a late correction cannot flip a decided match.
            return existing.RoundsWon == roundsWon
                && existing.Aborted == aborted
                && existing.Score == score
                && existing.Kills == kills
                && existing.Deaths == deaths
                ? EventReportOutcome.Duplicate
                : EventReportOutcome.Conflicting;
        }

        context.EventStatReports.Add(new EventStatReport
        {
            MatchIdentifier = matchIdentifier,
            CharacterIdentifier = characterIdentifier,
            TeamIdentifier = teamIdentifier,
            RoundsWon = roundsWon,
            Aborted = aborted,
            Score = score,
            Kills = kills,
            Deaths = deaths,
            ReportedAt = DateTimeOffset.UtcNow,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return EventReportOutcome.Duplicate;
        }

        return EventReportOutcome.Recorded;
    }

    /// <summary>
    /// Completes a match whose reports are complete and have settled. Called
    /// whenever a report arrives and by the sweep, so a match finishes without
    /// anyone having to hold a timer for it.
    /// </summary>
    /// <param name="matchIdentifier">Match to decide.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>What the decision produced, or null when the match is not decided yet.</returns>
    public async Task<EventOutcomeDecision?> TryCompleteAsync(
        int matchIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
            .FirstOrDefaultAsync(candidate => candidate.Identifier == matchIdentifier, cancellationToken);
        if (match is null || match.State != EventConstants.MatchAssignedState)
        {
            // Only an assigned match is in play; a decided or cancelled one has
            // nothing to infer.
            return null;
        }

        var reports = await context.EventStatReports
            .Where(report => report.MatchIdentifier == matchIdentifier)
            .ToListAsync(cancellationToken);
        if (reports.Count == 0)
        {
            return null;
        }

        var grace = TimeSpan.FromMilliseconds(Math.Max(0, options.Value.ReportGraceMilliseconds));
        if (!EventOutcomeUtils.IsDecisionDue(
                DateTimeOffset.UtcNow,
                reports.Max(report => report.ReportedAt),
                grace))
        {
            return null;
        }

        var teams = await context.EventTeams
            .Include(team => team.Members)
            .Where(team => team.Identifier == match.FirstTeamIdentifier
                || team.Identifier == match.SecondTeamIdentifier)
            .ToListAsync(cancellationToken);
        var first = teams.FirstOrDefault(team => team.Identifier == match.FirstTeamIdentifier);
        var second = teams.FirstOrDefault(team => team.Identifier == match.SecondTeamIdentifier);
        if (first is null || second is null)
        {
            return null;
        }

        var byCharacter = reports.ToDictionary(report => report.CharacterIdentifier);
        var inferred = EventOutcomeUtils.InferWinner(
            match.FirstTeamIdentifier,
            match.SecondTeamIdentifier,
            EventOutcomeUtils.Aggregate(EventTeamService.BuildSnapshot(first), byCharacter),
            EventOutcomeUtils.Aggregate(EventTeamService.BuildSnapshot(second), byCharacter));
        if (inferred is null)
        {
            // The reports do not separate the teams. Leaving the match open is
            // deliberate: inventing a winner here would advance the wrong team.
            return null;
        }

        var result = inferred.Value;

        return await FinishAsync(matchIdentifier, result.Winner, result.Loser, cancellationToken);
    }

    /// <summary>
    /// Completes a match from an authoritative terminal report. It is the path a
    /// native event host takes, and it exists so that report can arrive first and
    /// decide the match directly rather than being raced by the inference above.
    /// </summary>
    /// <param name="matchIdentifier">Match the report is for.</param>
    /// <param name="winnerIdentity">Character the report named as the winner's representative.</param>
    /// <param name="loserIdentity">Character the report named as the loser's representative.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>What the decision produced, or null when the report did not fit.</returns>
    public async Task<EventOutcomeDecision?> ReportTerminalAsync(
        int matchIdentifier,
        int winnerIdentity,
        int loserIdentity,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
            .FirstOrDefaultAsync(
                candidate => candidate.Identifier == matchIdentifier,
                cancellationToken);
        if (match is null || match.State != EventConstants.MatchAssignedState)
        {
            return null;
        }

        var teams = await context.EventTeams
            .Where(team => team.Identifier == match.FirstTeamIdentifier
                || team.Identifier == match.SecondTeamIdentifier)
            .ToListAsync(cancellationToken);
        var first = teams.FirstOrDefault(team => team.Identifier == match.FirstTeamIdentifier);
        var second = teams.FirstOrDefault(team => team.Identifier == match.SecondTeamIdentifier);
        if (first is null || second is null)
        {
            return null;
        }

        var winner = EventOutcomeUtils.ResolveTerminalWinner(
            first.Identifier,
            first.OwnerCharacterIdentifier,
            second.Identifier,
            second.OwnerCharacterIdentifier,
            winnerIdentity,
            loserIdentity);
        if (winner == 0)
        {
            // The report names identities that are not this match's two teams in
            // winner-then-loser order, so it is not a result for this match.
            return null;
        }

        var loser = winner == first.Identifier ? second.Identifier : first.Identifier;
        return await FinishAsync(matchIdentifier, winner, loser, cancellationToken);
    }

    /// <summary>
    /// The single place a match is finished, whichever path decided it: pay both
    /// teams, tell them, and let a Tournament match advance its bracket. Both
    /// paths share it so a terminal result and an inferred one cannot drift apart.
    /// </summary>
    private async Task<EventOutcomeDecision> FinishAsync(
        int matchIdentifier,
        int winnerTeamIdentifier,
        int loserTeamIdentifier,
        CancellationToken cancellationToken)
    {
        await assignmentService.CompleteAsync(matchIdentifier, winnerTeamIdentifier, cancellationToken);

        var delivered = await outcomePushService.PushOutcomeAsync(
            winnerTeamIdentifier,
            loserTeamIdentifier,
            cancellationToken);

        // A Survival match answers nothing here rather than freezing an event
        // that never entered a team.
        var bracket = await bracketService.AdvanceFromMatchAsync(
            matchIdentifier,
            winnerTeamIdentifier,
            cancellationToken);
        var bracketDelivered = bracket is { } advance
            ? await bracketPushService.PushAdvanceAsync(advance, cancellationToken)
            : 0;

        return new EventOutcomeDecision(
            winnerTeamIdentifier,
            loserTeamIdentifier,
            delivered,
            bracket,
            bracketDelivered);
    }

    /// <summary>Lists the reports stored for a match.</summary>
    /// <param name="matchIdentifier">Match to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<EventStatReport>> ListReportsAsync(
        int matchIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventStatReports
            .Where(report => report.MatchIdentifier == matchIdentifier)
            .OrderBy(report => report.Identifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Finds the match a leased game is playing, when it has one.</summary>
    /// <param name="gameIdentifier">Game to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<int> FindMatchForGameAsync(
        int gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var lease = await context.EventHostLeases
            .FirstOrDefaultAsync(
                candidate => candidate.GameIdentifier == gameIdentifier
                    && candidate.Status == EventConstants.LeaseActiveState,
                cancellationToken);
        return lease?.MatchIdentifier ?? 0;
    }
}
