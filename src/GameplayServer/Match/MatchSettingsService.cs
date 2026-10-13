using Mgo2Server.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// Resolves what this host's room is running, from this instance's own settings.
/// </summary>
/// <remarks>
/// <para>
/// <b>The room is asked for here rather than by each caller.</b> "Which room is
/// mine" has one answer and it lives on <see cref="MatchService"/>: a host that has
/// not published its room yet has no round to open and no rotation to write into a
/// joiner's settings burst, and every writer of the channel asks this before it
/// writes. Nothing is read from the database to answer it — the settings are this
/// instance's own (<see cref="MatchSettingsUtils"/>) — so the gate is the published
/// room and not a query, and a caller pays no round trip to learn what it already
/// knows.
/// </para>
/// <para>
/// A room that cannot be resolved is answered with <c>null</c> and never with a
/// default: a host that opened a round on a guess would play a rule or a map the
/// client was not told about, which is worse than a round that does not open.
/// </para>
/// </remarks>
/// <param name="match">Publisher of this host's room, which names the room to serve.</param>
/// <param name="options">Options of this instance, which state what the room runs.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class MatchSettingsService(
    MatchService match,
    IOptions<ServerOptions> options,
    ILogger<MatchSettingsService> logger)
{
    private readonly ServerOptions options = options.Value;

    /// <summary>
    /// The settings of the room this host publishes, which is the room every writer
    /// of this host's channel is answering for.
    /// </summary>
    /// <returns>The room's settings, or <c>null</c> while it has none published.</returns>
    public MatchSettings? FindPublished()
    {
        var matchIdentifier = match.MatchIdentifier;
        if (matchIdentifier == 0)
        {
            logger.LogWarning(
                "This host has not published its room yet, so what its room runs is not known");
            return null;
        }

        var settings = MatchSettingsUtils.From(
            options,
            matchIdentifier,
            options.GameplayServerRoomName);
        logger.LogDebug(
            "Room {GameIdentifier} ({GameName}) runs {RoundCount} round(s) with a {BriefingTime}-minute "
            + "briefing, from this instance's own settings",
            settings.GameIdentifier,
            settings.Name,
            settings.Rotation.Count,
            settings.BriefingTimeMinutes);
        return settings;
    }
}
