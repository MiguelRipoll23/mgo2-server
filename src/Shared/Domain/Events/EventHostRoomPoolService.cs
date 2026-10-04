using Microsoft.Extensions.Options;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The rooms a match may be hosted in, read from the one place they live: the
/// games table, and the one place the choice of host is made.
/// <para>
/// It is a service of its own because "which room can host this match" is asked
/// from more than one place, and the answer was once assembled in each of them.
/// A reader that asked a slightly different question found a host available, or
/// found none, depending on which reader it was. Both the assignment that leases
/// a room and the readers that report which room is free ask here now.
/// </para>
/// <para>
/// The rooms are deliberately not narrowed by lobby. A dedicated host is a room
/// rented for a role, and the role sets the room's mode when it is created, so a
/// host opened in another lobby is still the host the match needs.
/// </para>
/// <para>
/// The one rule that is the deployment's to state is whether a host has to be
/// running the event's own settings. It is <see cref="EventOptions.RequireHostSettingsMatch"/>
/// and it is off unless it is asked for, so the setting is read once here and
/// every reader of the choice applies it rather than each deciding for itself.
/// </para>
/// </summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="characterService">Service that owns the characters' host settings.</param>
/// <param name="options">Event configuration, for the rule about the room's own settings.</param>
public sealed class EventHostRoomPoolService(
    GameService gameService,
    CharacterService characterService,
    IOptions<EventOptions> options)
{
    /// <summary>
    /// Environment a room must be running, or null when any room may host. The
    /// environment is the shipping preset the event screens advertise, so what a
    /// room has to match is what the client was told the event runs. One preset
    /// serves both roles: the Survival and Tournament hosts are told the same
    /// settings, so the same environment is asked of either.
    /// </summary>
    private readonly EventHostEnvironment? requiredSettings =
        options.Value.RequireHostSettingsMatch ? EventHostEnvironment.CreateDefault() : null;

    /// <summary>Every room a match could be hosted in.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The rooms, each with the roster the idle rule reads.</returns>
    public async Task<List<Game>> ListAsync(CancellationToken cancellationToken = default) =>
        await gameService.FindHostRoomCandidatesAsync(cancellationToken);

    /// <summary>
    /// Picks the room that hosts a match out of a candidate list, applying the
    /// rule about the room's own settings that the deployment stated.
    /// <para>
    /// What a room is running is its host's saved settings, so the hosts of the
    /// candidates are read once here rather than one room at a time inside the
    /// rule. A host that has never pushed its settings is simply absent from the
    /// map, and the rule treats an absent host as one that has nothing to offer.
    /// </para>
    /// </summary>
    /// <param name="rooms">Rooms to choose from, as <see cref="ListAsync"/> returned them.</param>
    /// <param name="matchType">Mode of the match.</param>
    /// <param name="participantCount">Players the match brings.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The first room that may take the match, or null when none may.</returns>
    public async Task<Game?> FindHostAsync(
        List<Game> rooms,
        int matchType,
        int participantCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rooms);

        var hostSettings = await characterService.FindHostSettingsAsync(
            rooms.Select(room => room.HostIdentifier),
            cancellationToken);

        return SelectHost(rooms, matchType, participantCount, hostSettings, requiredSettings);
    }

    /// <summary>
    /// Picks the room that hosts a match out of a candidate list.
    /// </summary>
    /// <param name="rooms">Rooms to choose from, as <see cref="ListAsync"/> returned them.</param>
    /// <param name="matchType">Mode of the match.</param>
    /// <param name="participantCount">Players the match brings.</param>
    /// <param name="hostSettings">Saved settings of each candidate's host, keyed by character.</param>
    /// <param name="requiredSettings">
    /// Environment the room's own settings must match, or null when any room may host.
    /// Passed in rather than read from the service so the rule can be exercised
    /// without one, and so a reader can see which answer it is asking for.
    /// </param>
    /// <returns>The first room that may take the match, or null when none may.</returns>
    public static Game? SelectHost(
        List<Game> rooms,
        int matchType,
        int participantCount,
        IReadOnlyDictionary<int, CharacterHostSettings> hostSettings,
        EventHostEnvironment? requiredSettings)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(hostSettings);

        return rooms.FirstOrDefault(room =>
            EventHostEligibilityUtils.IsEligibleHost(
                room,
                matchType,
                participantCount,
                hostSettings.GetValueOrDefault(room.HostIdentifier),
                requiredSettings));
    }
}
