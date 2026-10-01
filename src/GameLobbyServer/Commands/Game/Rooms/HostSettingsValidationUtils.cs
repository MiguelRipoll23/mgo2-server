using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>
/// The rules a pushed host-settings block has to pass before it is stored.
/// <para>
/// They are the reference's own checks on the settings request, and they are here
/// rather than in the handler because a block that is refused is never stored, and
/// a stored block is what a later create-room request builds a room from: a check
/// that lived in the create handler would let the bad block in and only refuse it
/// once it had been written.
/// </para>
/// </summary>
public static class HostSettingsValidationUtils
{
    /// <summary>Lowest mode the client's settings screen may name.</summary>
    public const int MinimumSubtype = 1;

    /// <summary>Highest mode the client's settings screen may name.</summary>
    public const int MaximumSubtype = 9;

    /// <summary>Fewest players a room may be created to hold.</summary>
    public const int MinimumPlayers = 1;

    /// <summary>Most players a room may be created to hold.</summary>
    public const int MaximumPlayers = 17;

    /// <summary>
    /// Whether a block may be stored at all. Every rule is one refusal rather than
    /// a list of reasons, because the answer the client is given is one code.
    /// </summary>
    /// <param name="settings">Block the client pushed, already decoded.</param>
    public static bool IsAcceptable(CharacterHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return IsSupportedSubtype(settings.SettingsLobbySubtype)
            && IsSupportedPlayerCount(settings.MaxPlayers)
            && HasRotation(settings);
    }

    /// <summary>Whether a mode is one the host settings screen can name.</summary>
    /// <param name="subtype">Mode named by the block.</param>
    public static bool IsSupportedSubtype(int subtype) =>
        subtype is >= MinimumSubtype and <= MaximumSubtype;

    /// <summary>Whether a player count is one a room may be created with.</summary>
    /// <param name="maximumPlayers">Players the block asks the room to hold.</param>
    public static bool IsSupportedPlayerCount(int maximumPlayers) =>
        maximumPlayers is >= MinimumPlayers and <= MaximumPlayers;

    /// <summary>
    /// Whether the block names at least one game. A room with an empty rotation has
    /// nothing to play, so it is refused rather than created and left unplayable.
    /// </summary>
    /// <param name="settings">Block the client pushed.</param>
    public static bool HasRotation(CharacterHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var rules = settings.RotationRules;
        var maps = settings.RotationMaps;
        if (rules is null || maps is null)
        {
            return false;
        }

        for (var index = 0; index < Math.Min(rules.Length, maps.Length); index++)
        {
            if (rules[index] != 0 || maps[index] != 0)
            {
                return true;
            }
        }

        return false;
    }
}
