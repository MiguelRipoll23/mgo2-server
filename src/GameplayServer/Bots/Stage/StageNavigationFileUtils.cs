using System.Buffers.Binary;
using System.Text;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The file the extracted stage navigation data is read from and written to.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is written by the generator and shipped as a resource.</b>
/// <c>tools/stage-navigation</c> reads the client's stages once, extracts the box,
/// the placements and the cells of each, and writes this file; the gameplay server
/// embeds it, so a run needs no client install and no directory. The format is
/// little-endian throughout: a magic, a count, and per stage its name, its box, its
/// cell size, its placements by family and its cells.
/// </para>
/// <para>
/// <b>A file that does not start with the magic is refused</b>, and so is one that
/// ends inside a record: a truncated resource is a build fault, not a map with
/// fewer cells.
/// </para>
/// </remarks>
public static class StageNavigationFileUtils
{
    /// <summary>The embedded resource's name.</summary>
    public const string ResourceName = "stage-navigation.bin";

    /// <summary>Bytes that open the file.</summary>
    private static readonly byte[] Magic = "MGO2NAV1"u8.ToArray();

    /// <summary>Opens the data this assembly ships, or <c>null</c> when it ships none.</summary>
    /// <returns>The stream, or <c>null</c>.</returns>
    public static Stream? OpenEmbedded() =>
        typeof(StageNavigationFileUtils).Assembly.GetManifestResourceStream(ResourceName);

    /// <summary>Reads a stage navigation file.</summary>
    /// <param name="stream">The file's contents.</param>
    /// <returns>The stages, by stage name.</returns>
    /// <exception cref="InvalidDataException">The file is not one this server writes.</exception>
    public static IReadOnlyDictionary<string, StageNavigationMap> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var magic = reader.ReadBytes(Magic.Length);
        if (magic.Length != Magic.Length || !magic.AsSpan().SequenceEqual(Magic))
        {
            throw new InvalidDataException("The stage navigation data does not open with its magic.");
        }

        var stageCount = ReadCount(reader, "stage");
        var stages = new Dictionary<string, StageNavigationMap>(stageCount, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < stageCount; index++)
        {
            var stage = ReadString(reader);
            var bounds = new StageBounds(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
            var cellSize = reader.ReadSingle();

            var spawnCount = ReadCount(reader, "placement set");
            var spawns = new List<StageSpawnSet>(spawnCount);
            for (var set = 0; set < spawnCount; set++)
            {
                var family = (StageSpawnFamily)reader.ReadInt32();
                spawns.Add(new StageSpawnSet(family, ReadPoints(reader)));
            }

            stages[stage] = new StageNavigationMap(
                stage,
                bounds,
                cellSize,
                spawns,
                ReadPoints(reader));
        }

        return stages;
    }

    /// <summary>Writes a stage navigation file.</summary>
    /// <param name="stream">Where to write it.</param>
    /// <param name="stages">The stages to write.</param>
    public static void Write(Stream stream, IReadOnlyCollection<StageNavigationMap> stages)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(stages);

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(stages.Count);

        foreach (var stage in stages.OrderBy(stage => stage.Stage, StringComparer.Ordinal))
        {
            // An int32 length and then the bytes, which is what ReadString reads. A
            // BinaryWriter's own string overload writes a 7-bit length instead, and
            // mixing the two is a file that reads as noise.
            var nameBytes = Encoding.UTF8.GetBytes(stage.Stage);
            writer.Write(nameBytes.Length);
            writer.Write(nameBytes);
            writer.Write(stage.Bounds.MinX);
            writer.Write(stage.Bounds.MinY);
            writer.Write(stage.Bounds.MinZ);
            writer.Write(stage.Bounds.MaxX);
            writer.Write(stage.Bounds.MaxY);
            writer.Write(stage.Bounds.MaxZ);
            writer.Write(stage.CellSize);

            writer.Write(stage.Spawns.Count);
            foreach (var set in stage.Spawns)
            {
                writer.Write((int)set.Family);
                WritePoints(writer, set.Placements);
            }

            WritePoints(writer, stage.Cells);
        }
    }

    /// <summary>Reads a run of points.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The points.</returns>
    private static List<BotPosition> ReadPoints(BinaryReader reader)
    {
        var count = ReadCount(reader, "point");
        var points = new List<BotPosition>(count);
        for (var index = 0; index < count; index++)
        {
            points.Add(new BotPosition(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        }

        return points;
    }

    /// <summary>Writes a run of points.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="points">The points.</param>
    private static void WritePoints(BinaryWriter writer, IReadOnlyList<BotPosition> points)
    {
        writer.Write(points.Count);
        foreach (var point in points)
        {
            writer.Write(point.X);
            writer.Write(point.Y);
            writer.Write(point.Z);
        }
    }

    /// <summary>Reads a count the file states.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="what">What the count counts, for the refusal.</param>
    /// <returns>The count.</returns>
    private static int ReadCount(BinaryReader reader, string what)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > 1_000_000)
        {
            throw new InvalidDataException($"The stage navigation data states {count} {what}(s).");
        }

        return count;
    }

    /// <summary>Reads a length-prefixed string.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The string.</returns>
    private static string ReadString(BinaryReader reader)
    {
        var length = ReadCount(reader, "name byte");
        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new InvalidDataException("The stage navigation data ends inside a stage name.");
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
