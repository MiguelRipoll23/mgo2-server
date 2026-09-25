using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// A team's roster as it stood when the team entered the field: who was in it,
/// in what order, and under what name.
/// <para>
/// It is a record rather than a view because a draw is not live. Once a field is
/// frozen the pairings are settled, so the pairing card has to name the roster
/// the draw was made with rather than whoever happens to be in the team now —
/// otherwise the card describes a team the bracket never matched.
/// </para>
/// </summary>
/// <param name="TeamIdentifier">Team the roster belongs to.</param>
/// <param name="Name">Name the team had when the roster was taken.</param>
/// <param name="MemberIdentifiers">Members in roster order.</param>
public readonly record struct EventRoster(
    int TeamIdentifier,
    string Name,
    IReadOnlyList<int> MemberIdentifiers);

/// <summary>
/// Freezes and reads the roster a Tournament team is drawn and shown with.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class TournamentRosterService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>
    /// Copies a team's current roster into the frozen record. It is written once
    /// per team and event: a team that re-enters an event it already entered is
    /// the same submission, and rewriting the roster would answer with a
    /// different team than the one the field holds.
    /// </summary>
    /// <param name="eventIdentifier">Event the roster was submitted to.</param>
    /// <param name="teamIdentifier">Team the roster belongs to.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task FreezeAsync(
        int eventIdentifier,
        int teamIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (eventIdentifier <= 0 || teamIdentifier <= 0)
        {
            return;
        }

        await using var context = await CreateContextAsync(cancellationToken);

        var alreadyFrozen = await context.TournamentRosterMembers
            .AnyAsync(
                member => member.EventIdentifier == eventIdentifier
                    && member.TeamIdentifier == teamIdentifier,
                cancellationToken);
        if (alreadyFrozen)
        {
            return;
        }

        var team = await context.EventTeams
            .Include(candidate => candidate.Members)
            .FirstOrDefaultAsync(
                candidate => candidate.Identifier == teamIdentifier,
                cancellationToken);
        if (team is null)
        {
            return;
        }

        var names = await context.Characters
            .Where(character => team.Members
                .Select(member => member.CharacterIdentifier)
                .Contains(character.Identifier))
            .ToDictionaryAsync(character => character.Identifier, character => character.Name, cancellationToken);

        var index = 0;
        foreach (var member in team.Members
            .Where(member => member.CharacterIdentifier != 0)
            .OrderBy(member => member.Slot))
        {
            if (!names.TryGetValue(member.CharacterIdentifier, out var name))
            {
                // A member the characters table does not carry cannot be shown on
                // a card, so it is left out rather than written as a blank.
                continue;
            }

            context.TournamentRosterMembers.Add(new TournamentRosterMember
            {
                EventIdentifier = eventIdentifier,
                TeamIdentifier = teamIdentifier,
                MemberIndex = index++,
                CharacterIdentifier = member.CharacterIdentifier,
                Name = name,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Reads the frozen roster of one team, or null when it has none.</summary>
    /// <param name="eventIdentifier">Event the roster was submitted to.</param>
    /// <param name="teamIdentifier">Team to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventRoster?> LoadAsync(
        int eventIdentifier,
        int teamIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (eventIdentifier <= 0 || teamIdentifier <= 0)
        {
            return null;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var members = await context.TournamentRosterMembers
            .Where(member => member.EventIdentifier == eventIdentifier
                && member.TeamIdentifier == teamIdentifier)
            .OrderBy(member => member.MemberIndex)
            .ToListAsync(cancellationToken);
        if (members.Count == 0)
        {
            return null;
        }

        // The name the card renders is the leader's, and the leader is the first
        // member of the roster the submission copied in slot order.
        return new EventRoster(
            teamIdentifier,
            members[0].Name,
            [.. members.Select(member => member.CharacterIdentifier)]);
    }
}
