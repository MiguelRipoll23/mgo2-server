namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Assembles the entry the game-entry information screen shows for one
/// character: the room it was assigned to, or the Tournament place it holds
/// while there is no room yet.
/// <para>
/// One character holds at most one entry here, because a character holds one team
/// and one place. A character that reserved a place and was then paired has both
/// rows, and the assignment is the entry: the reservation is the same claim in an
/// earlier state, not a second game to open.
/// </para>
/// <para>
/// A reservation has no room and no lobby of its own, so the entry names the
/// lobby the character is asking from. That is the registration lobby while the
/// place is held, which is the only lobby a reserved character can be in.
/// </para>
/// </summary>
/// <param name="registrationService">Service that owns the reserved places.</param>
/// <param name="assignmentService">Service that owns the assigned games.</param>
/// <param name="teamService">Service that owns the teams, read for the entry's name.</param>
public sealed class EventGameEntryService(
    TournamentRegistrationService registrationService,
    EventAssignmentService assignmentService,
    EventTeamService teamService)
{
    /// <summary>Returns the entries a character holds, at most one.</summary>
    /// <param name="characterIdentifier">Character the screen is for.</param>
    /// <param name="teamIdentifier">Team the session is attached to, or zero when it is in none.</param>
    /// <param name="lobbyIdentifier">Lobby the character is asking from.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<EventGameEntry>> ListForCharacterAsync(
        int characterIdentifier,
        int teamIdentifier,
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (characterIdentifier <= 0)
        {
            return [];
        }

        if (teamIdentifier > 0)
        {
            var assignment = await assignmentService.FindByTeamAsync(teamIdentifier, cancellationToken);
            var team = assignment?.TeamOfCharacter(characterIdentifier);
            if (assignment is not null && team is not null)
            {
                return
                [
                    new EventGameEntry(
                        characterIdentifier,
                        team.Name,
                        team.SnapshotIdentifier,
                        assignment.LobbyIdentifier),
                ];
            }
        }

        var reservation = await registrationService.FindLiveAsync(characterIdentifier, cancellationToken);
        if (reservation is null)
        {
            return [];
        }

        var name = string.Empty;
        if (reservation.TeamIdentifier is { } reservedTeam && reservedTeam > 0)
        {
            var team = await teamService.FindAsync(reservedTeam, cancellationToken);
            name = team?.Name ?? string.Empty;
        }

        return
        [
            new EventGameEntry(
                characterIdentifier,
                name,
                reservation.TeamIdentifier ?? 0,

                // The lobby the character is asking from, because a place in an
                // event that has not been paired is played in the lobby the place
                // was taken in.
                lobbyIdentifier),
        ];
    }
}
