namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The room's map list, as the rules-roster identifiers carry it.
/// </summary>
/// <remarks>
/// The grammar is the replay console's own <c>rulesRoster()</c>
/// (<c>docs/analysis/replay-console/rpdt.js</c>): groups of (group id, members)
/// each ended by <c>0xfe</c>, the whole list ended by <c>0xfe</c>. Every
/// single-group list on the bench opens on <see cref="MapListGroup"/> and lists
/// values in the map-id range, which is what this writes. Which member is the
/// round's current map is **[U]** (`docs/protocol/UDP_GAME_CAPTURE.md` 9.4).
/// </remarks>
public static class RulesMapListBodiesUtils
{
    /// <summary>Group byte a room's own map list is written under.</summary>
    public const byte MapListGroup = 0xff;

    /// <summary>
    /// The room's rotation maps in the captures' list shape: the group byte, the
    /// maps in rotation order, and the list's two ends.
    /// </summary>
    /// <param name="maps">The room's rotation maps.</param>
    /// <returns>The record body.</returns>
    public static byte[] Build(IReadOnlyList<int> maps)
    {
        var body = new List<byte>(maps.Count + 3) { MapListGroup };
        foreach (var map in maps)
        {
            body.Add((byte)map);
        }

        body.Add(MatchRecordBodiesUtils.RulesListEnd);
        body.Add(MatchRecordBodiesUtils.RulesListEnd);
        return [.. body];
    }
}
