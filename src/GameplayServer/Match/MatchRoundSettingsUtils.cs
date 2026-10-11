using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// Reads the rounds a gameplay host's room advertises out of the settings its
/// host character saved, which is where the room's rule lives.
/// </summary>
/// <remarks>
/// <para>
/// A gameplay host's room runs the client's Free Battle, whose rule is team
/// deathmatch (<c>MatchService.FreeBattleRule</c>), so the round limit is that
/// rule's round word in the settings block the lobby and the room both read
/// (<see cref="EventHostSettingsEnvironmentUtils"/>). Reading it from the saved
/// settings rather than a value of this server's own keeps the room's behaviour
/// and the room list advertising the same rule.
/// </para>
/// <para>
/// Zero is the settings block's own "no rounds set", not a limit of none: the
/// dedicator's settings start with every rule word zeroed, so it is also what an
/// unconfigured host reports. It is returned as-is and the round service reads
/// zero as no limit, which is the shape every capture has — its rounds end on a
/// goal or a boundary and nothing stops the match. A configured limit is what
/// ends it.
/// </para>
/// </remarks>
public static class MatchRoundSettingsUtils
{
    /// <summary>
    /// The round limit a host's saved settings state, or zero when the host has
    /// no settings row or no round word set.
    /// </summary>
    /// <param name="settings">Rows saved for the host character.</param>
    /// <returns>Rounds the room plays, or zero for no limit.</returns>
    public static int MaximumRounds(IEnumerable<CharacterHostSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var row = settings.FirstOrDefault(candidate => candidate.Type == HostSettingsType.Value);
        if (row is null)
        {
            return 0;
        }

        return EventHostSettingsEnvironmentUtils.ToEnvironment(row).TeamDeathmatchRounds;
    }
}
