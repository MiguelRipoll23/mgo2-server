using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Routes a bot over the map's extracted stage cells, which this build ships.
/// </summary>
/// <remarks>
/// <para>
/// <b>The server reads no stage file.</b> The data is an embedded resource written
/// by <c>tools/stage-navigation</c>, so routing costs a lookup and a grid over
/// cells this build already holds. A map the build has data for routes; a map it
/// does not is refused with a sentence naming the stage, which is an answer a
/// decision can act on.
/// </para>
/// <para>
/// The one thing this provider never returns is a straight line to the
/// destination. A route is a chain of extracted cells, and a request those cells
/// cannot join is <see cref="BotNavigationStatus.Failed"/> with the reason.
/// </para>
/// </remarks>
/// <param name="catalog">The build's stage navigation data.</param>
/// <param name="logger">Logger of this provider.</param>
public sealed class StageBotNavigationProvider(
    StageNavigationCatalogService catalog,
    ILogger<StageBotNavigationProvider> logger)
    : IBotNavigationProvider, IBotMapNavigationDataProvider
{
    private readonly Dictionary<int, NavigationGrid> grids = [];
    private readonly Lock gate = new();

    /// <inheritdoc />
    public BotMapNavigationData Read(int map)
    {
        if (!catalog.TryRead(map, out var data, out var reason))
        {
            return new BotMapNavigationData(map, false, reason);
        }

        var grid = Grid(map, data);
        return grid.IsEmpty
            ? new BotMapNavigationData(
                map,
                false,
                $"The extracted data for stage {data.Stage} states no cell to route over.")
            : new BotMapNavigationData(
                map,
                true,
                $"Stage {data.Stage}: {grid.Count} cell(s) and {data.Spawns.Count} placement "
                + $"famil(ies), bounds {data.Bounds.MinX},{data.Bounds.MinZ} to "
                + $"{data.Bounds.MaxX},{data.Bounds.MaxZ}.");
    }

    /// <inheritdoc />
    public BotNavigationResult FindPath(BotNavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!catalog.TryRead(request.Map, out var data, out var reason))
        {
            return BotNavigationResult.Unavailable(reason);
        }

        var grid = Grid(request.Map, data);
        if (grid.IsEmpty)
        {
            return BotNavigationResult.Unavailable(
                $"The extracted data for stage {data.Stage} states no cell to route over.");
        }

        // A bot with no position starts from its own mode's placement, which is the
        // only point on the map this server can state for it.
        var from = request.From
            ?? StageSpawnRuleUtils.Select(data, request.Rule).FirstOrDefault()
            ?? data.Cells.FirstOrDefault();
        if (from is null)
        {
            return BotNavigationResult.Unavailable(
                $"Stage {data.Stage} states no placement and no cell, so a route has no origin.");
        }

        var path = grid.FindPath(from, request.To, out var refusal);
        if (path is null)
        {
            return new BotNavigationResult(BotNavigationStatus.Failed, null, refusal);
        }

        // Trace, not Debug: a route is planned again every 1.5 s per bot, so this
        // line is the hot path's own and not something a Debug run should hold.
        logger.LogTrace(
            "Routed map {Map} over {WaypointCount} point(s) from {FromX},{FromY},{FromZ} to {ToX},{ToY},{ToZ}",
            request.Map,
            path.Count,
            from.X,
            from.Y,
            from.Z,
            request.To.X,
            request.To.Y,
            request.To.Z);

        return new BotNavigationResult(
            BotNavigationStatus.Succeeded,
            new BotPath(path),
            $"A route over {path.Count} point(s) of the stage's own {grid.Count} cell(s).");
    }

    /// <summary>The grid of a map, built the first time it is asked for.</summary>
    /// <param name="map">Map the data is for.</param>
    /// <param name="data">The map's data.</param>
    /// <returns>The grid, which states its own emptiness.</returns>
    private NavigationGrid Grid(int map, StageNavigationMap data)
    {
        lock (gate)
        {
            if (!grids.TryGetValue(map, out var grid))
            {
                grid = NavigationGrid.Build(data);
                grids[map] = grid;
            }

            return grid;
        }
    }
}
