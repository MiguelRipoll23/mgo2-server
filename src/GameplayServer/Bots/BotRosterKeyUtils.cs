namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The key a bot's roster entry is filed under.
/// </summary>
/// <remarks>
/// <para>
/// <b>A bot has no endpoint, and the roster is keyed by endpoint.</b> Filing a
/// bot under an address it does not have would make it indistinguishable from a
/// peer that is merely quiet, and the session reaper would eventually take it off
/// the roster. The key is therefore a name in a space no endpoint can occupy —
/// <c>bot/</c> and the character name — which is also what lets
/// <see cref="Rooms.BriefingStateService"/> keep a bot's ready state beside a
/// player's without the two ever colliding.
/// </para>
/// <para>
/// It never goes on the wire: the bot's roster record carries the short layout
/// with no endpoint pairs, which is the layout the recorded hosts' own
/// <c>BOT</c> entries carry.
/// </para>
/// </remarks>
public static class BotRosterKeyUtils
{
    private const string Prefix = "bot/";

    /// <summary>The roster key of a bot with a name.</summary>
    /// <param name="name">The bot's character name.</param>
    /// <returns>The key the bot is filed under.</returns>
    public static string KeyFor(string name) => $"{Prefix}{name}";

    /// <summary>Whether a roster key names a bot rather than a peer.</summary>
    /// <param name="key">Roster key to test.</param>
    /// <returns><c>true</c> for a key this utility makes.</returns>
    public static bool IsBotKey(string? key) =>
        key is not null && key.StartsWith(Prefix, StringComparison.Ordinal);
}
