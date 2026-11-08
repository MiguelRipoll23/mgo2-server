namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// An axis-aligned box on a stage, in the stage's own world units.
/// </summary>
/// <param name="MinX">Smallest first coordinate.</param>
/// <param name="MinY">Smallest second coordinate.</param>
/// <param name="MinZ">Smallest third coordinate.</param>
/// <param name="MaxX">Largest first coordinate.</param>
/// <param name="MaxY">Largest second coordinate.</param>
/// <param name="MaxZ">Largest third coordinate.</param>
public sealed record StageBounds(
    float MinX,
    float MinY,
    float MinZ,
    float MaxX,
    float MaxY,
    float MaxZ)
{
    /// <summary>Whether a point lies inside the box.</summary>
    /// <param name="point">The point.</param>
    /// <returns><c>true</c> when the point is inside.</returns>
    public bool Contains(BotPosition point) =>
        point.X >= MinX && point.X <= MaxX &&
        point.Y >= MinY && point.Y <= MaxY &&
        point.Z >= MinZ && point.Z <= MaxZ;
}

/// <summary>
/// The kinds of placement a stage states, one per mode's spawn family.
/// </summary>
/// <remarks>
/// The names behind these come from the stages themselves
/// (<c>PRP_RES_TEAM_DEATHMATCH_A01</c>, <c>PRP_TEAM_SNEAKING_TGT_01</c>,
/// <c>PRP_HOMEBASE_*</c>), read once by the generator and stored here as a family
/// so the server needs no name table.
/// </remarks>
public enum StageSpawnFamily
{
    /// <summary>Free-for-all deathmatch spawns.</summary>
    Deathmatch = 0,

    /// <summary>Team deathmatch and survival spawns.</summary>
    TeamDeathmatch = 1,

    /// <summary>Rescue spawns.</summary>
    Rescue = 2,

    /// <summary>Capture spawns.</summary>
    Capture = 3,

    /// <summary>Sneaking spawns and targets.</summary>
    Sneaking = 4,

    /// <summary>Base spawns and gates.</summary>
    Base = 5,

    /// <summary>Bomb spawns and sites.</summary>
    Bomb = 6,

    /// <summary>Placements a mode does not sort into the families above.</summary>
    Other = 7,

    /// <summary>
    /// The room's home gate on side A: <c>PRP_HOMEGATE_A01</c>…<c>A16</c>, the
    /// placements the room's player entry is written from.
    /// </summary>
    HomeGateA = 8,

    /// <summary>The room's home gate on side B: <c>PRP_HOMEGATE_B01</c>…<c>B16</c>.</summary>
    HomeGateB = 9,
}

/// <summary>
/// One family of placements a stage states.
/// </summary>
/// <param name="Family">Which family.</param>
/// <param name="Placements">The points, in the stage's order.</param>
public sealed record StageSpawnSet(StageSpawnFamily Family, IReadOnlyList<BotPosition> Placements);

/// <summary>
/// One stage's navigation data: where its bounds are, what it offers each mode, and
/// the cells a route runs over.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the extracted data and not the stage.</b> The generator reads a stage's
/// GEOM once, keeps the three things navigation needs — the box the stage states,
/// the placements its modes name, and the cells its own spatial division makes —
/// and writes them to one file the server ships. A run therefore needs no client
/// install, and a map whose data is absent is refused rather than guessed at.
/// </para>
/// </remarks>
/// <param name="Stage">The stage's name, such as <c>n001a</c>.</param>
/// <param name="Bounds">The box the stage states.</param>
/// <param name="CellSize">The size of one grid cell, in stage units.</param>
/// <param name="Spawns">The placements, by family.</param>
/// <param name="Cells">The cell centres a route may use.</param>
public sealed record StageNavigationMap(
    string Stage,
    StageBounds Bounds,
    float CellSize,
    IReadOnlyList<StageSpawnSet> Spawns,
    IReadOnlyList<BotPosition> Cells)
{
    /// <summary>The placements of one family, or an empty list.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The points, empty when the stage states none.</returns>
    public IReadOnlyList<BotPosition> Placements(StageSpawnFamily family) =>
        Spawns.FirstOrDefault(set => set.Family == family)?.Placements ?? [];
}
