namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// The objective points of one stage, one array per rule that has them.
/// </summary>
/// <remarks>
/// The six arrays are the markers the stage's own script hands to its target
/// placement: Capture's two <c>PRP_RUGBY_TGT_01/02</c>, Rescue's
/// <c>PRP_RES_TGT_01</c>, Team Sneaking's <c>PRP_TEAM_SNEAKING_TGT_01/02</c>,
/// Bomb's <c>PRP_TGT_BOMB</c>, Sneaking Capture's <c>PRP_SCAP_TGT_00</c> and Race's
/// <c>PRP_TRACE_TGT_A/B</c>. Each is in the wire's own form
/// (<see cref="MapCustomPoint"/>).
/// </remarks>
/// <param name="Capture">Capture's targets; Interval's script places the same pair.</param>
/// <param name="Rescue">Rescue's single target.</param>
/// <param name="TeamSneaking">Team Sneaking's two dolls.</param>
/// <param name="Bomb">Bomb's single site.</param>
/// <param name="SneakingCapture">Sneaking Capture's single target.</param>
/// <param name="Race">Race's two trace targets, A then B.</param>
public readonly record struct MapCustomPointSets(
    MapCustomPoint[] Capture,
    MapCustomPoint[] Rescue,
    MapCustomPoint[] TeamSneaking,
    MapCustomPoint[] Bomb,
    MapCustomPoint[] SneakingCapture,
    MapCustomPoint[] Race);

/// <summary>
/// Every stage's objective points, read from the stage's own <c>.geom</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each row is a placement in the stage's props tree, not a measured wire
/// body.</b> The names are the procs' own — the props the target placement is
/// given — and the coordinates are Haven's <c>GeoEffectLayout</c> position slot,
/// read with <c>tools/stage-navigation/Stage/StageGeomFileUtils.cs</c>. The
/// conversion to the wire's form is <see cref="MapCustomPoint"/>'s: x and z ÷ 10,
/// y + 900. Four captured records reproduce four of these rows exactly, which is
/// what fixes the conversion (<c>docs/analysis/custom-points.md</c> §2).
/// </para>
/// <para>
/// <b>The map ids are <see cref="MatchMapUtils"/>'s, and the two that share a
/// stage name read the geometry that stage ships.</b> <c>sm_dd</c> is drawn from
/// <c>n021a.geom</c> and <c>sm_ll</c> from <c>n019a.geom</c>, the two files the
/// disc puts under those directories' names. A map the table does not hold — the
/// rotation's unnamed placeholder — is answered with <c>false</c> and no points
/// rather than with another map's.
/// </para>
/// <para>
/// <b>To regenerate:</b> the props are the six names above resolved in each
/// stage's <c>.geom</c> through Haven's dictionary, and the row is
/// <c>(trunc(x / 10), trunc(y + 900), trunc(z / 10))</c>. A stage whose props
/// move is a row a new capture verifies, not a value to hand-tune.
/// </para>
/// </remarks>
public static class CustomPointTableUtils
{
    /// <summary>The stages' points, by map id.</summary>
    private static readonly Dictionary<int, MapCustomPointSets> Points = new()
    {
        // n001a — Forest Firefight
        [1] = new(
            Capture: [new(-2900, 1900, 1500), new(2800, 1175, 500)],
            Rescue: [new(-3700, 1900, 2700)],
            TeamSneaking: [new(-3700, 1900, 2700), new(-2950, 2556, -2450)],
            Bomb: [new(3600, 879, 756)],
            SneakingCapture: [new(-400, 600, 100)],
            Race: [new(-3743, 1915, 2168), new(4150, 735, 100)]),

        // n002a — Ambush Alley
        [2] = new(
            Capture: [new(1150, 1262, -11175), new(-1375, 893, -8825)],
            Rescue: [new(-1200, 4900, -7550)],
            TeamSneaking: [new(-1550, 962, -9000), new(1000, 900, -7300)],
            Bomb: [new(1893, 974, -14393)],
            SneakingCapture: [new(-100, -381, -10000)],
            Race: [new(-2825, 900, -7381), new(1750, 900, -14350)]),

        // n003a — Urban Ultimatum
        [3] = new(
            Capture: [new(2656, 900, -2493), new(-662, 2900, 3887)],
            Rescue: [new(-700, 4900, 481)],
            TeamSneaking: [new(-1150, 10930, -600), new(-762, 12775, 2568)],
            Bomb: [new(2893, 4900, -1043)],
            SneakingCapture: [new(400, 4900, -137)],
            Race: [new(1350, 16775, 850), new(-1650, 14800, 2850)]),

        // n004a — Gronznyj Grad
        [4] = new(
            Capture: [new(3593, 775, -9137), new(1075, 900, -3075)],
            Rescue: [new(-2962, 775, -9550)],
            TeamSneaking: [new(6812, 775, -9887), new(-2956, 775, -9300)],
            Bomb: [new(4150, 775, -2893)],
            SneakingCapture: [new(1200, 8900, -7856)],
            Race: [new(4300, 1025, -2750), new(-2900, 1150, -10350)]),

        // n005a — Coppertown
        [5] = new(
            Capture: [new(281, 900, -93), new(-981, 900, -2262)],
            Rescue: [new(3450, 6025, -1581)],
            TeamSneaking: [new(3431, 900, -2387), new(2943, 900, 956)],
            Bomb: [new(-3100, 802, -500)],
            SneakingCapture: [new(-500, 863, -400)],
            Race: [new(-1950, 900, -3350), new(3450, 6025, -750)]),

        // n006a — Hazard House
        [6] = new(
            Capture: [new(-3100, 5900, 512), new(2700, 5900, -1550)],
            Rescue: [new(2350, 775, 2125)],
            TeamSneaking: [new(2350, 775, 2125), new(1762, 900, -1162)],
            Bomb: [new(3150, 1045, -306)],
            SneakingCapture: [new(-400, 900, 100)],
            Race: [new(-1225, 900, -1506), new(2100, 900, 2025)]),

        // n007a — Blood Bath
        [7] = new(
            Capture: [new(900, 900, -706), new(-706, 893, 775)],
            Rescue: [new(1981, 900, -1912)],
            TeamSneaking: [new(1687, -3099, -2512), new(2168, 900, -1668)],
            Bomb: [new(-2450, 909, 1450)],
            SneakingCapture: [new(100, -5100, 87)],
            Race: [new(-2500, 916, 1800), new(2500, 900, -1800)]),

        // n008a — Tomb of Tubes
        [8] = new(
            Capture: [new(-1443, 2900, -2587), new(-1125, 2900, 1456)],
            Rescue: [new(2387, 2900, 1543)],
            TeamSneaking: [new(2856, 2900, 1100), new(-243, 3400, 3806)],
            Bomb: [new(-900, 1900, -2900)],
            SneakingCapture: [new(-493, 950, -856)],
            Race: [new(-3000, 3900, 3300), new(3600, 6900, -3200)]),

        // n009a — Ravaged Riverfront
        [9] = new(
            Capture: [new(-1300, 775, -4900), new(1300, 654, -2100)],
            Rescue: [new(-1900, 900, -5550)],
            TeamSneaking: [new(-675, -1100, -750), new(-2900, 900, -5800)],
            Bomb: [new(2500, 1024, -4250)],
            SneakingCapture: [new(0, 2400, -3500)],
            Race: [new(-3150, 900, -6600), new(3650, 900, -450)]),

        // n010a — Silo Sunset
        [10] = new(
            Capture: [new(100, -5109, 125), new(4475, -5099, 1256)],
            Rescue: [new(162, -5099, -100)],
            TeamSneaking: [new(-856, -8099, 250), new(1150, -2099, -1150)],
            Bomb: [new(4475, -5100, 1256)],
            SneakingCapture: [new(1843, -98, 1068)],
            Race: [new(4750, -5100, 0), new(-450, -5100, -1500)]),

        // n018a — Icebound Inferno
        [11] = new(
            Capture: [new(8500, -11021, 7500), new(-500, -9761, 7493)],
            Rescue: [new(3650, -11678, 3943)],
            TeamSneaking: [new(700, -11396, 4400), new(8050, -11324, 3450)],
            Bomb: [new(4400, -11748, 15100)],
            SneakingCapture: [new(3850, -12100, 7500)],
            Race: [new(2137, -11230, 2812), new(7737, -12100, 12000)]),

        // n012a — Midtown
        [12] = new(
            Capture: [new(3925, 900, -8637), new(-4075, 900, -10950)],
            Rescue: [new(3400, 900, -7500)],
            TeamSneaking: [new(-2750, 1275, -11600), new(-2750, 1025, -8250)],
            Bomb: [new(-6293, 900, -10868)],
            SneakingCapture: [new(0, 900, -9900)],
            Race: [new(-4050, 900, -10550), new(3925, 900, -9750)]),

        // n013a — Winter Warehouse
        [13] = new(
            Capture: [new(5200, 6233, -1350), new(-3600, 10435, -1850)],
            Rescue: [new(100, 6650, -750)],
            TeamSneaking: [new(0, 11900, -1400), new(-2450, 5650, 700)],
            Bomb: [new(-4250, 6025, -543)],
            SneakingCapture: [new(650, 1152, -862)],
            Race: [new(4650, 5525, 450), new(-4200, 5989, -550)]),

        // n014a — Virtuous Vista
        [14] = new(
            Capture: [new(2725, 8848, 3793), new(81, 2900, -3381)],
            Rescue: [new(-37, 3900, -1262)],
            TeamSneaking: [new(-37, 3900, -1262), new(1781, 2135, -800)],
            Bomb: [new(-1306, 4089, 10200)],
            SneakingCapture: [new(125, 1137, 1514)],
            Race: [new(-50, 2900, -150), new(-5200, 628, 9600)]),

        // n015a — Outer Outlet
        [15] = new(
            Capture: [new(3350, 900, -400), new(-5450, -3100, -1550)],
            Rescue: [new(-3250, -3100, 150)],
            TeamSneaking: [new(-3250, -3100, 150), new(2756, 900, 1400)],
            Bomb: [new(-3450, -3100, -650)],
            SneakingCapture: [new(-1200, 900, -481)],
            Race: [new(-300, 900, -4700), new(-3300, -1100, 3300)]),

        // sm_dd — Desert Duel, drawn from n021a's geometry
        [17] = new(
            Capture: [new(14675, -1896, 12569), new(8131, 632, 7506)],
            Rescue: [new(7149, 800, 11567)],
            TeamSneaking: [new(13080, -500, 11249), new(13489, -1100, 7720)],
            Bomb: [new(5712, 469, 10456)],
            SneakingCapture: [new(9916, 4774, 9027)],
            Race: [new(14181, -1556, 7732), new(6090, 171, 9420)]),

        // sm_ll — Lethal Leviathan, drawn from n019a's geometry
        [18] = new(
            Capture: [new(2, 1450, -4103), new(1, 1450, 5579)],
            Rescue: [new(0, 1450, -4136)],
            TeamSneaking: [new(1110, 1450, -3026), new(-1118, 1450, -3004)],
            Bomb: [new(-1632, 1450, -3859)],
            SneakingCapture: [new(0, 1150, 866)],
            Race: [new(0, 1150, -4298), new(0, 1150, 5900)]),

        // n024a — Perilous Palace
        [19] = new(
            Capture: [new(-14082, 12300, -23396), new(-5986, 10900, -17203)],
            Rescue: [new(-13204, 13750, -21652)],
            TeamSneaking: [new(-8246, 12638, -20240), new(-10423, 12400, -23617)],
            Bomb: [new(-6688, 10845, -22852)],
            SneakingCapture: [new(-10115, 12700, -20566)],
            Race: [new(-13780, 11875, -17602), new(-6360, 9825, -23436)]),

        // n022a — Quaint Quarrel
        [20] = new(
            Capture: [new(-7945, 1700, 8852), new(-2011, 1700, 3900)],
            Rescue: [new(-2466, 1900, 3920)],
            TeamSneaking: [new(-7978, 1900, 6428), new(-4145, 1900, 8308)],
            Bomb: [new(-7836, 1400, 8607)],
            SneakingCapture: [new(-4749, 1700, 5480)],
            Race: [new(-7387, 1700, 8666), new(-2503, 1700, 3635)]),

        // n023a — Jade Junction
        [21] = new(
            Capture: [new(-3234, 6076, -24547), new(-11504, 5663, -24208)],
            Rescue: [new(-5427, 4875, -24699)],
            TeamSneaking: [new(-5150, 4875, -24725), new(-5132, 5034, -27747)],
            Bomb: [new(-11504, 5663, -24208)],
            SneakingCapture: [new(-9245, 12984, -25500)],
            Race: [new(-11585, 5331, -24115), new(-3234, 6076, -24547)]),

        // n020a — Eastern Europe
        [22] = new(
            Capture: [new(3464, 4400, -8740), new(-2851, 4400, -14138)],
            Rescue: [new(-782, 5900, -9204)],
            TeamSneaking: [new(-1903, 6800, -11986), new(800, 6800, -8617)],
            Bomb: [new(2882, 4300, -15026)],
            SneakingCapture: [new(800, 11900, -10577)],
            Race: [new(2652, 4400, -14454), new(-1954, 4400, -8291)]),
    };

    /// <summary>The points a map states, or <c>false</c> when the table has no row for it.</summary>
    /// <param name="map">Map id from a round's rotation entry.</param>
    /// <param name="sets">The stage's six point sets.</param>
    /// <returns><c>true</c> when the map has a row.</returns>
    public static bool TryFor(int map, out MapCustomPointSets sets) =>
        Points.TryGetValue(map, out sets);
}
