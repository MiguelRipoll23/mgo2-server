namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The patrol routes the working host's bots actually walked, one per slot, read
/// off <c>game-with-bots-workingserver.pcapng</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are recorded tracks and not a lattice.</b> Each slot's position
/// records over one walking stretch of the capture were reduced to their turning
/// points with a 450-unit tolerance; each entry below is one such stretch, in
/// stage units, in the order the bot walked them. Slot 1's is
/// <c>x 21270 → 22230</c> then <c>22150 → 19100 → -2170</c>, twelve seconds of
/// walking; slot 7's is twenty-seven seconds and crosses the stage twice.
/// </para>
/// <para>
/// <b>Why a table instead of the stage's placements.</b> A patrol over the
/// placements picks its points by slot and cursor, so a bot that arrives at one
/// takes the next one from an arithmetic stride and can turn back on the point it
/// just left — the room of 2026-11-11 circled on the spot for exactly that
/// reason: consecutive waypoints lay on opposite sides of the body, and the
/// heading flipped a half-turn every beat. A recorded track is a path somebody
/// actually walked, and no two slots' tracks are alike.
/// </para>
/// <para>
/// The heights are recorded too and are not used to walk on: a route moves a body
/// across the stage and never up or down it
/// (<see cref="BotLocomotionService.Step"/>). A slot with no track — a map other
/// than Blood Bath, or a room whose bots outnumber the capture's — falls back to
/// the stage's own points (<see cref="BotMovementDestinationService.ChoosePatrol"/>).
/// </para>
/// </remarks>
public static class BotPatrolRouteUtils
{
    /// <summary>Map id of <c>n007a</c> Blood Bath, the stage the tracks were read on.</summary>
    public const int BloodBathMap = 7;

    /// <summary>One recorded walking stretch per slot, in stage units.</summary>
    private static readonly Dictionary<byte, BotPosition[]> BloodBath = new()
    {
        [1] = Route((21270, 610, 21190), (22230, 980, 21100), (22150, 1000, 20240), (19100, 1000, 16650), (-2170, 1000, 8560)),
        [2] = Route((1460, 1000, -10120), (-2820, 1000, -10570), (-14410, 1200, -15330), (-21140, 1200, -15490), (-24300, 1000, -13510), (-19000, 1000, -6290), (-6880, 1390, -6650), (-800, 1000, -7770)),
        [3] = Route((-830, 1010, 19880), (11990, 1000, 23480), (23220, 1000, 24310), (24140, 1000, 14730), (22670, 1000, 9900), (16080, 1110, 3580), (3830, 1000, 9680), (-3830, 920, 7950)),
        [4] = Route((18300, 1000, -12200), (20400, 1000, -11910), (17970, 1320, 550), (12030, 1000, 9890), (9580, 1000, -5660), (-380, 1000, -7950), (-13690, 1200, -14710)),
        [5] = Route((16200, 1000, -24260), (15070, 1000, -23140), (7220, 1000, -22280), (-560, 1000, -18470), (-3280, 1000, -15850), (-4120, 1000, -12620), (-12570, 870, -10560), (-6000, 1000, 7290), (60, 1000, 11500), (10040, 1130, 14250)),
        [6] = Route((9340, 1400, 19250), (5300, 1000, 12900), (2170, 1000, 10460), (2350, 1000, 9990), (5460, 1000, 12580), (5440, 1000, 17430), (-1000, 1000, 19650), (-6940, 1000, 12660), (-19380, 1000, 8750), (-21900, 1000, 4300), (-22720, 1020, -9140), (-23880, 1000, -10890)),
        [7] = Route((18950, 920, 8910), (20450, 940, 9180), (16550, 1040, 6280), (10710, 1000, -4680), (2310, 1010, -9790), (-12400, 870, -10500), (-12800, 900, -16420), (-15860, 840, -21340), (-22660, 1000, -22920), (-17020, 800, -21610), (-14490, 1000, -17450), (-13710, 1000, -17340), (-9000, 980, -11210), (9590, 1000, -5160), (14880, 1000, 6400), (20170, 1000, 9760), (20350, 1380, 10700)),
        [8] = Route((2780, 800, -24500), (6760, 840, -22580), (14360, 900, -22060), (18230, 800, -20880), (20120, 800, -17230), (23090, 800, -4570), (22970, 820, -2480), (20080, 920, 1630), (14220, 800, 6700), (11710, 1280, 17550), (5910, 800, 23080), (3560, 800, 23970), (-1340, 880, 24060), (-8300, 760, 25950), (-11930, 800, 25420)),
    };

    /// <summary>
    /// The recorded track a bot in a slot walks, or none when this build holds no
    /// track for the map and slot.
    /// </summary>
    /// <param name="map">The round's map, in the replay decoder's numbering.</param>
    /// <param name="sessionSlot">Slot the bot holds, which picks the track.</param>
    /// <returns>The track's points, oldest first; empty when there is none.</returns>
    public static IReadOnlyList<BotPosition> RouteOf(int map, byte sessionSlot) =>
        map == BloodBathMap && BloodBath.TryGetValue(sessionSlot, out var route) ? route : [];

    /// <summary>Turns recorded triples into the positions the router takes.</summary>
    /// <param name="points">The recorded points, in stage units.</param>
    /// <returns>The same points as positions.</returns>
    private static BotPosition[] Route(params (float X, float Y, float Z)[] points) =>
        [.. points.Select(point => new BotPosition(point.X, point.Y, point.Z))];
}
