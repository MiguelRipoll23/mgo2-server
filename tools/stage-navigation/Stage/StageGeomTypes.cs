namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// One of a stage's spatial groups: the box it covers and the cell size it is
/// divided into.
/// </summary>
/// <remarks>
/// <b>A group is the stage's own division of its volume.</b> <c>GeoGroup</c> in
/// Haven (<c>Formats/Geo/GeoGroup.cs</c>) carries a base point, a division per axis
/// and a count per axis; the product of the three is the number of cells the group
/// holds, which is what its collision radix is indexed by. Reading the groups
/// therefore gives the stage's own extent and the resolution it uses without a
/// mesh.
/// </remarks>
/// <param name="BaseX">First coordinate of the group's corner.</param>
/// <param name="BaseY">Second coordinate of the group's corner.</param>
/// <param name="BaseZ">Third coordinate of the group's corner.</param>
/// <param name="CellX">Width of one cell along the first axis.</param>
/// <param name="CellY">Width of one cell along the second axis.</param>
/// <param name="CellZ">Width of one cell along the third axis.</param>
/// <param name="MaxX">Cells along the first axis.</param>
/// <param name="MaxY">Cells along the second axis.</param>
/// <param name="MaxZ">Cells along the third axis.</param>
public sealed record StageGeomGroup(
    float BaseX,
    float BaseY,
    float BaseZ,
    float CellX,
    float CellY,
    float CellZ,
    int MaxX,
    int MaxY,
    int MaxZ)
{
    /// <summary>The box the group's cells fill.</summary>
    /// <returns>The group's bounds.</returns>
    public StageBounds Bounds => new(
        BaseX,
        BaseY,
        BaseZ,
        BaseX + (CellX * MaxX),
        BaseY + (CellY * MaxY),
        BaseZ + (CellZ * MaxZ));
}

/// <summary>
/// One effect in a stage's props tree: a name, a position where the record carries
/// one, and how deep in the category tree it sits.
/// </summary>
/// <remarks>
/// <b>The position is the stage's own.</b> A GEOM effect packs three payload slots
/// into its index, and the position slot is measured in eight-byte units from the
/// record's start (<c>GeoEffectLayout.cs</c>); a record whose position slot is zero
/// carries no point, which is what the category nodes — <c>SYSTEM</c>,
/// <c>RULE</c>, <c>ITEM</c> — are.
/// </remarks>
/// <param name="NameHash">Hash of the effect's <c>PRP_*</c> name.</param>
/// <param name="X">First coordinate the record states.</param>
/// <param name="Y">Second coordinate the record states.</param>
/// <param name="Z">Third coordinate the record states.</param>
/// <param name="Depth">Depth in the effect tree, zero for a category.</param>
public sealed record StageEffect(uint NameHash, float X, float Y, float Z, int Depth);

/// <summary>
/// A stage's GEOM, as far as extraction reads it: its groups and its effects.
/// </summary>
/// <remarks>
/// <b>Two things are read and the rest is left alone.</b> The groups say what space
/// the stage occupies and at what resolution; the effects say where its own markers
/// are. The mesh, the materials, the collision primitives and the routes chunk are
/// not decoded, because a route can be built over the cells without them and a
/// structure nothing checked would be a guess dressed as data.
/// </remarks>
/// <param name="Source">Path the stage was read from.</param>
/// <param name="Version">The file's version word.</param>
/// <param name="Groups">The stage's groups.</param>
/// <param name="Effects">The effects read from the props tree, categories included.</param>
public sealed record StageGeomFile(
    string Source,
    uint Version,
    IReadOnlyList<StageGeomGroup> Groups,
    IReadOnlyList<StageEffect> Effects)
{
    /// <summary>The box every group lies in, or <c>null</c> when there are none.</summary>
    public StageBounds? Bounds
    {
        get
        {
            if (Groups.Count == 0)
            {
                return null;
            }

            var boxes = Groups.Select(group => group.Bounds).ToList();
            return new StageBounds(
                boxes.Min(box => box.MinX),
                boxes.Min(box => box.MinY),
                boxes.Min(box => box.MinZ),
                boxes.Max(box => box.MaxX),
                boxes.Max(box => box.MaxY),
                boxes.Max(box => box.MaxZ));
        }
    }

    /// <summary>The stage's own bounds markers, when it states both corners.</summary>
    /// <returns>The box, or <c>null</c> when a corner is missing.</returns>
    public StageBounds? DeclaredBounds()
    {
        StageEffect? first = null;
        StageEffect? second = null;

        foreach (var effect in Effects)
        {
            if (effect.Depth == 0)
            {
                continue;
            }

            if (effect.NameHash == StageNameHashUtils.StageSizeOne)
            {
                first = effect;
            }
            else if (effect.NameHash == StageNameHashUtils.StageSizeTwo)
            {
                second = effect;
            }
        }

        if (first is null || second is null)
        {
            return null;
        }

        return new StageBounds(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Min(first.Z, second.Z),
            Math.Max(first.X, second.X),
            Math.Max(first.Y, second.Y),
            Math.Max(first.Z, second.Z));
    }
}
