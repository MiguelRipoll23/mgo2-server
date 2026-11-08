namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// A stage's extracted cells, as a graph a route can be found over.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its nodes are the stage's own cells, extracted once and shipped.</b> The
/// generator reads a stage's spatial groups and writes the centre of each cell; the
/// grid takes those centres as nodes and joins a node to the nodes in the cells
/// around it, so a route runs over the stage's own division of its volume and the
/// server never opens a stage file.
/// </para>
/// <para>
/// <b>Height is coarse and that is stated rather than hidden.</b> A cell is a box,
/// and a route over cell centres does not know the floor inside it, so a bot's
/// route is over space the stage occupies rather than a walk along ground nothing
/// sampled. A finer extraction — collision primitives, or a replay's own tracks —
/// refines the data without the bot system changing.
/// </para>
/// </remarks>
public sealed class NavigationGrid
{
    /// <summary>Nodes a single route search may expand.</summary>
    private const int MaximumExpansions = 65536;

    private static readonly (int X, int Y, int Z)[] Offsets = BuildOffsets();

    private readonly Dictionary<(int X, int Y, int Z), BotPosition> cells;
    private readonly float step;
    private readonly StageBounds bounds;

    private NavigationGrid(
        Dictionary<(int X, int Y, int Z), BotPosition> cells,
        StageBounds bounds,
        float step)
    {
        this.cells = cells;
        this.bounds = bounds;
        this.step = step;
    }

    /// <summary>How many cells the grid holds.</summary>
    public int Count => cells.Count;

    /// <summary>Whether the grid holds no cell at all.</summary>
    public bool IsEmpty => cells.Count == 0;

    /// <summary>
    /// Builds a grid from a map's extracted cells.
    /// </summary>
    /// <param name="map">The map's navigation data.</param>
    /// <returns>The grid, empty when the data states no cell.</returns>
    public static NavigationGrid Build(StageNavigationMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var step = Math.Clamp(map.CellSize > 0f ? map.CellSize : 1000f, 250f, 12000f);
        var cells = new Dictionary<(int X, int Y, int Z), BotPosition>(map.Cells.Count);

        foreach (var cell in map.Cells)
        {
            cells.TryAdd(Index(cell, map.Bounds, step), cell);
        }

        return new NavigationGrid(cells, map.Bounds, step);
    }

    /// <summary>
    /// The cell nearest a point, or <c>null</c> when the grid is empty.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The nearest cell's centre.</returns>
    public BotPosition? Nearest(BotPosition point)
    {
        var nearest = NearestKey(point);
        return nearest is null ? null : cells[nearest.Value];
    }

    /// <summary>
    /// Finds a route between two points over the grid.
    /// </summary>
    /// <param name="from">Where the route starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <param name="reason">Why there is no route, when this returns <c>null</c>.</param>
    /// <returns>The points to walk through, or <c>null</c> when the cells cannot join them.</returns>
    public IReadOnlyList<BotPosition>? FindPath(
        BotPosition from,
        BotPosition to,
        out string reason)
    {
        if (IsEmpty)
        {
            reason = "The stage's extracted data states no cell, so there is no graph to route over.";
            return null;
        }

        var start = NearestKey(from);
        var goal = NearestKey(to);
        if (start is null || goal is null)
        {
            reason = "The grid holds no cell near the route's ends.";
            return null;
        }

        var open = new PriorityQueue<(int X, int Y, int Z), float>();
        var cost = new Dictionary<(int X, int Y, int Z), float> { [start.Value] = 0f };
        var previous = new Dictionary<(int X, int Y, int Z), (int X, int Y, int Z)>();
        var closed = new HashSet<(int X, int Y, int Z)>();
        open.Enqueue(start.Value, Distance(cells[start.Value], cells[goal.Value]));

        while (open.TryDequeue(out var current, out _))
        {
            if (current == goal.Value)
            {
                reason = string.Empty;
                return Build(cells, previous, start.Value, goal.Value, to);
            }

            if (!closed.Add(current))
            {
                continue;
            }

            if (closed.Count > MaximumExpansions)
            {
                break;
            }

            foreach (var offset in Offsets)
            {
                var neighbour = (current.X + offset.X, current.Y + offset.Y, current.Z + offset.Z);
                if (!cells.TryGetValue(neighbour, out var centre) || closed.Contains(neighbour))
                {
                    continue;
                }

                var candidate = cost[current] + Distance(cells[current], centre);
                if (cost.TryGetValue(neighbour, out var known) && candidate >= known)
                {
                    continue;
                }

                cost[neighbour] = candidate;
                previous[neighbour] = current;
                open.Enqueue(neighbour, candidate + Distance(centre, cells[goal.Value]));
            }
        }

        reason = "No route joins the two cells the request names over the stage's extracted cells.";
        return null;
    }

    /// <summary>The key of the cell nearest a point.</summary>
    /// <param name="point">The point.</param>
    /// <returns>The key, or <c>null</c> when the grid is empty.</returns>
    private (int X, int Y, int Z)? NearestKey(BotPosition point)
    {
        (int X, int Y, int Z)? nearest = null;
        var best = float.MaxValue;

        foreach (var (key, centre) in cells)
        {
            var distance = Distance(centre, point);
            if (distance < best)
            {
                best = distance;
                nearest = key;
            }
        }

        return nearest;
    }

    /// <summary>Turns a search's chain of cells into the points to walk through.</summary>
    private static List<BotPosition> Build(
        Dictionary<(int X, int Y, int Z), BotPosition> cells,
        Dictionary<(int X, int Y, int Z), (int X, int Y, int Z)> previous,
        (int X, int Y, int Z) start,
        (int X, int Y, int Z) goal,
        BotPosition destination)
    {
        var path = new List<BotPosition>();
        var current = goal;
        while (current != start)
        {
            path.Add(cells[current]);
            if (!previous.TryGetValue(current, out var predecessor))
            {
                break;
            }

            current = predecessor;
        }

        path.Add(cells[start]);
        path.Reverse();

        if (path.Count == 0 || path[^1] != destination)
        {
            path.Add(destination);
        }

        return path;
    }

    /// <summary>The cell a point falls in.</summary>
    private static (int X, int Y, int Z) Index(BotPosition point, StageBounds bounds, float step) => (
        (int)MathF.Floor((point.X - bounds.MinX) / step),
        (int)MathF.Floor((point.Y - bounds.MinY) / step),
        (int)MathF.Floor((point.Z - bounds.MinZ) / step));

    /// <summary>The distance between two points.</summary>
    private static float Distance(BotPosition left, BotPosition right)
    {
        var x = left.X - right.X;
        var y = left.Y - right.Y;
        var z = left.Z - right.Z;
        return MathF.Sqrt((x * x) + (y * y) + (z * z));
    }

    /// <summary>The twenty-six cell offsets a node is joined to.</summary>
    private static (int X, int Y, int Z)[] BuildOffsets()
    {
        var offsets = new List<(int X, int Y, int Z)>(26);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                for (var z = -1; z <= 1; z++)
                {
                    if (x != 0 || y != 0 || z != 0)
                    {
                        offsets.Add((x, y, z));
                    }
                }
            }
        }

        return [.. offsets];
    }
}
