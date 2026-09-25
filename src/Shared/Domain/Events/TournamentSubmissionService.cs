using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Submits a formed team for an event, giving every member a place under one
/// team slot.
/// <para>
/// This is the second half of taking a place, and it is the half that has a
/// roster to check. A reservation asks only whether a character may enter; a
/// submission asks whether the people named can actually play, because a team
/// that goes into a draw with somebody not connected is a fixture decided by
/// forfeit and the bracket has no way to say so.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="scheduleService">Service that resolves a named event to its schedule.</param>
public sealed class TournamentSubmissionService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    EventScheduleService scheduleService)
    : DomainService(contextFactory)
{

    /// <summary>
    /// Submits a formed team for an event, giving every member a place under one
    /// team slot.
    /// <para>
    /// A member who already reserved keeps the same row and it gains the team and
    /// the slot, which is why reservations and registrations share one table: the
    /// reservation is not replaced by the submission, it is completed by it. The
    /// team slot is allocated over team places rather than over player places,
    /// because a submitted team occupies one place in the field however many
    /// players it brings.
    /// </para>
    /// </summary>
    /// <param name="eventIdentifier">Event being entered.</param>
    /// <param name="teamIdentifier">Team being submitted.</param>
    /// <param name="characterIdentifier">Character submitting, which must lead the team.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<TournamentSubmitOutcome> SubmitTeamAsync(
        int eventIdentifier,
        int teamIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (teamIdentifier <= 0 || characterIdentifier <= 0)
        {
            return TournamentSubmitOutcome.NotTheLeader;
        }

        var schedule = await scheduleService.FindOpenAsync(
            eventIdentifier,
            EventConstants.TournamentRegistrationSelector,
            cancellationToken);
        if (schedule is null)
        {
            // A team may only enter an event players are still being admitted to.
            return TournamentSubmitOutcome.EventUnavailable;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var team = await context.EventTeams
            .Include(candidate => candidate.Members)
            .FirstOrDefaultAsync(candidate => candidate.Identifier == teamIdentifier, cancellationToken);
        if (team is null || team.Members.Count == 0)
        {
            return TournamentSubmitOutcome.NotTheLeader;
        }

        // Only the team's own leader may submit it, so a member cannot enter a
        // roster it does not own.
        if (team.OwnerCharacterIdentifier != characterIdentifier)
        {
            return TournamentSubmitOutcome.NotTheLeader;
        }

        if (await context.TournamentBrackets
                .AnyAsync(bracket => bracket.EventIdentifier == eventIdentifier, cancellationToken))
        {
            // Once the field is frozen a new team cannot be seeded into it.
            return TournamentSubmitOutcome.BracketFrozen;
        }

        var members = team.Members.OrderBy(member => member.Slot).ToList();
        var memberIdentifiers = members
            .Select(member => member.CharacterIdentifier)
            .Where(identifier => identifier != 0)
            .ToList();
        if (memberIdentifiers.Count == 0
            || memberIdentifiers.Count > EventConstants.TeamMemberLimit)
        {
            // A field is drawn from teams, and a team is either a roster somebody
            // can field or it is not one. An empty or oversized roster would
            // seed a bracket with an entrant that cannot play.
            return TournamentSubmitOutcome.RosterNotPlayable;
        }

        // Every member has to exist and be connected. A roster naming somebody
        // who is not there is a roster that would be decided by forfeit rather
        // than played, and the bracket has no way to express that.
        var online = await context.Characters
            .Where(character => memberIdentifiers.Contains(character.Identifier))
            .Join(
                context.CharacterPresence,
                character => character.Identifier,
                presence => presence.CharacterIdentifier,
                (character, presence) => character.Identifier)
            .ToListAsync(cancellationToken);
        if (online.Count != memberIdentifiers.Count)
        {
            return TournamentSubmitOutcome.MemberNotOnline;
        }

        var registrations = await context.TournamentRegistrations
            .Where(registration => registration.EventIdentifier == eventIdentifier
                || memberIdentifiers.Contains(registration.CharacterIdentifier))
            .ToListAsync(cancellationToken);

        var existing = registrations
            .Where(registration => registration.EventIdentifier == eventIdentifier
                && registration.TeamIdentifier == teamIdentifier)
            .ToList();
        if (existing.Count > 0)
        {
            // Submitting the same team again is the same request, not a second
            // place: the first submission already put it in the field.
            return TournamentSubmitOutcome.AlreadyRegistered;
        }

        foreach (var member in memberIdentifiers)
        {
            // A member who is playing here cannot also be playing in another
            // event, and a member holding a place in an event they have not
            // entered has not surrendered it: the reference refuses both, and a
            // player who had to choose between two entries should be told rather
            // than entered in both.
            var elsewhere = registrations.Any(registration =>
                registration.CharacterIdentifier == member
                && registration.EventIdentifier != eventIdentifier);
            if (elsewhere)
            {
                return TournamentSubmitOutcome.MemberRegisteredElsewhere;
            }
        }

        var capacity = EventScheduleService.TeamCapacityOf(schedule);
        var takenSlots = registrations
            .Where(registration => registration.EventIdentifier == eventIdentifier
                && registration.TeamIdentifier is not null)
            .Select(registration => registration.SlotIndex)
            .ToList();
        var slot = TournamentRegistrationUtils.NextSlot(takenSlots, capacity);
        if (slot >= capacity)
        {
            return TournamentSubmitOutcome.TournamentFull;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var member in members)
        {
            var registration = registrations.FirstOrDefault(candidate =>
                candidate.EventIdentifier == eventIdentifier
                && candidate.CharacterIdentifier == member.CharacterIdentifier);
            if (registration is null)
            {
                context.TournamentRegistrations.Add(new TournamentRegistration
                {
                    EventIdentifier = eventIdentifier,
                    CharacterIdentifier = member.CharacterIdentifier,
                    TeamIdentifier = teamIdentifier,
                    SlotIndex = slot,
                    ReservedAt = now,
                });
                continue;
            }

            // The member's own reservation, completed by the submission.
            registration.TeamIdentifier = teamIdentifier;
            registration.SlotIndex = slot;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another lobby submitted a team into the same place first.
            return TournamentSubmitOutcome.TournamentFull;
        }

        return TournamentSubmitOutcome.Registered;
    }
}
