namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The navigation provider this server runs with: it can route nothing, and says
/// so with a reason rather than a path.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a real implementation of the provider and not a placeholder.</b> What
/// it implements is the answer "no data exists", which is the truthful answer for
/// every map today, and a bot's movement decision reads it: the decision stays a
/// decision and no coordinate is invented for it to walk to.
/// </para>
/// <para>
/// When a map's data is read — sampled from replays or from the client — it
/// arrives through an <see cref="IBotMapNavigationDataProvider"/>, and the
/// routing provider implemented against it replaces this one in the container.
/// Nothing else in the bot system changes.
/// </para>
/// </remarks>
public sealed class UnavailableBotNavigationProvider
    : IBotNavigationProvider, IBotMapNavigationDataProvider
{
    /// <summary>The reason every route is refused with.</summary>
    public const string Reason =
        "No navigation data is verified for this map, so no route can be found.";

    /// <inheritdoc />
    public BotNavigationResult FindPath(BotNavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return BotNavigationResult.Unsupported(Reason);
    }

    /// <inheritdoc />
    public BotMapNavigationData Read(int map) => BotMapNavigationData.None(map);
}
