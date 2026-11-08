namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The extension point a map's navigation data arrives through.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is an interface with no data behind it, and that is the state of the
/// evidence.</b> No capture and no asset read for this server states where a map's
/// walkable ground is: the position records in the operations stream say where a
/// player stood, and a set of points is not a navigation mesh. A provider is the
/// seam a later reading plugs into, and every implementation today answers that
/// it has nothing.
/// </para>
/// </remarks>
public interface IBotNavigationProvider
{
    /// <summary>
    /// Routes a request, or says why it cannot.
    /// </summary>
    /// <param name="request">Where the route starts and ends.</param>
    /// <returns>The route, or a refusal that is not a path.</returns>
    BotNavigationResult FindPath(BotNavigationRequest request);
}

/// <summary>
/// The seam a map's own navigation data is read through.
/// </summary>
/// <remarks>
/// It is separate from <see cref="IBotNavigationProvider"/> so a map can be given
/// data without a routing implementation existing yet: the data says what the map
/// is, and the provider says how to walk it.
/// </remarks>
public interface IBotMapNavigationDataProvider
{
    /// <summary>
    /// What is known about a map's walkable ground.
    /// </summary>
    /// <param name="map">Map, in the replay decoder's numbering.</param>
    /// <returns>The data, which states its own absence when there is none.</returns>
    BotMapNavigationData Read(int map);
}

/// <summary>
/// One map's navigation data, or the statement that it has none.
/// </summary>
/// <param name="Map">Map the data is for.</param>
/// <param name="HasData">Whether any walkable ground is known for the map.</param>
/// <param name="Reason">Why the data is or is not there.</param>
public sealed record BotMapNavigationData(int Map, bool HasData, string Reason)
{
    /// <summary>The reading for a map nothing has been measured on.</summary>
    /// <param name="map">Map, in the replay decoder's numbering.</param>
    /// <returns>Data that states it has none.</returns>
    public static BotMapNavigationData None(int map) => new(
        map,
        false,
        "No navigation data is verified for this map; no mesh, waypoint set or geometry has been read for it.");
}

/// <summary>
/// The extension point that keeps a bot off obstacles once paths exist.
/// </summary>
/// <remarks>
/// <b>A path and the walk along it are different problems.</b> A route can be
/// correct and still run a bot into another player, a door or a moving object, so
/// the adjustment is a seam of its own rather than part of the router. It has one
/// implementation today — none — and a destination passes through unchanged.
/// </remarks>
public interface IBotObstacleAvoidance
{
    /// <summary>
    /// Adjusts a destination for what the bot can see in the way.
    /// </summary>
    /// <param name="destination">Where the bot means to go.</param>
    /// <param name="path">The route it would take, or <c>null</c> when it has none.</param>
    /// <returns>The destination to walk to.</returns>
    BotMovementDestination Adjust(BotMovementDestination destination, BotPath? path);
}
