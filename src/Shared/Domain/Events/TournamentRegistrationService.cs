using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Owns the Tournament places a character holds before it has a team. A
/// reservation is a registration row with no team, which is why the two are one
/// table: a place becomes a registration when a team is submitted, and until
/// then it is the same claim with a null team.
/// <para>
/// A place belongs to an event, and the event's own schedule decides whether it
/// is still live — so a place can only be held in an event somebody published,
/// and capacity is counted per event, because two events running at once each
/// have their own field and neither fills the other's.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="scheduleService">Service that resolves a named event to its schedule.</param>
/// <param name="informationService">Source of the advertised daily window.</param>
/// <param name="options">Event configuration.</param>
public sealed class TournamentRegistrationService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    EventScheduleService scheduleService,
    EventInformationService informationService,
    IOptions<EventOptions> options)
    : DomainService(contextFactory)
{
    /// <summary>Takes a place in an event for one character.</summary>
    /// <param name="characterIdentifier">Character asking.</param>
    /// <param name="eventIdentifier">Event it is asking to enter.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>What happened.</returns>
    public async Task<TournamentReserveOutcome> ReserveAsync(
        int characterIdentifier,
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (characterIdentifier <= 0)
        {
            return TournamentReserveOutcome.CharacterMissing;
        }

        // The event a client names is resolved before anything is written, so a
        // place is never held in an event that does not exist, is unpublished,
        // or whose window is shut.
        var schedule = await scheduleService.FindOpenAsync(
            eventIdentifier,
            EventConstants.TournamentRegistrationSelector,
            cancellationToken);
        if (schedule is null)
        {
            return TournamentReserveOutcome.EventUnavailable;
        }

        var now = DateTimeOffset.UtcNow;
        await using var context = await CreateContextAsync(cancellationToken);

        var experience = await context.Characters
            .Where(character => character.Identifier == characterIdentifier)
            .Select(character => (int?)character.Experience)
            .FirstOrDefaultAsync(cancellationToken);
        if (experience is null)
        {
            return TournamentReserveOutcome.CharacterMissing;
        }

        var level = LevelUtils.CalculateLevel(experience.Value);
        if (!TournamentRegistrationUtils.IsLevelEligible(level, options.Value))
        {
            return TournamentReserveOutcome.LevelNotEligible;
        }

        var live = await FindLiveRowsAsync(context, cancellationToken);
        var existing = live.FirstOrDefault(row => row.CharacterIdentifier == characterIdentifier);
        if (existing is not null)
        {
            // A retry of the event the character already holds is the same
            // request, not a second place.
            return existing.EventIdentifier == eventIdentifier
                ? TournamentReserveOutcome.AlreadyReserved
                : TournamentReserveOutcome.ReservedElsewhere;
        }

        // The field being filled is this event's, not the server's.
        var capacity = TournamentRegistrationUtils.PlaceCapacity(
            EventScheduleService.TeamCapacityOf(schedule));
        var taken = live
            .Where(row => row.EventIdentifier == eventIdentifier)
            .Select(row => row.SlotIndex)
            .ToList();
        if (taken.Count >= capacity)
        {
            return TournamentReserveOutcome.NoPlacesLeft;
        }

        // A guard against the unique key rather than a substitute for it: two
        // lobbies can take the same place at once, and the index is what decides
        // which of them keeps it.
        var takenByCharacter = await context.TournamentRegistrations
            .AnyAsync(
                registration => registration.EventIdentifier == eventIdentifier
                    && registration.CharacterIdentifier == characterIdentifier,
                cancellationToken);
        if (takenByCharacter)
        {
            return TournamentReserveOutcome.AlreadyReserved;
        }

        var slot = TournamentRegistrationUtils.NextSlot(taken, capacity);
        context.TournamentRegistrations.Add(new TournamentRegistration
        {
            EventIdentifier = eventIdentifier,
            CharacterIdentifier = characterIdentifier,
            SlotIndex = slot,
            ReservedAt = now,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another lobby took the same place between the read and the write.
            return TournamentReserveOutcome.AlreadyReserved;
        }

        return TournamentReserveOutcome.Reserved;
    }

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
        var memberIdentifiers = members.Select(member => member.CharacterIdentifier).ToList();
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
            var elsewhere = registrations.Any(registration =>
                registration.CharacterIdentifier == member
                && registration.TeamIdentifier is not null
                && registration.TeamIdentifier != teamIdentifier);
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

    /// <summary>Finds the place a character holds, when its event is still open.</summary>
    /// <param name="characterIdentifier">Character to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<TournamentRegistration?> FindLiveAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (characterIdentifier <= 0)
        {
            return null;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var live = await FindLiveRowsAsync(context, cancellationToken);
        return live.FirstOrDefault(row => row.CharacterIdentifier == characterIdentifier);
    }

    /// <summary>Releases the place a character holds.</summary>
    /// <param name="characterIdentifier">Character whose place is released.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>What happened.</returns>
    public async Task<TournamentCancelOutcome> CancelAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (characterIdentifier <= 0)
        {
            return TournamentCancelOutcome.NotHeld;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var live = await FindLiveRowsAsync(context, cancellationToken);
        var existing = live.FirstOrDefault(row => row.CharacterIdentifier == characterIdentifier);
        if (existing is null)
        {
            return TournamentCancelOutcome.NotHeld;
        }

        // A drawn field is playing: the seed order names this team, and the next
        // round is waiting for it.
        if (await context.TournamentBrackets
                .AnyAsync(
                    bracket => bracket.EventIdentifier == existing.EventIdentifier,
                    cancellationToken))
        {
            return TournamentCancelOutcome.AlreadyFrozen;
        }

        context.TournamentRegistrations.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        return TournamentCancelOutcome.Released;
    }

    /// <summary>
    /// Releases every place an event holds, which is what a decided bracket owes
    /// its field: the entries have all been drawn, so holding them serves nothing
    /// and keeping them would let a finished event refuse the next one a seat.
    /// The frozen seeds and the result ledger are untouched, so the bracket can
    /// still be read back after its places are gone.
    /// </summary>
    /// <param name="eventIdentifier">Event whose field is finished.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Places released.</returns>
    public async Task<int> ReleaseEventAsync(
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (eventIdentifier <= 0)
        {
            return 0;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var rows = await context.TournamentRegistrations
            .Where(registration => registration.EventIdentifier == eventIdentifier)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return 0;
        }

        context.TournamentRegistrations.RemoveRange(rows);
        await context.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    /// <summary>
    /// Returns the places that are still live: those in an event the schedule
    /// still publishes, and those taken inside the current advertised window.
    /// The two answer different questions — the schedule says whether the event
    /// is one anybody may enter at all, the window whether a place taken in an
    /// earlier run of the day's event is still a place in this one — so a place
    /// failing either is not held.
    /// </summary>
    /// <param name="context">Context to read through.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<List<TournamentRegistration>> FindLiveRowsAsync(
        Mgo2DatabaseContext context,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var nowSeconds = now.ToUnixTimeSeconds();
        var open = await context.EventSchedules
            .Where(schedule => schedule.LobbySubtype == EventConstants.TournamentRegistrationSelector)
            .ToListAsync(cancellationToken);
        var openEvents = open
            .Where(schedule => EventScheduleService.IsPublished(schedule, nowSeconds))
            .Select(schedule => schedule.Identifier)
            .ToList();
        if (openEvents.Count == 0)
        {
            return [];
        }
        var (start, end) = informationService.ScheduleFor(now);
        var windowStart = DateTimeOffset.FromUnixTimeSeconds(start);
        var windowEnd = DateTimeOffset.FromUnixTimeSeconds(end);

        return await context.TournamentRegistrations
            .Where(registration => openEvents.Contains(registration.EventIdentifier)
                && registration.ReservedAt >= windowStart
                && registration.ReservedAt < windowEnd)
            .OrderBy(registration => registration.Identifier)
            .ToListAsync(cancellationToken);
    }
}
