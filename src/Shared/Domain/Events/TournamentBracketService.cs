using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>Status of a frozen bracket.</summary>
public static class TournamentBracketStatus
{
    /// <summary>The field is frozen and the bracket is being played.</summary>
    public const int Running = 1;

    /// <summary>The bracket was played out and has a champion.</summary>
    public const int Decided = 2;
}

/// <summary>
/// Persists the Tournament bracket. The bracket tree itself is never stored:
/// the frozen seed order and the ledger of results carry it, and the tree is
/// rebuilt from them. That is what makes a restart resume the same bracket, and
/// it is why a replayed result is caught by the tree rather than by a stored
/// flag the process used to hold in memory.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class TournamentBracketService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Freezes the field into a bracket.</summary>
    /// <param name="eventIdentifier">Event being frozen.</param>
    /// <param name="teamIdentifiers">Entrants in the order they are seeded.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether this call froze the field; false when it was already frozen.</returns>
    public async Task<bool> FreezeAsync(
        int eventIdentifier,
        IReadOnlyList<int> teamIdentifiers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(teamIdentifiers);
        if (eventIdentifier <= 0)
        {
            throw new ArgumentException("A bracket belongs to an event.", nameof(eventIdentifier));
        }

        await using var context = await CreateContextAsync(cancellationToken);
        if (await context.TournamentBrackets
                .AnyAsync(bracket => bracket.EventIdentifier == eventIdentifier, cancellationToken))
        {
            // Seeding is written once and frozen, so a second freeze is refused
            // rather than allowed to reseed a bracket that is being played.
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        context.TournamentBrackets.Add(new TournamentBracket
        {
            EventIdentifier = eventIdentifier,
            Status = TournamentBracketStatus.Running,
            CurrentRound = 1,
            CreatedAt = now,
            UpdatedAt = now,
        });

        for (var index = 0; index < teamIdentifiers.Count; index++)
        {
            context.TournamentSeeds.Add(new TournamentSeed
            {
                EventIdentifier = eventIdentifier,
                SeedIndex = index,
                TeamIdentifier = teamIdentifiers[index],
            });
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another lobby froze the same field between the check and the write.
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns the event's bracket, freezing it from the submitted teams if it has
    /// not been frozen yet. Freezing is lazy because the caller decides when
    /// entries close: the first thing that needs the bracket is the moment it is
    /// known no further team will be submitted, and a second caller finds it
    /// already frozen rather than reseeding it.
    /// </summary>
    /// <param name="eventIdentifier">Event to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The bracket, or null when the event has no seedable field.</returns>
    public async Task<TournamentBracket?> EnsureFrozenAsync(
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindAsync(eventIdentifier, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var seeds = await LoadSeedOrderAsync(eventIdentifier, cancellationToken);
        if (!TournamentSeedingUtils.IsSeedable(seeds))
        {
            return null;
        }

        await FreezeAsync(eventIdentifier, seeds, cancellationToken);
        return await FindAsync(eventIdentifier, cancellationToken);
    }

    /// <summary>
    /// Advances the tournament the finished match belonged to. A match that is
    /// not a Tournament match, or belongs to an event with no field to seed,
    /// reports no result rather than creating a bracket nothing will play.
    /// </summary>
    /// <param name="matchIdentifier">Match that finished.</param>
    /// <param name="winnerTeamIdentifier">Team that won.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The bracket as it stands, or null when the match had none.</returns>
    public async Task<TournamentAdvance?> AdvanceFromMatchAsync(
        int matchIdentifier,
        int winnerTeamIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
            .FirstOrDefaultAsync(
                candidate => candidate.Identifier == matchIdentifier,
                cancellationToken);
        if (match is null || match.MatchType != EventConstants.TournamentSelector)
        {
            // A Survival match has no bracket, and asking for one would freeze an
            // event that never entered a team.
            return null;
        }

        var eventIdentifier = await context.EventTeams
            .Where(team => team.Identifier == match.FirstTeamIdentifier
                || team.Identifier == match.SecondTeamIdentifier)
            .OrderBy(team => team.Identifier)
            .Select(team => (int?)team.EventIdentifier)
            .FirstOrDefaultAsync(cancellationToken);
        // A team with no event, and one naming a zero event, both hold no bracket.
        var eventId = eventIdentifier ?? 0;
        if (eventId <= 0)
        {
            return null;
        }

        var bracket = await EnsureFrozenAsync(eventId, cancellationToken);
        if (bracket is null)
        {
            return null;
        }

        var outcome = await ReportResultAsync(
            eventId,
            matchIdentifier,
            winnerTeamIdentifier,
            cancellationToken);
        var tree = await RebuildAsync(eventId, cancellationToken);
        if (tree is null)
        {
            return null;
        }

        return new TournamentAdvance(
            eventId,
            matchIdentifier,
            await FindRecordedRoundAsync(eventId, matchIdentifier, cancellationToken),
            tree.Champion(),
            tree.RunnerUp(),
            outcome);
    }

    /// <summary>Reads the round a match's result was recorded in.</summary>
    /// <param name="eventIdentifier">Event being played.</param>
    /// <param name="matchIdentifier">Match that reported the result.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The recorded round, or zero when the match reported nothing.</returns>
    private async Task<int> FindRecordedRoundAsync(
        int eventIdentifier,
        int matchIdentifier,
        CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var result = await context.TournamentResults
            .FirstOrDefaultAsync(
                candidate => candidate.EventIdentifier == eventIdentifier
                    && candidate.MatchIdentifier == matchIdentifier,
                cancellationToken);
        return result?.RoundIndex ?? 0;
    }

    /// <summary>Reads the submitted teams of an event in seed order.</summary>
    /// <param name="eventIdentifier">Event to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<IReadOnlyList<int>> LoadSeedOrderAsync(
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var entrants = await context.TournamentRegistrations
            .Where(registration => registration.EventIdentifier == eventIdentifier)
            .Select(registration => new TournamentEntrant(
                registration.SlotIndex,
                registration.TeamIdentifier ?? 0))
            .ToListAsync(cancellationToken);

        return TournamentSeedingUtils.OrderTeamSeeds(entrants);
    }

    /// <summary>
    /// Reads the frozen seed order of a drawn bracket.
    /// <para>
    /// This is not the same read as <see cref="LoadSeedOrderAsync"/>, and the
    /// difference matters once the field has been released: the submitted teams
    /// are what the field was drawn from, while the seeds are what it was drawn
    /// as. A bracket that has been played out no longer has its submissions, and
    /// the bracket it was still describes itself in its seeds.
    /// </para>
    /// </summary>
    /// <param name="eventIdentifier">Event to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Team identifiers in the order they were drawn, or an empty list.</returns>
    public async Task<IReadOnlyList<int>> LoadFrozenSeedOrderAsync(
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (eventIdentifier <= 0)
        {
            return [];
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var seeds = await context.TournamentSeeds
            .Where(seed => seed.EventIdentifier == eventIdentifier)
            .OrderBy(seed => seed.SeedIndex)
            .Select(seed => seed.TeamIdentifier)
            .ToListAsync(cancellationToken);

        return seeds;
    }

    /// <summary>Finds a frozen bracket.</summary>
    /// <param name="eventIdentifier">Event to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<TournamentBracket?> FindAsync(
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.TournamentBrackets
            .FirstOrDefaultAsync(
                bracket => bracket.EventIdentifier == eventIdentifier,
                cancellationToken);
    }

    /// <summary>Reads the bracket as a tree by replaying its recorded results.</summary>
    /// <param name="eventIdentifier">Event to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The reconstructed tree, or null when the field is not frozen.</returns>
    public async Task<TournamentBracketTree?> RebuildAsync(
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var bracket = await context.TournamentBrackets
            .FirstOrDefaultAsync(
                candidate => candidate.EventIdentifier == eventIdentifier,
                cancellationToken);
        if (bracket is null)
        {
            return null;
        }

        var seeds = await context.TournamentSeeds
            .Where(seed => seed.EventIdentifier == eventIdentifier)
            .OrderBy(seed => seed.SeedIndex)
            .ToListAsync(cancellationToken);
        if (seeds.Count == 0)
        {
            return null;
        }

        var tree = new TournamentBracketTree([.. seeds.Select(seed => seed.TeamIdentifier)]);
        var results = await context.TournamentResults
            .Where(result => result.EventIdentifier == eventIdentifier)
            .OrderBy(result => result.Identifier)
            .ToListAsync(cancellationToken);

        foreach (var result in results)
        {
            Replay(tree, result);
        }

        return tree;
    }

    /// <summary>
    /// Records the result of the fixture two teams played and advances the
    /// bracket. The fixture is resolved from <em>both</em> teams rather than from
    /// the winner alone: a match that paired two teams the bracket never put
    /// together is not this bracket's fixture, and resolving it against whoever
    /// the winner was drawn to meet would advance a team past an opponent it
    /// never faced.
    /// </summary>
    /// <param name="eventIdentifier">Event being played.</param>
    /// <param name="matchIdentifier">Match the fixture was played as.</param>
    /// <param name="winnerTeamIdentifier">Winning team.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>What happened to the bracket.</returns>
    public async Task<TournamentResultOutcome> ReportResultAsync(
        int eventIdentifier,
        int matchIdentifier,
        int winnerTeamIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var match = await context.EventMatches
            .FirstOrDefaultAsync(
                candidate => candidate.Identifier == matchIdentifier,
                cancellationToken);
        if (match is null)
        {
            return TournamentResultOutcome.NotAFixture;
        }

        // A match that already reported a result is answered from the ledger
        // before the tree is consulted, because a resolved fixture is no longer
        // a ready one: without this a repeated report would be indistinguishable
        // from a match that was never part of the draw.
        var recorded = await context.TournamentResults
            .FirstOrDefaultAsync(
                result => result.EventIdentifier == eventIdentifier
                    && result.MatchIdentifier == matchIdentifier,
                cancellationToken);
        if (recorded is not null)
        {
            // The same claim again is the same report. A different one is not a
            // replay of anything, and a decided fixture is not this call's to
            // reopen, so a conflicting claim records nothing.
            return recorded.WinnerTeamIdentifier == winnerTeamIdentifier
                ? TournamentResultOutcome.Replayed
                : TournamentResultOutcome.NotAFixture;
        }

        var tree = await RebuildAsync(eventIdentifier, cancellationToken);
        if (tree is null)
        {
            return TournamentResultOutcome.NotAFixture;
        }

        var fixture = tree.ReadyFixtureOf(match.FirstTeamIdentifier, match.SecondTeamIdentifier);
        if (fixture.Node == 0
            || (winnerTeamIdentifier != fixture.FirstTeamIdentifier
                && winnerTeamIdentifier != fixture.SecondTeamIdentifier))
        {
            // Either the two teams are not playing a fixture, or the winner is
            // not one of the two that played it.
            return TournamentResultOutcome.NotAFixture;
        }

        var outcome = tree.RecordWinner(fixture.Node, winnerTeamIdentifier);
        if (outcome == TournamentResultOutcome.NotAFixture)
        {
            return outcome;
        }

        var bracket = await context.TournamentBrackets
            .FirstOrDefaultAsync(
                candidate => candidate.EventIdentifier == eventIdentifier,
                cancellationToken);
        if (bracket is null)
        {
            return TournamentResultOutcome.NotAFixture;
        }

        var now = DateTimeOffset.UtcNow;
        context.TournamentResults.Add(new TournamentResult
        {
            EventIdentifier = eventIdentifier,
            MatchIdentifier = matchIdentifier,
            RoundIndex = fixture.Round,
            FirstTeamIdentifier = fixture.FirstTeamIdentifier,
            SecondTeamIdentifier = fixture.SecondTeamIdentifier,
            WinnerTeamIdentifier = winnerTeamIdentifier,
            ReportedAt = now,
        });

        var champion = tree.Champion();
        bracket.CurrentRound = Math.Max(1, tree.NextRound());
        bracket.ChampionTeamIdentifier = champion == 0 ? null : champion;
        bracket.Status = champion == 0 ? TournamentBracketStatus.Running : TournamentBracketStatus.Decided;
        bracket.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
        return outcome;
    }

    private static void Replay(TournamentBracketTree tree, TournamentResult result)
    {
        var fixture = tree.ReadyFixtureOf(result.FirstTeamIdentifier, result.SecondTeamIdentifier);
        if (fixture.Node != 0)
        {
            tree.RecordWinner(fixture.Node, result.WinnerTeamIdentifier);
        }
    }
}
