using Mgo2Server.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// The map this process's room plays: the map the last start left behind, one
/// along, or the configured map when there is no start behind it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The map rotates at every start, and one object states it.</b> Two readers
/// have to agree on the map a round plays: the room row the lobby hands a client,
/// written at startup (<see cref="MatchService"/>), and every record the gameplay
/// channel writes, built from <see cref="MatchSettingsService"/>. A map advanced in
/// one of them and not the other is a room whose row names one stage while its
/// round plays another, so the advance happens once, here, and both read it.
/// </para>
/// <para>
/// <b>Where the previous start is read from.</b> The room row's own rotation is the
/// only record a restart has: the container's filesystem is the pod's, so a file
/// would be a rotation that starts over on every start
/// (<see cref="MatchSettingsUtils.StoredMap"/>). The configured
/// <see cref="ServerOptions.GameplayServerMap"/> is therefore the map of a room
/// that has no row behind it — the first start on a fresh database — and not the
/// map of every start.
/// </para>
/// </remarks>
/// <param name="options">Options of this instance, which state the first map.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class MatchMapRotationService(
    IOptions<ServerOptions> options,
    ILogger<MatchMapRotationService> logger)
{
    private readonly int configured = MatchMapUtils.Parse(options.Value.GameplayServerMap);

    private int map;

    /// <summary>
    /// The map this process's room plays. It is the configured map until a start
    /// has advanced it, so a reader is never answered a zero.
    /// </summary>
    public int Map => map == 0 ? configured : map;

    /// <summary>
    /// States the map this process's room plays, which the startup advance decides.
    /// </summary>
    /// <remarks>
    /// It is written once per process, before the room is published, and every
    /// reader below runs after that: a room that is not published yet answers
    /// <c>null</c> from <see cref="MatchSettingsService.FindPublished"/>, so no
    /// record this host writes can be built on the map before it is stated.
    /// </remarks>
    /// <param name="selected">Map id the room plays.</param>
    public void Select(int selected)
    {
        map = selected;
        logger.LogInformation(
            "This start's room plays map {Map} ({MapName}); the map advances at every start",
            selected,
            MatchMapUtils.Name(selected) ?? "unnamed");
    }
}
