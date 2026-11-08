namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The outcome of a navigation request, including the outcomes that are not a
/// path.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Unsupported"/> is the honest answer while no map's navigation
/// data exists.</b> A provider that cannot route a request says so and returns no
/// path; it does not return a straight line to the destination dressed as a route,
/// because a bot that walks through a wall is worse than a bot that stands still
/// and says it cannot move.
/// </para>
/// <para>
/// <see cref="Unavailable"/> is the same refusal for a different reason: there is
/// nothing to route over yet — no data provider, no origin — rather than a
/// provider that does not support the request.
/// </para>
/// </remarks>
public enum BotNavigationStatus
{
    /// <summary>A route was found.</summary>
    Succeeded,

    /// <summary>A route is being followed and has not finished.</summary>
    InProgress,

    /// <summary>The request could not be routed.</summary>
    Failed,

    /// <summary>Navigation data for the map does not exist yet.</summary>
    Unavailable,

    /// <summary>The provider does not support this kind of request.</summary>
    Unsupported,
}

/// <summary>
/// A point on a map, in the game's own coordinates.
/// </summary>
/// <remarks>
/// It is a value the operations stream carries for a player's position (the
/// position records the client sends), not a coordinate this server invents. No
/// point is produced anywhere in the bot system yet, because no source here
/// states one for a bot.
/// </remarks>
/// <param name="X">First coordinate.</param>
/// <param name="Y">Second coordinate.</param>
/// <param name="Z">Third coordinate.</param>
public sealed record BotPosition(float X, float Y, float Z);

/// <summary>
/// A route: the points a bot would walk through, in order.
/// </summary>
/// <remarks>An empty path is a route of nothing, which a successful result never is.</remarks>
/// <param name="Waypoints">The points to walk through, destination last.</param>
public sealed record BotPath(IReadOnlyList<BotPosition> Waypoints)
{
    /// <summary>Whether the path names no point at all.</summary>
    public bool IsEmpty => Waypoints.Count == 0;
}

/// <summary>
/// A request for a route across a map.
/// </summary>
/// <param name="Map">Map the route is for, in the replay decoder's numbering.</param>
/// <param name="From">Where the bot is, or <c>null</c> while no position is known.</param>
/// <param name="To">Where the bot wants to be.</param>
/// <param name="Rule">The round's rule, which picks the placement a bot without a position starts from.</param>
public sealed record BotNavigationRequest(int Map, BotPosition? From, BotPosition To, int Rule = 0);

/// <summary>
/// The answer to a navigation request.
/// </summary>
/// <param name="Status">What the provider could do.</param>
/// <param name="Path">The route, or <c>null</c> when there is none.</param>
/// <param name="Reason">Why the status is what it is, in prose.</param>
public sealed record BotNavigationResult(BotNavigationStatus Status, BotPath? Path, string Reason)
{
    /// <summary>A result with no route because the map has no navigation data.</summary>
    /// <param name="reason">Why no data exists.</param>
    /// <returns>The result.</returns>
    public static BotNavigationResult Unavailable(string reason) =>
        new(BotNavigationStatus.Unavailable, null, reason);

    /// <summary>A result with no route because the request is not supported.</summary>
    /// <param name="reason">What the provider cannot do.</param>
    /// <returns>The result.</returns>
    public static BotNavigationResult Unsupported(string reason) =>
        new(BotNavigationStatus.Unsupported, null, reason);
}

/// <summary>
/// Where a bot would move to, or why it has nowhere to go.
/// </summary>
/// <param name="Position">The destination, or <c>null</c> while none is known.</param>
/// <param name="Reason">Why the destination is what it is.</param>
public sealed record BotMovementDestination(BotPosition? Position, string Reason);
