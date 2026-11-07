using Mgo2Server.Shared.Domain.Characters;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// One point on a stage: two of the three numbers the room's 25-byte player entry
/// carries for it.
/// </summary>
/// <remarks>
/// A spawn states the pair as its heading (<c>+10</c>) and its <c>x</c>, and a
/// look-at point as the <c>y</c> and <c>z</c> of the point it names
/// (<see cref="RoomPlayerEntryBodiesUtils"/>).
/// </remarks>
/// <param name="First">
/// The heading of a spawn, or the <c>y</c> of a look-at point. Blood Bath's solo room
/// writes <c>0x5FFF</c> on the side it spawns on, the full room <c>0x7FFE</c> on that
/// same side and <c>0x0000</c> on the other, which is the turn **[I]**; every look-at
/// point writes <c>800</c>, the ground plus the lift
/// <c>SV_GetInitialPosDir</c> adds.
/// </param>
/// <param name="Along">
/// The <c>x</c> of a spawn, or the <c>z</c> of a look-at point: the axis a stage's two
/// sides sit on, <c>+20750</c> to <c>+23750</c> for one side of Blood Bath and
/// <c>-20750</c> to <c>-25000</c> for the other, over the eight entries a side in a
/// full room.
/// </param>
public readonly record struct MapSpawnPoint(short First, short Along);

/// <summary>
/// Where each side starts on one stage, and which point that side faces.
/// </summary>
/// <remarks>
/// The two points are the two sides of a stage. A round opens with the two teams on
/// opposite sides of it and every player's heading pointing across, so the point a
/// side faces is the other side's spawn: that is the entry's second point, and it is
/// a point rather than a heading in every recorded body. Blood Bath's half of the
/// table is measured from a room of one and from a full room, the rest from the
/// Survival replays' opening frames.
/// </remarks>
/// <param name="Red">The side the raw team byte <c>0</c> puts a player on.</param>
/// <param name="Blue">The side the raw team byte <c>1</c> puts a player on.</param>
/// <param name="RedFacing">
/// The look-at point recorded for raw team 0, where the capture carries one that
/// is not the other side's spawn.
/// </param>
/// <param name="BlueFacing">
/// The look-at point recorded for raw team 1, where the capture carries one that
/// is not the other side's spawn.
/// </param>
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
    /// The point the side a raw team byte states faces: the other side's spawn.
    /// </summary>
    /// <param name="team">Raw team byte, where <c>1</c> is blue and anything else is red.</param>
    /// <returns>The side's look-at point.</returns>
    public MapSpawnPoint FacingOf(byte team) =>
        team == 1 ? BlueFacing ?? Red : RedFacing ?? Blue;
}

/// <summary>
/// The spawn and facing pair each of the game's stages writes into the room's
/// 25-byte player entry.
/// </summary>
/// <remarks>
/// <para>
/// <b>The points are measured from the Survival replays, not from one room.</b>
/// Each stage was read from a completed match of the SaveMGO Survival list
/// (<c>https://mgo2pc.com/api/v2/survival/matches</c>, whose
/// <c>replayUrl</c> is the <c>.dat</c> the replay console parses). The
/// checked-in decoder <c>docs/analysis/replay-console/rpdt.js</c> was run over
/// the downloaded replay; its first frame is the round's opening, and the
/// first position of each player in it is that player's spawn. A side's point
/// below is the member whose spawn is nearest the side's centroid, so it is an
/// actual spawn rather than an average that could land off the floor.
/// </para>
/// <para>
/// <b>The side is the decoder's group, not the replay's raw team byte.</b> The
/// raw team byte is <c>0</c> or <c>2</c>; <c>rpdt.js</c> derives
/// <c>teamGroup</c> <c>0</c> for red and <c>1</c> for blue from the lobby-slot
/// array, and that is the field the table uses. All nineteen rows were checked
/// against the match record's own <c>redPlayers</c>/<c>bluePlayers</c>: the two
/// sets agree, including the one map 5 replay whose red roster has five of the
/// six names the record lists.
/// </para>
/// <para>
/// <b>Coordinates are converted to the entry's wire units.</b> The decoder
/// reports world coordinates at <c>SCALE = 10</c>; the entry carries the
/// signed 16-bit form the position records use, which is the world value
/// divided by <see cref="PlayerPosition.CoordinateScale"/>. That keeps every
/// stage inside the entry's two-byte coordinates — Silo Sunset's far side and
/// Desert Duel's eastern spawn would both overflow as world values.
/// </para>
/// <para>
/// <b>Modes mostly share a side, and Blood Bath does not.</b> The replay for a
/// stage may be a Capture, Bomb, Base, Race, Rescue or Sneaking match, because
/// that is how the stage appeared in the list, and every stage but one occupies
/// the same corner in each of them. Blood Bath is read from its own room entries
/// instead, and its two sides come from two captures:
/// <c>briefing-then-solo-game</c> is a CAP round on it whose slot <c>0a</c> entry
/// writes <c>ff 5f 3a a3 ff ff</c> and <c>20 03 00 00 0e 51</c>, and
/// <c>working-server-4-sessions</c> is a full room on the same stage whose eight
/// entries come in two mirrored pairs — <c>00 00 20 51 00 00 | 20 03 00 00 3a a3</c>
/// with the <c>ff ff 00</c> tail, against the solo body's <c>00 00 00</c> — so the
/// other side is measured rather than mirrored from the first. Every entry of a
/// side sits at one point and its players differ only along it, which is why one
/// point per side is enough.
/// </para>
/// <para>
/// <b>The values are the recordings' shorts, byte for byte.</b> They are ten times
/// the values the replay rows carry for the same stage's corner (the replay row
/// Blood Bath used to carry had that side at <c>(-2275, 2175)</c>), and dividing
/// them to match the replays is what put the live client's camera outside the
/// stage: reading them as world units rather than as the entry's coordinates was
/// the error, so the recordings' bytes are what is written.
/// </para>
/// <para>
/// <b>Two stages have no measured entry.</b> <c>19</c> Perilous Palace and
/// <c>20</c> Quaint Quarrel have no completed Survival match in the list the
/// API serves — the same gap <see cref="MatchMapUtils"/> records — so no replay
/// exists to read. <see cref="For"/> answers them with Blood Bath's pair, which
/// is the room this host already serves by default; a host configured for one
/// of them needs a replay before its camera can be placed. <c>16</c> is the
/// table's unnamed placeholder and is not a playable stage.
/// </para>
/// <para>
/// The trailing three bytes of the recorded entry are not stage coordinates:
/// the measured map 7 bodies carry <c>ff ff 00</c> on side zero and
/// <c>00 00 00</c> on side one, and those two patterns are written unchanged.
/// </para>
/// </remarks>
public static class MapSpawnUtils
{
    /// <summary>The stage whose pair answers a map with no measured entry.</summary>
    public const int DefaultMap = 7;

    /// <summary>
    /// The measured spawns by map id, in <see cref="MatchMapUtils"/>'s order.
    /// </summary>
    private static readonly Dictionary<int, MapSpawns> Spawns = new()
    {
        [1] = new(new(-3625, 2500), new(4400, 100)), // n001a Forest Firefight, replay_378527_11
        [2] = new(new(-3300, -7325), new(1825, -14500)), // n002a Ambush Alley, replay_378520_18
        [3] = new(new(3062, -200), new(-1150, 5012)), // n003a Urban Ultimatum, replay_378522_14
        [4] = new(new(4900, -2900), new(-2850, -11150)), // n004a Gronznyj Grad, replay_378527_13
        [5] = new(new(-3575, -775), new(3475, -2400)), // n005a Coppertown, replay_378521_14
        [6] = new(new(-1250, -1825), new(2000, 2250)), // n006a Hazard House, replay_378525_15
        // n007a Blood Bath, two captures: briefing-then-solo-game's slot 0a entry
        // for the side the round spawns on (pos ff 5f 3a a3 ff ff, face
        // 20 03 00 00 0e 51) and working-server-4-sessions' eight entries for both
        // sides, which put the other side at pos 00 00 0e 51 00 00 with face
        // 20 03 00 00 3a a3 and the ffff00 tail.
        [7] = new(
            new(24575, -23750),
            new(0, 20750),
            new(800, 20750),
            new(800, -23750)),
        [8] = new(new(-3606, 3425), new(3700, -3850)), // n008a Tomb of Tubes, replay_378519_15
        [9] = new(new(1300, -5950), new(-3850, -6300)), // n009a Ravaged Riverfront, replay_378518_12
        [10] = new(new(5150, 0), new(-975, -1675)), // n010a Silo Sunset, replay_378523_18
        [11] = new(new(2600, 893), new(5652, 14439)), // n018a Icebound Inferno, replay_378523_16
        [12] = new(new(-4087, -11162), new(3925, -9331)), // n012a Midtown, replay_378517_11
        [13] = new(new(5100, 300), new(-4800, -700)), // n013a Winter Warehouse, replay_378525_9
        [14] = new(new(112, -787), new(-5035, 10843)), // n014a Virtuous Vista, replay_378517_10
        [15] = new(new(-3900, -800), new(4500, -500)), // n015a Outer Outlet, replay_378526_19
        [17] = new(new(14335, 10466), new(7205, 7324)), // sm_dd Desert Duel, replay_378520_17
        [18] = new(new(-108, -4611), new(-64, 5956)), // sm_ll Lethal Leviathan, replay_378519_14
        [21] = new(new(-11562, -26189), new(-2550, -24768)), // n023a Jade Junction, replay_372883_1
        [22] = new(new(2610, -14848), new(-1946, -7800)), // n020a Eastern Europe, replay_378520_16
    };

    /// <summary>
    /// The spawn pair a map writes, or the default pair for a map that has no
    /// measured replay.
    /// </summary>
    /// <param name="map">Map id from a rotation triple, or zero when no map is known.</param>
    /// <returns>The map's spawn pair.</returns>
    public static MapSpawns For(int map) =>
        Spawns.TryGetValue(map, out var spawns) ? spawns : Spawns[DefaultMap];

    /// <summary>
    /// Whether a map's pair came from a replay rather than the fallback.
    /// </summary>
    /// <param name="map">Map id from a rotation triple.</param>
    /// <returns><c>true</c> when the map has a measured entry.</returns>
    public static bool IsMeasured(int map) => Spawns.ContainsKey(map);
}
