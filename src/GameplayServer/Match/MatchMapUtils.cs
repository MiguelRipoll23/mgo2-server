namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// The maps a room's rotation can carry: each id's stage name and the name a
/// player knows it by.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both columns are the replay console's own table.</b> <c>MAPS</c> in
/// <c>docs/analysis/replay-console/rpdt.js</c> is what
/// <c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2 measures a room's rotation
/// against, and its pairs — <c>n001a</c> Forest Firefight, <c>n007a</c> Blood
/// Bath, <c>n020a</c> Eastern Europe — are the disc's stage names beside the
/// names the rooms were published under. It is ported here rather than
/// re-derived, because a name is how a person states a map in a configuration
/// and the stage name is what the client's own files call it, and both are
/// already measured in one table.
/// </para>
/// <para>
/// Two ids are named without a stage and one is owned by no name at all: <c>16</c>
/// is the placeholder the table itself writes as unknown, and <c>19</c> is
/// Perilous Palace, whose stage the table leaves unnamed — the game's own stage
/// list has it as <c>n024a</c>, which the viewer cannot draw yet. A map that is
/// named has a name; a map that is not is answered with <c>null</c> rather than
/// with a stage this server guessed.
/// </para>
/// <para>
/// A name the table does not hold is refused rather than mapped onto a nearby id.
/// A room whose map no client can load is a room the client is not in, so
/// <see cref="Parse"/> throws and the caller with a name to resolve decides what
/// an unresolvable one costs.
/// </para>
/// </remarks>
public static class MatchMapUtils
{
    /// <summary>
    /// Every map a rotation can carry, in id order: the id, the stage name the
    /// disc ships it as where the table names one, and the name it is published
    /// under.
    /// </summary>
    private static readonly (int Map, string? Stage, string? Name)[] Maps =
    [
        (1, "n001a", "Forest Firefight"),
        (2, "n002a", "Ambush Alley"),
        (3, "n003a", "Urban Ultimatum"),
        (4, "n004a", "Gronznyj Grad"),
        (5, "n005a", "Coppertown"),
        (6, "n006a", "Hazard House"),
        (7, "n007a", "Blood Bath"),
        (8, "n008a", "Tomb of Tubes"),
        (9, "n009a", "Ravaged Riverfront"),
        (10, "n010a", "Silo Sunset"),
        (11, "n018a", "Icebound Inferno"),
        (12, "n012a", "Midtown"),
        (13, "n013a", "Winter Warehouse"),
        (14, "n014a", "Virtuous Vista"),
        (15, "n015a", "Outer Outlet"),

        // The table's own placeholder: the id exists and nothing names it.
        (16, null, null),

        (17, "sm_dd", "Desert Duel"),
        (18, "sm_ll", "Lethal Leviathan"),

        // 19-22 were one stage off until 2026-10-07. MEASURED by standing the
        // tracks of real matches on each stage's floor: map 21 (match 68994) =
        // n023a 98%, map 22 (match 69003) = n020a 100%, every other stage ~0%.
        // 19 and 20 follow the game's own stage table (lobby scenerio.gcx:
        // 19 = n024a, 20 = n022a); no match on either was found to test.
        (19, null, "Perilous Palace"),
        (20, "n022a", "Quaint Quarrel"),
        (21, "n023a", "Jade Junction"),
        (22, "n020a", "Eastern Europe"),
    ];

    /// <summary>The name a map is known by, or <c>null</c> when the id is no map.</summary>
    /// <param name="map">Map id from a rotation triple.</param>
    /// <returns>The map's name, or <c>null</c>.</returns>
    public static string? Name(int map)
    {
        foreach (var entry in Maps)
        {
            if (entry.Map == map)
            {
                return entry.Name;
            }
        }

        return null;
    }

    /// <summary>The stage a map is drawn on, or <c>null</c> when none is named for it.</summary>
    /// <param name="map">Map id from a rotation triple.</param>
    /// <returns>The stage name, or <c>null</c>.</returns>
    public static string? Stage(int map)
    {
        foreach (var entry in Maps)
        {
            if (entry.Map == map)
            {
                return entry.Stage;
            }
        }

        return null;
    }

    /// <summary>
    /// The map a room moves to after this one: the next entry of the table that has
    /// both a stage name and a measured spawn, wrapping at its end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It walks the maps whose stage and spawn are both measured.</b> Id <c>16</c>
    /// is the table's own placeholder, so a rotation that landed on it would publish
    /// a room nobody can ask for and a spawn nothing measured. Id <c>19</c> is the
    /// other one: Perilous Palace has a name and no stage, which is a stage this
    /// server can neither state in a record nor place a spawn on — the pair it
    /// writes for it is Blood Bath's. A room rotated onto it therefore hands its
    /// client a stage nothing here measured, and the join of 30 October 14:27 is
    /// what that costs: the client loaded <c>n024a</c> and the stage failed every
    /// asset check it printed (`dg_texture.cpp:3025` null, thirty-nine texture
    /// overflows, `brk_light.cc:1017`, `windowpane_mng.cc:297`,
    /// `crt_monitor.cc:781` and `tree_test_02.cc:1149` prop directories not found),
    /// which is the black screen that join ended on
    /// (`docs/analysis/join-stall-after-mode2.md` §71).
    /// </para>
    /// <para>
    /// <b>Id <c>20</c> is skipped for the same reason and not for a missing stage.</b>
    /// Quaint Quarrel has a stage name, <c>n022a</c>, and no completed match in the
    /// Survival list, so <see cref="MapSpawnUtils"/> places it with Blood Bath's pair.
    /// The join of 30 October 13:46 landed on it: the client loaded <c>n022a</c>, the
    /// stage script ran, and the collision unit then reported a <c>NAN ERROR</c> for
    /// every trace it took — five `unknown chara-class 0` checks and seventeen
    /// thousand collision failures before the window closed — which is the same black
    /// screen in a stage this server states but cannot place a player in. So the
    /// rotation now requires a measured spawn rather than a name alone, and 19 and 20
    /// are both out of it until a replay of each is read
    /// (`docs/analysis/join-stall-after-mode2.md` §75).
    /// </para>
    /// <para>
    /// An id the table does not hold — a row written by an older build — starts the
    /// walk at the table's first map instead of leaving the room without one.
    /// </para>
    /// </remarks>
    /// <param name="map">Map the room played last, or zero when none is known.</param>
    /// <returns>The map the room plays next.</returns>
    public static int Next(int map)
    {
        var playable = Maps
            .Where(entry => entry.Stage is not null && MapSpawnUtils.IsMeasured(entry.Map))
            .Select(entry => entry.Map)
            .ToArray();
        return playable[(Array.IndexOf(playable, map) + 1) % playable.Length];
    }

    /// <summary>The map id a name states.</summary>
    /// <remarks>
    /// Either column of the table answers, because both are names a person was
    /// given: the room's own — Blood Bath — and the stage the disc ships — n007a.
    /// Matching is without case, as a configuration is written by a person rather
    /// than sent on a wire.
    /// </remarks>
    /// <param name="name">Map name or stage name.</param>
    /// <returns>The map id.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the name is no map.</exception>
    public static int Parse(string name) =>
        TryParse(name, out var map)
            ? map
            : throw new InvalidOperationException(
                $"'{name}' is not a map a room's rotation can carry; it is one of {Names}.");

    /// <summary>
    /// Whether a name is one of the maps, and the id it states.
    /// </summary>
    /// <param name="name">Name to look up.</param>
    /// <param name="map">Map id the name states, or zero when it is no map.</param>
    /// <returns><c>true</c> when the name is one of the maps.</returns>
    public static bool TryParse(string? name, out int map)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var trimmed = name.Trim();
            foreach (var entry in Maps)
            {
                if (Matches(entry.Name, trimmed) || Matches(entry.Stage, trimmed))
                {
                    map = entry.Map;
                    return true;
                }
            }
        }

        map = 0;
        return false;
    }

    /// <summary>Every map name, for an error message that tells a reader what to write.</summary>
    private static string Names => string.Join(", ", Maps.Where(entry => entry.Name is not null).Select(entry => entry.Name));

    /// <summary>Whether one column of a map's entry is the name being looked up.</summary>
    /// <param name="candidate">The entry's name, or <c>null</c> when it states none.</param>
    /// <param name="name">Name being looked up.</param>
    /// <returns><c>true</c> when they are the same name.</returns>
    private static bool Matches(string? candidate, string name) =>
        candidate is not null && string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase);
}
