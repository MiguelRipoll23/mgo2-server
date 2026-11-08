using Mgo2Server.GameplayServer.Bots;

// Extracts what navigation needs from the client's stages: the box each stage
// states, the placements its modes name, and the centres of its own spatial cells.
// The output is one file the gameplay server embeds, so a run needs no stage files.

var stageDirectory = args.ElementAtOrDefault(0)
    ?? "/Users/miguel/Documents/Metal Gear Online 2/game/stage";
var dictionaryPath = args.ElementAtOrDefault(1) ?? ".gametools/Haven/dictionary.txt";
var outputPath = args.ElementAtOrDefault(2)
    ?? "src/GameplayServer/Bots/Stage/Data/stage-navigation.bin";

if (!Directory.Exists(stageDirectory))
{
    Console.Error.WriteLine($"The stage directory '{stageDirectory}' does not exist.");
    return 1;
}

var names = LoadNames(dictionaryPath);
Console.WriteLine($"Read {names.Count} name(s) from '{dictionaryPath}'.");

var stages = new List<StageNavigationMap>();
foreach (var directory in Directory.EnumerateDirectories(stageDirectory).Order(StringComparer.Ordinal))
{
    var stage = Path.GetFileName(directory);
    var path = Path.Combine(directory, stage + ".geom");
    if (!File.Exists(path))
    {
        continue;
    }

    try
    {
        var geometry = StageGeomFileUtils.Read(path);
        var map = Extract(stage, geometry, names);
        if (map.Cells.Count == 0)
        {
            Console.WriteLine($"{stage}: skipped, the stage states no spatial cell.");
            continue;
        }

        stages.Add(map);
        Console.WriteLine(
            $"{stage}: {map.Cells.Count} cell(s), cell size {map.CellSize}, "
            + $"placements {string.Join(", ", map.Spawns.Select(set => $"{set.Family}={set.Placements.Count}"))}");
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException)
    {
        Console.WriteLine($"{stage}: skipped ({exception.GetType().Name}: {exception.Message})");
    }
}

var fullPath = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
using (var stream = File.Create(fullPath))
{
    StageNavigationFileUtils.Write(stream, stages);
}

Console.WriteLine(
    $"Wrote {stages.Count} stage(s) to '{fullPath}' ({new FileInfo(fullPath).Length} bytes).");
return 0;

static Dictionary<uint, string> LoadNames(string path)
{
    var names = new Dictionary<uint, string>();
    if (!File.Exists(path))
    {
        Console.Error.WriteLine(
            $"The dictionary '{path}' does not exist; placements are written by family from the "
            + "names the stages hash, and nothing else changes.");
        return names;
    }

    foreach (var line in File.ReadLines(path))
    {
        var name = line.Trim();
        if (name.Length > 0)
        {
            names[StageNameHashUtils.Hash(name)] = name;
        }
    }

    return names;
}

static StageNavigationMap Extract(
    string stage,
    StageGeomFile geometry,
    IReadOnlyDictionary<uint, string> names)
{
    var bounds = Bounds(geometry);
    var (cells, cellSize) = Cells(geometry, bounds);
    var spawns = Families(geometry, names);
    return new StageNavigationMap(stage, bounds, cellSize, spawns, cells);
}

// The stage's own bounds markers state x and z, and its groups state every axis:
// the union is the box a bot can be placed in, which is what the lattice is cut
// from.
static StageBounds Bounds(StageGeomFile geometry)
{
    var declared = geometry.DeclaredBounds();
    var groups = geometry.Bounds;

    if (declared is null && groups is null)
    {
        return new StageBounds(0f, 0f, 0f, 0f, 0f, 0f);
    }

    if (declared is null)
    {
        return groups!;
    }

    if (groups is null)
    {
        return declared;
    }

    return new StageBounds(
        Math.Min(declared.MinX, groups.MinX),
        Math.Min(declared.MinY, groups.MinY),
        Math.Min(declared.MinZ, groups.MinZ),
        Math.Max(declared.MaxX, groups.MaxX),
        Math.Max(declared.MaxY, groups.MaxY),
        Math.Max(declared.MaxZ, groups.MaxZ));
}

// A lattice over the stage's box, keeping the points that fall inside one of its
// spatial groups: the boxes are the space the stage states it occupies, so a point
// inside one is ground the stage itself describes. The step is the coarsest that
// still fits the data, which keeps every stage's graph one piece.
static (List<BotPosition> Cells, float Step) Cells(StageGeomFile geometry, StageBounds bounds)
{
    // The cap keeps the shipped file small: a stage's lattice is coarsened until it
    // fits, and a route over a coarser lattice is still a route over the stage.
    const int maximumCells = 8000;
    var boxes = geometry.Groups.Select(group => group.Bounds).ToList();
    if (boxes.Count == 0)
    {
        return ([], 1000f);
    }

    foreach (var step in new[] { 500f, 750f, 1000f, 1500f, 2000f, 3000f, 4000f, 6000f, 12000f })
    {
        var cells = new List<BotPosition>();
        var countX = (int)MathF.Ceiling((bounds.MaxX - bounds.MinX) / step);
        var countY = (int)MathF.Ceiling((bounds.MaxY - bounds.MinY) / step);
        var countZ = (int)MathF.Ceiling((bounds.MaxZ - bounds.MinZ) / step);

        for (var x = 0; x < countX && cells.Count <= maximumCells; x++)
        {
            for (var y = 0; y < countY && cells.Count <= maximumCells; y++)
            {
                for (var z = 0; z < countZ && cells.Count <= maximumCells; z++)
                {
                    var point = new BotPosition(
                        bounds.MinX + ((x + 0.5f) * step),
                        bounds.MinY + ((y + 0.5f) * step),
                        bounds.MinZ + ((z + 0.5f) * step));
                    if (boxes.Any(box => box.Contains(point)))
                    {
                        cells.Add(point);
                    }
                }
            }
        }

        if (cells.Count <= maximumCells)
        {
            return (cells, step);
        }
    }

    return ([], 12000f);
}

static List<StageSpawnSet> Families(
    StageGeomFile geometry,
    IReadOnlyDictionary<uint, string> names)
{
    var placements = new Dictionary<StageSpawnFamily, List<BotPosition>>();
    var seen = new HashSet<(float X, float Y, float Z)>();

    foreach (var effect in geometry.Effects)
    {
        if (effect.Depth == 0 || !names.TryGetValue(effect.NameHash, out var name))
        {
            continue;
        }

        var family = FamilyOf(name);
        if (family is null || !seen.Add((effect.X, effect.Y, effect.Z)))
        {
            continue;
        }

        if (!placements.TryGetValue(family.Value, out var points))
        {
            points = [];
            placements[family.Value] = points;
        }

        points.Add(new BotPosition(effect.X, effect.Y, effect.Z));
    }

    return
    [
        .. placements
            .OrderBy(entry => entry.Key)
            .Select(entry => new StageSpawnSet(entry.Key, entry.Value)),
    ];
}

// The families are the stages' own names, read through Haven's dictionary: a
// placement is a point of a mode's spawn set, and the minimap twin is not one.
static StageSpawnFamily? FamilyOf(string name)
{
    if (name.StartsWith("PRP_MINI_", StringComparison.Ordinal))
    {
        return null;
    }

    if (name.Contains("RESCUE", StringComparison.Ordinal))
    {
        return StageSpawnFamily.Rescue;
    }

    if (name.Contains("SNEAKING", StringComparison.Ordinal))
    {
        return StageSpawnFamily.Sneaking;
    }

    // The home gate is the family the room's own player entry is written from: one
    // side is A01..A16 and the other B01..B16, and a map's measured spawn lands on
    // one of each (Blood Bath's room entry is exactly A01 and B01).
    if (name.StartsWith("PRP_HOMEGATE_A", StringComparison.Ordinal))
    {
        return StageSpawnFamily.HomeGateA;
    }

    if (name.StartsWith("PRP_HOMEGATE_B", StringComparison.Ordinal))
    {
        return StageSpawnFamily.HomeGateB;
    }

    if (name.Contains("HOMEBASE", StringComparison.Ordinal))
    {
        return StageSpawnFamily.Base;
    }

    if (name.Contains("BOMB", StringComparison.Ordinal))
    {
        return StageSpawnFamily.Bomb;
    }

    if (name.Contains("TEAM_DEATHMATCH", StringComparison.Ordinal))
    {
        return StageSpawnFamily.TeamDeathmatch;
    }

    if (name.Contains("DEATHMATCH", StringComparison.Ordinal))
    {
        return StageSpawnFamily.Deathmatch;
    }

    if (name.Contains("CAP", StringComparison.Ordinal))
    {
        return StageSpawnFamily.Capture;
    }

    return null;
}
