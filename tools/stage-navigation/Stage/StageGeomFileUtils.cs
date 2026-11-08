using System.Buffers.Binary;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Reads the two parts of a stage's GEOM that navigation uses: its spatial groups
/// and its effect tree.
/// </summary>
/// <remarks>
/// <para>
/// <b>The layout is Haven's, and the checks are this reader's.</b> The header is a
/// version, the file's own size, the chunk count and the four base floats; each
/// chunk entry is a type, a size and an offset into the file. Haven reads the same
/// twenty-four-bit name hash and the same eight-byte payload slots
/// (<c>Formats/Geo/GeomFile.cs</c>, <c>GeoEffectLayout.cs</c>), so the two readers
/// see one file.
/// </para>
/// <para>
/// <b>A file that does not hold together is refused rather than half-read.</b> The
/// size the header states is checked against the bytes present, an offset outside
/// its chunk is an error, and the effect walk carries both a visited set and a
/// depth limit, because a cyclic tree must not become a hang. A stage this server
/// cannot read is a map navigation says it has no data for, which is an answer.
/// </para>
/// </remarks>
public static class StageGeomFileUtils
{
    /// <summary>Bytes before the chunk table.</summary>
    public const int HeaderSize = 32;

    /// <summary>Bytes of one chunk-table entry.</summary>
    public const int ChunkEntrySize = 12;

    /// <summary>Bytes of one group header.</summary>
    public const int GroupSize = 64;

    /// <summary>Chunk type of the stage's spatial groups.</summary>
    public const ushort GroupChunkType = 0;

    /// <summary>Chunk type of the stage's effects, its props tree.</summary>
    public const ushort PropsChunkType = 6;

    /// <summary>Bits of an effect index that hold the position slot.</summary>
    private const int PositionSlotMask = 0x3FF;

    /// <summary>Bytes of one effect payload slot.</summary>
    private const int PayloadSlotSize = 8;

    private const int MaximumChunks = 32;
    private const int MaximumGroups = 4096;
    private const int MaximumEffects = 65536;
    private const int MaximumDepth = 64;

    /// <summary>Reads a stage's GEOM from a file.</summary>
    /// <param name="path">Path of the <c>.geom</c> file.</param>
    /// <returns>The stage's groups and effects.</returns>
    /// <exception cref="InvalidDataException">The file does not hold together.</exception>
    public static StageGeomFile Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Read(File.ReadAllBytes(path), path);
    }

    /// <summary>Reads a stage's GEOM from bytes.</summary>
    /// <param name="bytes">The file's contents.</param>
    /// <param name="source">Where the bytes came from, for the refusal to name.</param>
    /// <returns>The stage's groups and effects.</returns>
    /// <exception cref="InvalidDataException">The file does not hold together.</exception>
    public static StageGeomFile Read(ReadOnlySpan<byte> bytes, string source = "")
    {
        if (bytes.Length < HeaderSize)
        {
            throw new InvalidDataException(
                $"GEOM '{source}' is {bytes.Length} bytes, shorter than its own {HeaderSize}-byte header.");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        var declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        var chunkCount = BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]);

        if (declaredSize != bytes.Length)
        {
            throw new InvalidDataException(
                $"GEOM '{source}' states {declaredSize} bytes and carries {bytes.Length}.");
        }

        if (chunkCount <= 0 || chunkCount > MaximumChunks)
        {
            throw new InvalidDataException(
                $"GEOM '{source}' states {chunkCount} chunk(s), which is no chunk table.");
        }

        var chunks = new Dictionary<ushort, (int Size, int Offset)>();
        var cursor = HeaderSize;
        for (var index = 0; index < chunkCount; index++)
        {
            if (cursor + ChunkEntrySize > bytes.Length)
            {
                throw new InvalidDataException(
                    $"GEOM '{source}' ends inside its chunk table at 0x{cursor:X}.");
            }

            var type = BinaryPrimitives.ReadUInt16LittleEndian(bytes[cursor..]);
            var size = BinaryPrimitives.ReadInt32LittleEndian(bytes[(cursor + 4)..]);
            var offset = BinaryPrimitives.ReadInt32LittleEndian(bytes[(cursor + 8)..]);
            chunks[type] = (size, offset);
            cursor += ChunkEntrySize;
        }

        var groups = chunks.TryGetValue(GroupChunkType, out var groupChunk)
            ? ReadGroups(bytes, groupChunk.Offset, source)
            : [];
        var effects = chunks.TryGetValue(PropsChunkType, out var propsChunk)
            ? ReadEffects(bytes, propsChunk.Offset, propsChunk.Size, source)
            : [];

        return new StageGeomFile(source, version, groups, effects);
    }

    /// <summary>Reads the group run that starts at a chunk's data.</summary>
    /// <param name="bytes">The file's contents.</param>
    /// <param name="offset">Where the run starts.</param>
    /// <param name="source">Where the bytes came from.</param>
    /// <returns>The groups, in the file's order.</returns>
    private static List<StageGeomGroup> ReadGroups(
        ReadOnlySpan<byte> bytes,
        int offset,
        string source)
    {
        var groups = new List<StageGeomGroup>();

        for (var index = 0; index < MaximumGroups; index++)
        {
            var start = offset + (index * GroupSize);
            if (start < 0 || start + GroupSize > bytes.Length)
            {
                throw new InvalidDataException(
                    $"GEOM '{source}' ends inside its group run at 0x{start:X}.");
            }

            var group = new StageGeomGroup(
                BinaryPrimitives.ReadSingleLittleEndian(bytes[start..]),
                BinaryPrimitives.ReadSingleLittleEndian(bytes[(start + 4)..]),
                BinaryPrimitives.ReadSingleLittleEndian(bytes[(start + 8)..]),
                BinaryPrimitives.ReadSingleLittleEndian(bytes[(start + 16)..]),
                BinaryPrimitives.ReadSingleLittleEndian(bytes[(start + 20)..]),
                BinaryPrimitives.ReadSingleLittleEndian(bytes[(start + 24)..]),
                BinaryPrimitives.ReadInt32LittleEndian(bytes[(start + 32)..]),
                BinaryPrimitives.ReadInt32LittleEndian(bytes[(start + 36)..]),
                BinaryPrimitives.ReadInt32LittleEndian(bytes[(start + 40)..]));
            groups.Add(group);

            // The last group of the run states itself as the last one.
            if (BinaryPrimitives.ReadInt32LittleEndian(bytes[(start + 48)..]) == 1)
            {
                break;
            }
        }

        return groups;
    }

    /// <summary>Walks the props tree of a stage.</summary>
    /// <param name="bytes">The file's contents.</param>
    /// <param name="offset">Where the props chunk's records start.</param>
    /// <param name="size">How many bytes the chunk states.</param>
    /// <param name="source">Where the bytes came from.</param>
    /// <returns>Every effect read, categories included.</returns>
    private static List<StageEffect> ReadEffects(
        ReadOnlySpan<byte> bytes,
        int offset,
        int size,
        string source)
    {
        var effects = new List<StageEffect>();
        if (size < 16)
        {
            return effects;
        }

        var visited = new HashSet<int>();
        var pending = new Stack<(int Offset, int Depth)>();
        pending.Push((0, 0));

        while (pending.Count > 0 && effects.Count < MaximumEffects)
        {
            var (record, depth) = pending.Pop();
            if (!visited.Add(record))
            {
                continue;
            }

            if (record < 0 || record + 16 > size)
            {
                throw new InvalidDataException(
                    $"GEOM '{source}' names an effect at 0x{record:X}, outside its {size}-byte props tree.");
            }

            var start = offset + record;
            var next = BinaryPrimitives.ReadInt32LittleEndian(bytes[start..]);
            var child = BinaryPrimitives.ReadInt32LittleEndian(bytes[(start + 4)..]);
            var name = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(start + 8)..]);
            var effectIndex = BinaryPrimitives.ReadInt32LittleEndian(bytes[(start + 12)..]);

            var positionSlot = effectIndex & PositionSlotMask;
            var x = 0f;
            var y = 0f;
            var z = 0f;
            if (positionSlot != 0)
            {
                var payload = record + (positionSlot * PayloadSlotSize);
                if (payload < 0 || payload + 16 > size)
                {
                    throw new InvalidDataException(
                        $"GEOM '{source}' puts an effect position at 0x{payload:X}, outside its props tree.");
                }

                var placement = offset + payload;
                x = BinaryPrimitives.ReadSingleLittleEndian(bytes[placement..]);
                y = BinaryPrimitives.ReadSingleLittleEndian(bytes[(placement + 4)..]);
                z = BinaryPrimitives.ReadSingleLittleEndian(bytes[(placement + 8)..]);
            }

            effects.Add(new StageEffect(name, x, y, z, depth));

            if (child > 0 && depth < MaximumDepth)
            {
                var sibling = record + child;
                for (var guard = 0; guard < MaximumEffects && sibling > 0 && sibling + 16 <= size; guard++)
                {
                    pending.Push((sibling, depth + 1));
                    var siblingNext = BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + sibling)..]);
                    if (siblingNext == 0)
                    {
                        break;
                    }

                    sibling += siblingNext;
                }
            }

            if (next != 0 && record + next < size)
            {
                pending.Push((record + next, depth));
            }
        }

        return effects;
    }
}
