using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Turns a bracket into matches: it closes the field, seeds it, and pairs every
/// fixture the bracket is ready to play.
/// <para>
/// This is what makes a Tournament a played draw rather than a list. The
/// pairings are the bracket's own — the two teams of a fixture, in the order the
/// seed order put them — so the match a team is assigned is the opponent the
/// draw named, and the result that comes back resolves the fixture it was played
/// as rather than whichever fixture happens to contain the winner.
/// </para>
/// <para>
/// The pass is idempotent and safe to run from more than one process: a match is
/// created only for a fixture whose two teams have no live match, and the partial
/// unique indexes on the two team columns are what settles a race the check
/// loses. Running it again after a result pairs the round that result opened.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="bracketService">Service that owns the seeds and the bracket.</param>
/// <param name="matchService">Service that persists the pairings.</param>
/// <param name="options">Event configuration, which carries the field capacity.</param>
public sealed class TournamentMatchService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    TournamentBracketService bracketService,
    EventMatchService matchService,
    IOptions<EventOptions> options)
    : DomainService(contextFactory)
{
    /// <summary>Pairs every fixture the brackets of every field are ready to play.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Matches created.</returns>
    public async Task<int> PairReadyFixturesAsync(CancellationToken cancellationToken = default)
    {
        var paired = 0;
        foreach (var eventIdentifier in await ListFieldEventsAsync(cancellationToken))
        {
            paired += await PairEventAsync(eventIdentifier, cancellationToken);
        }

        return paired;
    }

    /// <summary>Closes, seeds and pairs one event's field, when it has closed.</summary>
    /// <param name="eventIdentifier">Event to advance.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Matches created.</returns>
    public async Task<int> PairEventAsync(
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (eventIdentifier <= 0
            || !await AreEntriesClosedAsync(eventIdentifier, cancellationToken))
        {
            // An open field is still taking teams, and seeding it now would draw
            // a bracket around a field that is not finished.
            return 0;
        }

        var seeds = await bracketService.LoadSeedOrderAsync(eventIdentifier, cancellationToken);
        if (!TournamentSeedingUtils.IsSeedable(seeds))
        {
            return 0;
        }

        // Freezing refuses a second freeze itself, so a bracket already being
        // played keeps the seed order it was drawn with.
        await bracketService.FreezeAsync(eventIdentifier, seeds, cancellationToken);

        var tree = await bracketService.RebuildAsync(eventIdentifier, cancellationToken);
        if (tree is null)
        {
            return 0;
        }

        var paired = 0;
        foreach (var fixture in tree.ReadyFixtures())
        {
            if (await HasLiveMatchAsync(fixture.FirstTeamIdentifier, cancellationToken)
                || await HasLiveMatchAsync(fixture.SecondTeamIdentifier, cancellationToken))
            {
                // A fixture stays ready until its result is recorded, so without
                // this the same draw would be paired again on every pass.
                continue;
            }

            var lobbyIdentifier = await FindTeamLobbyAsync(fixture.FirstTeamIdentifier, cancellationToken);
            try
            {
                await matchService.CreateAsync(
                    lobbyIdentifier,
                    EventConstants.TournamentSelector,
                    fixture.FirstTeamIdentifier,
                    fixture.SecondTeamIdentifier,
                    cancellationToken);
                paired++;
            }
            catch (DbUpdateException)
            {
                // Another process paired one of these teams between the check and
                // the write. The index decided it, and its match is the live one.
            }
        }

        return paired;
    }

    /// <summary>Lists the events that have a field: entrants, or a seeded bracket.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<int>> ListFieldEventsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var registered = await context.TournamentRegistrations
            .Select(registration => registration.EventIdentifier)
            .Distinct()
            .ToListAsync(cancellationToken);
        var seeded = await context.TournamentBrackets
            .Select(bracket => bracket.EventIdentifier)
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. registered.Union(seeded).OrderBy(identifier => identifier)];
    }

    private async Task<bool> AreEntriesClosedAsync(
        int eventIdentifier,
        CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var teamIdentifiers = await context.TournamentRegistrations
            .Where(registration => registration.EventIdentifier == eventIdentifier
                && registration.TeamIdentifier != null)
            .Select(registration => registration.TeamIdentifier!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var teams = await context.EventTeams
            .Include(team => team.Members)
            .Where(team => teamIdentifiers.Contains(team.Identifier))
            .ToListAsync(cancellationToken);

        var readiness = teams
            .Select(team => team.Members
                .Where(member => member.CharacterIdentifier != 0)
                .All(member => member.State == EventConstants.ParticipantReadyState))
            .ToList();

        return TournamentSeedingUtils.EntriesClosed(
            teams.Count,
            TournamentRegistrationUtils.TeamCapacity(options.Value),
            readiness);
    }

    private async Task<bool> HasLiveMatchAsync(int teamIdentifier, CancellationToken cancellationToken) =>
        await matchService.FindActiveByTeamAsync(teamIdentifier, cancellationToken) is not null;

    /// <summary>
    /// Returns the lobby a fixture is played in, taken from its first team. The
    /// entrant team's lobby is where it entered the event, and both teams of a
    /// fixture are entrants of the same event.
    /// </summary>
    private async Task<int> FindTeamLobbyAsync(int teamIdentifier, CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventTeams
            .Where(team => team.Identifier == teamIdentifier)
            .Select(team => (int?)team.LobbyIdentifier)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
    }
}
