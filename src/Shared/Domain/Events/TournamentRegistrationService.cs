using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>Outcome of asking for a Tournament place.</summary>
public enum TournamentReserveOutcome
{
    /// <summary>The character holds the place now.</summary>
    Reserved,

    /// <summary>The character already held a place in this event.</summary>
    AlreadyReserved,

    /// <summary>The character holds a place in a different event.</summary>
    ReservedElsewhere,

    /// <summary>Every place in the event is taken.</summary>
    NoPlacesLeft,

    /// <summary>The character does not exist.</summary>
    CharacterMissing,

    /// <summary>The character's level is outside the configured limits.</summary>
    LevelNotEligible,
}

/// <summary>Outcome of submitting a team for a Tournament event.</summary>
public enum TournamentSubmitOutcome
{
    /// <summary>The team now holds a place in the bracket field.</summary>
    Registered,

    /// <summary>The team already held a place in this event.</summary>
    AlreadyRegistered,

    /// <summary>No such team, or the character is not its leader.</summary>
    NotTheLeader,

    /// <summary>The field is frozen and can no longer accept a team.</summary>
    BracketFrozen,

    /// <summary>A member already plays for another team in this event.</summary>
    MemberRegisteredElsewhere,

    /// <summary>Every team place is taken.</summary>
    TournamentFull,
}

/// <summary>
/// Owns the Tournament places a character holds before it has a team. A
/// reservation is a registration row with no team, which is why the two are one
/// table: a place becomes a registration when a team is submitted, and until
/// then it is the same claim with a null team.
/// <para>
/// A place is live while the event window is open, so ending the window releases
/// every place without a sweep having to run, and a restart cannot lose the
/// claim because the row is the claim.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="informationService">Source of the configured event window.</param>
/// <param name="options">Event configuration.</param>
public sealed class TournamentRegistrationService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
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
        if (characterIdentifier <= 0 || eventIdentifier <= 0)
        {
            return TournamentReserveOutcome.CharacterMissing;
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

        var live = await FindLiveRowsAsync(context, now, cancellationToken);
        var existing = live.FirstOrDefault(row => row.CharacterIdentifier == characterIdentifier);
        if (existing is not null)
        {
            // A retry of the event the character already holds is the same
            // request, not a second place.
            return existing.EventIdentifier == eventIdentifier
                ? TournamentReserveOutcome.AlreadyReserved
                : TournamentReserveOutcome.ReservedElsewhere;
        }

        var capacity = TournamentRegistrationUtils.PlaceCapacity(options.Value);
        if (live.Count >= capacity)
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

        var slot = TournamentRegistrationUtils.NextSlot(live.Select(row => row.SlotIndex), capacity);
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
        if (eventIdentifier <= 0 || teamIdentifier <= 0 || characterIdentifier <= 0)
        {
            return TournamentSubmitOutcome.NotTheLeader;
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

        var capacity = TournamentRegistrationUtils.TeamCapacity(options.Value);
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

    /// <summary>Finds the place a character holds, when the window is open.</summary>
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

        var now = DateTimeOffset.UtcNow;
        await using var context = await CreateContextAsync(cancellationToken);
        var live = await FindLiveRowsAsync(context, now, cancellationToken);
        return live.FirstOrDefault(row => row.CharacterIdentifier == characterIdentifier);
    }

    /// <summary>Releases the place a character holds.</summary>
    /// <param name="characterIdentifier">Character whose place is released.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether a place was released.</returns>
    public async Task<bool> CancelAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (characterIdentifier <= 0)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        await using var context = await CreateContextAsync(cancellationToken);
        var live = await FindLiveRowsAsync(context, now, cancellationToken);
        var existing = live.FirstOrDefault(row => row.CharacterIdentifier == characterIdentifier);
        if (existing is null)
        {
            return false;
        }

        context.TournamentRegistrations.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Counts the places taken in an event.</summary>
    /// <param name="eventIdentifier">Event to count.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<int> CountPlacesAsync(
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await using var context = await CreateContextAsync(cancellationToken);
        var live = await FindLiveRowsAsync(context, now, cancellationToken);
        return live.Count(row => row.EventIdentifier == eventIdentifier);
    }

    /// <summary>
    /// Returns the reservations whose window is still open. The window is the
    /// configured daily one, so a reservation taken yesterday does not hold a
    /// place in today's event.
    /// </summary>
    private async Task<List<TournamentRegistration>> FindLiveRowsAsync(
        Mgo2DatabaseContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var (start, end) = informationService.ScheduleFor(now);
        var startTime = DateTimeOffset.FromUnixTimeSeconds(start);
        var endTime = DateTimeOffset.FromUnixTimeSeconds(end);

        return await context.TournamentRegistrations
            .Where(registration => registration.ReservedAt >= startTime
                && registration.ReservedAt < endTime)
            .OrderBy(registration => registration.Identifier)
            .ToListAsync(cancellationToken);
    }

}
