using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Options;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>What entering an event as an individual produced.</summary>
public enum EventEntryOutcome
{
    /// <summary>The character entered the event.</summary>
    Entered,

    /// <summary>The character already held an entry, so nothing changed.</summary>
    AlreadyEntered,

    /// <summary>The event is not one this server serves, or its window is shut.</summary>
    EventNotFound,

    /// <summary>The character does not exist.</summary>
    CharacterMissing,

    /// <summary>The character's level is outside the configured limits.</summary>
    LevelNotEligible,

    /// <summary>The character owns a formed team, which enters as a team instead.</summary>
    InTeam,

    /// <summary>The event's field is full.</summary>
    FieldFull,
}

/// <summary>Outcome of an entry and the entrant it belongs to.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="TeamIdentifier">Entrant team the character belongs to, when there is one.</param>
public readonly record struct EventEntryResult(EventEntryOutcome Outcome, int TeamIdentifier);

/// <summary>
/// Enters one character into an event. This is the individual path a Tournament
/// or Survival lobby takes when an event's detail screen is open, and it is
/// distinct from the team path a registration lobby takes: there the unit is a
/// formed roster, and here it is the player alone.
/// <para>
/// An entrant in this server is a team, because teams are what get paired, paid
/// and pushed. An individual entry is therefore a team of one, owned by the
/// entering character and named after it — the same single entrant slot the
/// client was told about, in the shape this server can play.
/// </para>
/// <para>
/// The entry is a commitment, so the entrant's own slot is marked ready as part
/// of it: the individual path has no separate decision step, and a queued team
/// that is not ready would be removed from the field instead of entering it.
/// </para>
/// </summary>
/// <param name="teamService">Service that owns the entrant teams.</param>
/// <param name="submissionService">Service that submits a team into the Tournament field.</param>
/// <param name="matchmakingService">Service that pairs the Survival field.</param>
/// <param name="characterService">Service that owns the characters entering.</param>
/// <param name="informationService">Source of the configured event window.</param>
/// <param name="options">Event configuration, which carries the level limits.</param>
public sealed class EventEntryService(
    EventTeamService teamService,
    TournamentSubmissionService submissionService,
    EventMatchmakingService matchmakingService,
    CharacterService characterService,
    EventInformationService informationService,
    IOptions<EventOptions> options)
{
    /// <summary>Enters one character into an event.</summary>
    /// <param name="characterIdentifier">Character entering.</param>
    /// <param name="eventIdentifier">Event being entered.</param>
    /// <param name="lobbyIdentifier">Lobby the entry is made in.</param>
    /// <param name="lobbySubtype">Game type of that lobby, which selects the field.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventEntryResult> EnterAsync(
        int characterIdentifier,
        int eventIdentifier,
        int lobbyIdentifier,
        int lobbySubtype,
        CancellationToken cancellationToken = default)
    {
        if (characterIdentifier <= 0)
        {
            return new EventEntryResult(EventEntryOutcome.CharacterMissing, 0);
        }

        if (eventIdentifier != EventConstants.TransientEventIdentifier
            || !IsWindowOpen(DateTimeOffset.UtcNow))
        {
            // An entry into an event that is not running is one the server
            // cannot honour, and reporting success would say it had.
            return new EventEntryResult(EventEntryOutcome.EventNotFound, 0);
        }

        var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
        if (character is null)
        {
            return new EventEntryResult(EventEntryOutcome.CharacterMissing, 0);
        }

        var level = LevelUtils.CalculateLevel(character.Experience);
        if (!TournamentRegistrationUtils.IsLevelEligible(level, options.Value))
        {
            return new EventEntryResult(EventEntryOutcome.LevelNotEligible, 0);
        }

        var existing = await teamService.FindOwnedInLobbyAsync(
            characterIdentifier,
            lobbyIdentifier,
            cancellationToken);
        var teamIdentifier = existing?.Identifier ?? 0;
        if (existing is null)
        {
            var team = await teamService.CreateAsync(
                characterIdentifier,
                character.Name,
                character.Experience,
                character.Name,
                comment: string.Empty,
                flagBits: 0,
                password: string.Empty,
                matchType: lobbySubtype,
                lobbyIdentifier,
                eventIdentifier,
                cancellationToken);
            teamIdentifier = team.Identifier;
        }
        else if (!EventEntryUtils.IsReusableEntrantTeam(
                existing.Members.Count(member => member.CharacterIdentifier != 0),
                existing.EventIdentifier,
                eventIdentifier))
        {
            // A formed roster is entered as a team through its own screens, and a
            // team of this character's already in another event is that event's
            // entrant. Either way, a second entrant for one character is not
            // something this path may create.
            return new EventEntryResult(EventEntryOutcome.InTeam, existing.Identifier);
        }

        await teamService.SetDecisionAsync(teamIdentifier, characterIdentifier, 1, cancellationToken);

        return lobbySubtype == EventConstants.TournamentSelector
            ? await EnterTournamentAsync(characterIdentifier, eventIdentifier, teamIdentifier, cancellationToken)
            : await EnterSurvivalAsync(teamIdentifier, cancellationToken);
    }

    /// <summary>Whether the configured event window is open at one moment.</summary>
    /// <param name="now">Moment to test.</param>
    private bool IsWindowOpen(DateTimeOffset now)
    {
        var (start, end) = informationService.ScheduleFor(now);
        return EventEntryUtils.IsWindowOpen(
            now.ToUnixTimeSeconds(),
            start,
            end);
    }

    private async Task<EventEntryResult> EnterTournamentAsync(
        int characterIdentifier,
        int eventIdentifier,
        int teamIdentifier,
        CancellationToken cancellationToken)
    {
        var outcome = await submissionService.SubmitTeamAsync(
            eventIdentifier,
            teamIdentifier,
            characterIdentifier,
            cancellationToken);

        return outcome switch
        {
            TournamentSubmitOutcome.Registered =>
                new EventEntryResult(EventEntryOutcome.Entered, teamIdentifier),
            TournamentSubmitOutcome.AlreadyRegistered =>
                new EventEntryResult(EventEntryOutcome.AlreadyEntered, teamIdentifier),
            TournamentSubmitOutcome.TournamentFull =>
                new EventEntryResult(EventEntryOutcome.FieldFull, teamIdentifier),
            _ => new EventEntryResult(EventEntryOutcome.EventNotFound, teamIdentifier),
        };
    }

    private async Task<EventEntryResult> EnterSurvivalAsync(
        int teamIdentifier,
        CancellationToken cancellationToken)
    {
        // The team is the character's own and its only member was just marked
        // ready, so it is either queued, paired, or already one of the two —
        // there is no third state this can be read as.
        var matchmaking = await matchmakingService.ReconcileAsync(teamIdentifier, cancellationToken);
        var outcome = matchmaking.Status == MatchmakingStatus.Unchanged
            ? EventEntryOutcome.AlreadyEntered
            : EventEntryOutcome.Entered;

        return new EventEntryResult(outcome, teamIdentifier);
    }
}
