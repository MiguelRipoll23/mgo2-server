using System.Text.Json;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The rules that decide which room may host an event match.
/// <para>
/// A room hosts an event only if it was created to: it is named for the role and
/// it says it is dedicated. Without that, any player's room would be eligible and
/// a Survival match could be dropped into somebody's private game — which is why
/// the name and the flag are both required rather than either.
/// </para>
/// </summary>
public static class EventHostEligibilityUtils
{
    /// <summary>Name a room carries to be a Survival host.</summary>
    public const string SurvivalHostName = "SURVIVAL_HOST";

    /// <summary>Name a room carries to be a Tournament host.</summary>
    public const string TournamentHostName = "TOURNAMENT_HOST";

    /// <summary>Players a Survival match itself may hold.</summary>
    public const int MatchPlayerCapacity = 16;

    /// <summary>Slots the dedicated host consumes beyond the players it hosts.</summary>
    public const int DedicatedHostPlayerSlots = 1;

    /// <summary>
    /// Whether a room is a dedicated event host. The name is compared
    /// case-insensitively because the client sends it as typed, and the flag is
    /// read from the room's own settings rather than inferred.
    /// </summary>
    /// <param name="name">Name of the room.</param>
    /// <param name="common">Room settings blob.</param>
    public static bool IsDedicatedEventHost(string? name, string? common)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var matchesRole =
            string.Equals(name, SurvivalHostName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, TournamentHostName, StringComparison.OrdinalIgnoreCase);
        return matchesRole && IsDedicated(common);
    }

    /// <summary>Reads the dedicated flag out of the room settings.</summary>
    /// <param name="common">Room settings blob, or null.</param>
    public static bool IsDedicated(string? common)
    {
        if (string.IsNullOrWhiteSpace(common))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(common);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("dedicated", out var dedicated)
                && dedicated.ValueKind is JsonValueKind.True;
        }
        catch (JsonException)
        {
            // A settings blob that cannot be read is not evidence of a dedicated
            // room, so the room is left out rather than guessed at.
            return false;
        }
    }

    /// <summary>
    /// Whether every player in the room is the host and the host is there. A
    /// host that has already collected players is running a game, and a host that
    /// is absent cannot start one.
    /// </summary>
    /// <param name="hostIdentifier">Character hosting the room.</param>
    /// <param name="playerCharacterIdentifiers">Characters in the room.</param>
    public static bool IsIdle(int hostIdentifier, IEnumerable<int> playerCharacterIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(playerCharacterIdentifiers);

        var hostPresent = false;
        foreach (var characterIdentifier in playerCharacterIdentifiers)
        {
            if (characterIdentifier == hostIdentifier)
            {
                hostPresent = true;
                continue;
            }

            if (characterIdentifier != 0)
            {
                return false;
            }
        }

        return hostPresent;
    }

    /// <summary>
    /// Whether the room has room for the match, counting the dedicated host's own
    /// slot. A match that fits the players but not the host would start and then
    /// find nowhere to seat the host.
    /// </summary>
    /// <param name="maximumPlayers">Players the room holds.</param>
    /// <param name="participantCount">Players the match brings.</param>
    public static bool HasCapacity(int maximumPlayers, int participantCount) =>
        participantCount >= 0
        && participantCount <= MatchPlayerCapacity
        && participantCount + DedicatedHostPlayerSlots <= maximumPlayers;

    /// <summary>
    /// Whether a room may take a specific match: the mode has to agree, because a
    /// host is only dedicated to one, and the room has to seat everyone including
    /// itself.
    /// </summary>
    /// <param name="gameLobbySubtype">Mode of the room's lobby.</param>
    /// <param name="matchType">Mode of the match.</param>
    /// <param name="maximumPlayers">Players the room holds.</param>
    /// <param name="participantCount">Players the match brings.</param>
    public static bool AcceptsMatch(
        int gameLobbySubtype,
        int matchType,
        int maximumPlayers,
        int participantCount) =>
        gameLobbySubtype == matchType && HasCapacity(maximumPlayers, participantCount);
}
