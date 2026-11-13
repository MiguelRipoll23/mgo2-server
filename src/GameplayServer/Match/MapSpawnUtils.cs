using Mgo2Server.Shared.Domain.Characters;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// One point on a stage: the heading and the coordinate the room's 25-byte player
/// entry carries for it.
/// </summary>
/// <remarks>
/// A spawn states its heading (<c>+10</c>) and its <c>x</c>, and a look-at point
/// the <c>y</c> and <c>z</c> of the point it names
/// (<see cref="RoomPlayerEntryBodiesUtils"/>). The coordinate is a stage unit and
/// is written as a signed thirty-two, so it is carried here as one; only the
/// heading is sixteen bit.
/// </remarks>
/// <param name="First">
/// The heading of a spawn, or the <c>y</c> of a look-at point. Blood Bath's solo
/// room writes <c>0x5FFF</c> on the side it spawns on and the full room
/// <c>0x0000</c> on the other; every look-at point writes <c>800</c>, the ground
/// plus the lift <c>SV_GetInitialPosDir</c> adds.
/// </param>
/// <param name="Along">
/// The <c>x</c> of a spawn, or the <c>z</c> of a look-at point, in the stage's own
/// units — Blood Bath's two sides are <c>x = -23750</c> and <c>x = 20750</c>.
/// </param>
public readonly record struct MapSpawnPoint(short First, int Along);

/// <summary>
/// Where each side starts on one stage, and the point the entry records for that
/// side.
/// </summary>
/// <remarks>
/// The two spawns are the two sides of a stage. Each side's look-at point is that
/// same side's own <c>z</c> with the constant <c>y = 800</c>: Blood Bath records
/// <c>face 20 03 00 00 0e 51</c> against <c>pos ff 5f 3a a3 ff ff</c>, which is the
/// side at <c>(-23750, 20750)</c> facing its own <c>z</c>.
/// </remarks>
/// <param name="Red">The side the raw team byte <c>0</c> puts a player on.</param>
/// <param name="Blue">The side the raw team byte <c>1</c> puts a player on.</param>
/// <param name="RedFacing">The look-at point recorded for raw team 0.</param>
/// <param name="BlueFacing">The look-at point recorded for raw team 1.</param>
public readonly record struct MapSpawns(
    MapSpawnPoint Red,
    MapSpawnPoint Blue,
    MapSpawnPoint? RedFacing = null,
    MapSpawnPoint? BlueFacing = null)
{
    /// <summary>
    /// The spawn of the side a raw team byte states.
    /// </summary>
    /// <param name="team">Raw team byte, where <c>1</c> is blue and anything else is red.</param>
    /// <returns>The side's spawn.</returns>
    public MapSpawnPoint SpawnOf(byte team) => team == 1 ? Blue : Red;

    /// <summary>
    /// The point the side a raw team byte states records as its look-at point.
    /// </summary>
    /// <param name="team">Raw team byte, where <c>1</c> is blue and anything else is red.</param>
    /// <returns>The side's look-at point, or the other side's spawn when none is stated.</returns>
    public MapSpawnPoint FacingOf(byte team) =>
        team == 1 ? BlueFacing ?? RedFacing ?? Blue : RedFacing ?? BlueFacing ?? Blue;
}

/// <summary>
/// The spawn and look-at pair each of the game's stages writes into the room's
/// 25-byte player entry.
/// </summary>
/// <remarks>
/// <para>    /// <b>Every row is the stage's own <c>A01</c>/<c>B01</c> home gate.</b> A stage
    /// names up to sixteen placements a side — <c>PRP_HOMEGATE_A01</c>…<c>A16</c> and
    /// <c>PRP_HOMEGATE_B01</c>…<c>B16</c> — and the room's entry is written from the
    /// first pair. Blood Bath's recorded entry is exactly
    /// <c>HOMEGATE_A01 (-23750, 0, 20750)</c> and <c>HOMEGATE_B01 (20750, 0, -23750)</c>;
    /// every row below was read from the same pair in its stage's <c>.geom</c> with
    /// Haven's <c>GeoEffectLayout.GetPositionSlot</c>, the decoder the stage tool
    /// shares. A row is therefore an actual marker, not an average or a replay
    /// position.
    /// </para>
    /// <para>
    /// <b>Rows that named another gate were still on the stage, but on a different
    /// spawn.</b> The earlier table took the marker a replay's side point landed on
    /// (<c>A03</c>/<c>B04</c> and so on), which is a valid marker of the same family
    /// and not the <c>01</c> pair the room entry uses. They are replaced by
    /// <c>A01</c>/<c>B01</c> so one map's entry has the same meaning as every other's.
    /// The heading is taken from which side has the smaller <c>x</c>, the rule Blood
    /// Bath's two captures measure.
    /// </para>
/// <para>
/// <b>The heading is the side across, measured on Blood Bath.</b> The side at the
/// smaller <c>x</c> writes <c>0x5FFF</c> and the other <c>0x0000</c>, which is what
/// the two captures show; a stage whose sides sit the other way round takes the
/// same rule, because the turn is half a stage rather than a fixed side.
/// </para>
/// <para>    /// <b>Three map ids have no home gate pair to read.</b> <c>sm_dd</c> and
    /// <c>sm_ll</c> (17 and 18) state no <c>HOMEGATE</c> placement, so their rows come
    /// from their <c>HOMEBASE</c>/<c>KEROTAN_RUGBY</c> placements instead. <c>n023a</c>
    /// (21) states no <c>HOMEGATE</c> pair in this build, so its row stays the replay
    /// row. <c>19</c> Perilous Palace has no stage in the map table, so
    /// <see cref="For"/> answers it with Blood Bath, the room this host serves by
    /// default. <c>16</c> is the table's unnamed placeholder and is not a playable
    /// stage.
/// </para>
/// </remarks>
public static class MapSpawnUtils
{
    /// <summary>The stage whose pair answers a map with no measured entry.</summary>
    public const int DefaultMap = 7;

    /// <summary>
    /// The spawns by map id, in <see cref="MatchMapUtils"/>'s order.
    /// </summary>
    private static readonly Dictionary<int, MapSpawns> Spawns = new()
    {
        [1] = new(new(24575, -38250), new(0, 43000), new(800, 25000), new(800, -500)), // n001a Forest Firefight, A01/B01
        [2] = new(new(24575, -33000), new(0, 21500), new(800, -74250), new(800, -145000)), // n002a Ambush Alley, A01/B01
        [3] = new(new(0, 20000), new(24575, -18562), new(800, 10500), new(800, 27125)), // n003a Urban Ultimatum, A01/B01
        [4] = new(new(0, 47938), new(24575, -30000), new(800, -29000), new(800, -111500)), // n004a Gronznyj Grad, A01/B01
        [5] = new(new(24575, -29500), new(0, 37500), new(800, -35500), new(800, -10500)), // n005a Coppertown, A01/B01
        [6] = new(new(24575, -14000), new(0, 18500), new(800, -18250), new(800, 22500)), // n006a Hazard House, A01/B01
        // n007a Blood Bath is the measured entry: the solo room writes
        // pos ff 5f 3a a3 ff ff with face 20 03 00 00 0e 51, and the full room
        // adds pos 00 00 0e 51 00 00 with face 20 03 00 00 3a a3. Both sides sit
        // on HOMEGATE_A01 and _B01 exactly, which is the pair every row now uses.
        [7] = new(new(24575, -23750), new(0, 20750), new(800, 20750), new(800, -23750)), // n007a Blood Bath, A01/B01
        [8] = new(new(24575, -37562), new(0, 35500), new(800, 34250), new(800, -38500)), // n008a Tomb of Tubes, A01/B01
        [9] = new(new(24575, -38000), new(0, 40000), new(800, -66000), new(800, -9000)), // n009a Ravaged Riverfront, A01/B01
        [10] = new(new(0, 49500), new(24575, -12000), new(800, -1000), new(800, -18250)), // n010a Silo Sunset, A01/B01
        [11] = new(new(24575, 21500), new(0, 77500), new(800, 20875), new(800, 125250)), // n018a Icebound Inferno, A01/B01
        [12] = new(new(24575, -41875), new(0, 38250), new(800, -111625), new(800, -94312)), // n012a Midtown, A01/B01
        [13] = new(new(0, 50000), new(24575, -49000), new(800, 3000), new(800, -7000)), // n013a Winter Warehouse, A01/B01
        [14] = new(new(0, 1125), new(24575, -53562), new(800, -7875), new(800, 99250)), // n014a Virtuous Vista, A01/B01
        [15] = new(new(0, -3500), new(24575, -37000), new(800, -52500), new(800, 31500)), // n015a Outer Outlet, A01/B01
        [17] = new(new(0, 143350), new(24575, 72050), new(800, 104660), new(800, 73240)), // sm_dd Desert Duel, HOMEBASE_A05/B06
        [18] = new(new(24575, -1080), new(0, -640), new(800, -46110), new(800, 59560)), // sm_ll Lethal Leviathan, KEROTAN_RUGBY_A/B
        [20] = new(new(24575, -79740), new(0, -21861), new(800, 87521), new(800, 39698)), // n022a Quaint Quarrel, A01/B01
        [21] = new(new(24575, -117956), new(0, -25630), new(800, -253554), new(800, -242944)), // n023a Jade Junction, replay row (no HOMEGATE pair)
        [22] = new(new(0, 27571), new(24575, -20966), new(800, -148810), new(800, -77924)), // n020a Eastern Europe, A01/B01
    };

    /// <summary>
    /// The spawn pair a map writes, or the default pair for a map that has no
    /// measured entry.
    /// </summary>
    /// <param name="map">Map id from a rotation triple, or zero when no map is known.</param>
    /// <returns>The map's spawn pair.</returns>
    public static MapSpawns For(int map) =>
        Spawns.TryGetValue(map, out var spawns) ? spawns : Spawns[DefaultMap];

    /// <summary>
    /// Whether a map states its own pair rather than falling back to the default.
    /// </summary>
    /// <param name="map">Map id from a rotation triple.</param>
    /// <returns><c>true</c> when the map has an entry.</returns>
    public static bool IsMeasured(int map) => Spawns.ContainsKey(map);
}
