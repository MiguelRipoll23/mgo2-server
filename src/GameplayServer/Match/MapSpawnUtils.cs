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
/// <para>
/// <b>Every stage states its own spawns, and the pair comes from them.</b> The
/// <c>.geom</c> props tree names sixteen placements a side:
/// <c>PRP_HOMEGATE_A01</c>…<c>A16</c> and <c>PRP_HOMEGATE_B01</c>…<c>B16</c>, and a
/// room writes one of each. Blood Bath's recorded entry is exactly
/// <c>HOMEGATE_A01 (-23750, 0, 20750)</c> and <c>HOMEGATE_B01 (20750, 0, -23750)</c>,
/// at distance zero, and Forest Firefight's is <c>A03</c> and <c>B04</c>, also at
/// distance zero. A row below is the placement the measured side point landed on,
/// so a stage's spawn is an actual marker rather than an average.
/// </para>
/// <para>
/// <b>The earlier rows were a tenth of these and that is why only Blood Bath
/// worked.</b> The replays were decoded at <c>SCALE = 10</c> and the rows were
/// written in the decoded units, while the entry carries stage units. Blood Bath
/// escaped because its row was measured from a room's own entry bytes, in the
/// units the entry uses. Rows 3, 5, 9, 11 and 15 were never on a marker at all:
/// their nearest home gate is 11 to 49 thousand units away, and 15's red row is
/// nearer a <c>B</c> marker than an <c>A</c> one.
/// </para>
/// <para>
/// <b>The heading is the side across, measured on Blood Bath.</b> The side at the
/// smaller <c>x</c> writes <c>0x5FFF</c> and the other <c>0x0000</c>, which is what
/// the two captures show; a stage whose sides sit the other way round takes the
/// same rule, because the turn is half a stage rather than a fixed side.
/// </para>
/// <para>
/// <b>Three stages have no home gate to read.</b> <c>sm_dd</c> and <c>sm_ll</c>
/// (maps 17 and 18) state no <c>HOMEGATE</c> placement, so their rows are the
/// replay rows scaled once and are the only two that are not anchored. <c>19</c>
/// Perilous Palace and <c>20</c> Quaint Quarrel have no completed Survival match
/// and no stage in this build, so <see cref="For"/> answers them with Blood Bath,
/// the room this host serves by default. <c>16</c> is the table's unnamed
/// placeholder and is not a playable stage.
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
        [1] = new(new(24575, -36250), new(0, 44000), new(800, 25000), new(800, 1000)), // n001a Forest Firefight, anchored HOMEGATE_A03/B04
        [2] = new(new(24575, -33000), new(0, 21500), new(800, -73250), new(800, -145000)), // n002a Ambush Alley, anchored HOMEGATE_A02/B01
        [3] = new(new(0, 20000), new(24575, -20562), new(800, 7500), new(800, 31125)), // n003a Urban Ultimatum, anchored HOMEGATE_A04/B12
        [4] = new(new(0, 49000), new(24575, -28500), new(800, -29000), new(800, -111500)), // n004a Gronznyj Grad, anchored HOMEGATE_A03/B02
        [5] = new(new(24575, -29500), new(0, 37500), new(800, -35500), new(800, -10500)), // n005a Coppertown, anchored HOMEGATE_A01/B01
        [6] = new(new(24575, -12500), new(0, 20000), new(800, -18250), new(800, 22500)), // n006a Hazard House, anchored HOMEGATE_A02/B02
        // n007a Blood Bath, measured from two captures and unchanged: the
        // solo room writes pos ff 5f 3a a3 ff ff with face 20 03 00 00 0e 51,
        // and the full room adds the side at pos 00 00 0e 51 00 00 with face
        // 20 03 00 00 3a a3. Both sides sit on HOMEGATE_A01 and _B01 exactly.
        [7] = new(
            new(24575, -23750),
            new(0, 20750),
            new(800, 20750),
            new(800, -23750)),
        [8] = new(new(24575, -36062), new(0, 37000), new(800, 34250), new(800, -38500)), // n008a Tomb of Tubes, anchored HOMEGATE_A02/B02
        [9] = new(new(24575, -33000), new(0, 40000), new(800, -64500), new(800, -9000)), // n009a Ravaged Riverfront, anchored HOMEGATE_A/B01
        [10] = new(new(0, 51500), new(24575, -9750), new(800, 0), new(800, -16750)), // n010a Silo Sunset, anchored HOMEGATE_A04/B04
        [11] = new(new(24575, 23000), new(0, 76000), new(800, 19438), new(800, 126750)), // n018a Icebound Inferno, anchored HOMEGATE_A04/B13
        [12] = new(new(24575, -40875), new(0, 39250), new(800, -111625), new(800, -93312)), // n012a Midtown, anchored HOMEGATE_A02/B05
        [13] = new(new(0, 51000), new(24575, -48000), new(800, 3000), new(800, -7000)), // n013a Winter Warehouse, anchored HOMEGATE_A02/B02
        [14] = new(new(0, 1125), new(24575, -53500), new(800, -7875), new(800, 105000)), // n014a Virtuous Vista, anchored HOMEGATE_A01/B
        [15] = new(new(0, -4500), new(24575, -35000), new(800, -49500), new(800, 31500)), // n015a Outer Outlet, anchored HOMEGATE_A13/B04
        [17] = new(new(0, 143350), new(24575, 72050), new(800, 104660), new(800, 73240)), // sm_dd Desert Duel, anchored PRP_HOMEBASE_A05/B06 (d<=8)
        [18] = new(new(24575, -1080), new(0, -640), new(800, -46110), new(800, 59560)), // sm_ll Lethal Leviathan, anchored KEROTAN_RUGBY_A/B (d<=6)
        [20] = new(new(24575, -79740), new(0, -18902), new(800, 87521), new(800, 38237)), // n022a Quaint Quarrel, anchored HOMEGATE_A01/B01
        [21] = new(new(24575, -117956), new(0, -25630), new(800, -253554), new(800, -242944)), // n023a Jade Junction, anchored HOMEGATE_A04/B13
        [22] = new(new(0, 26106), new(24575, -19468), new(800, -148488), new(800, -78004)), // n020a Eastern Europe, anchored HOMEGATE_A03/B03
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
