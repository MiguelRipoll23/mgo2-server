using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>
/// The room's own figures as the game details reply states them: its briefing time,
/// its level-limit base and its seventeen per-rule words, read from the host's
/// settings row.
/// </summary>
/// <remarks>
/// <para>
/// <b>They are the host's row rather than this server's opinion.</b> A player-hosted
/// room's settings arrive as the client's own push and are stored there; a dedicated
/// host states the same fields at startup from its own configuration. Either way one
/// row is the source, which is why the reply reads it instead of writing the zeros an
/// absent row leaves behind.
/// </para>
/// <para>
/// <b>A zero is a figure and not an absence.</b> The reply's briefing time is the
/// number of minutes a browsing client is told the room briefs for, and the per-rule
/// words are where it reads a round's own length, so a room that has both must state
/// them: a room that states zero is one whose briefing and round last no time at all.
/// </para>
/// </remarks>
public static class GameDetailsSettingsUtils
{
    /// <summary>Number of per-rule words the settings block carries.</summary>
    public const int RuleTimerCount = 17;

    /// <summary>Minutes the room's briefing lasts.</summary>
    /// <param name="settings">The host's settings row, or <c>null</c> when it has none.</param>
    /// <returns>The row's briefing time, or zero for a host that states none.</returns>
    public static int BriefingMinutes(CharacterHostSettings? settings) =>
        settings?.BriefingTime ?? 0;

    /// <summary>The room's level-limit base.</summary>
    /// <param name="settings">The host's settings row, or <c>null</c> when it has none.</param>
    /// <returns>The row's base, or zero for a host that states none.</returns>
    public static int LevelLimitBase(CharacterHostSettings? settings) =>
        settings?.LevelLimitBase ?? 0;

    /// <summary>
    /// The room's per-rule times, rounds and tickets, in the order the reply states
    /// them, padded with zeros to the block's full width.
    /// </summary>
    /// <remarks>
    /// The parser reads all seventeen words unconditionally, so a row that stores
    /// fewer is answered with the rest zero rather than with a shorter block — the
    /// length is the reply's, not the row's.
    /// </remarks>
    /// <param name="settings">The host's settings row, or <c>null</c> when it has none.</param>
    /// <returns>The seventeen words.</returns>
    public static int[] RuleTimers(CharacterHostSettings? settings)
    {
        var timers = new int[RuleTimerCount];
        var stored = settings?.RuleTimers;

        if (stored is not null)
        {
            Array.Copy(stored, timers, Math.Min(stored.Length, RuleTimerCount));
        }

        return timers;
    }
}
