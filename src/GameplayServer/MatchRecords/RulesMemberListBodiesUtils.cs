namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The players a round holds, as the rules-roster identifiers carry them: the
/// group byte, one member per live player, and the list's two ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>The class-<c>0x00</c> body is the room's membership and not its maps.</b>
/// The grammar is the replay console's own <c>rulesRoster()</c>
/// (<c>docs/analysis/replay-console/rpdt.js</c>): a group byte, its members, the
/// group's <c>0xfe</c> and the list's <c>0xfe</c>. The recorded host writes it at
/// the round's spawn — <c>briefing-then-solo-game</c> t+148.490 is
/// <c>ff 0a fe fe</c> — and that one member is <c>0x0a</c>, the slot the same
/// frame's 25-byte spawn entry names and the slot the room's roster entry names.
/// A room of one whose body listed the stage's maps or its fifteen rotation
/// rounds would not be that body.
/// </para>
/// <para>
/// The member is therefore the player's slot in the room's 24-entry table, the
/// same key <c>RoomPlayerEntryBodiesUtils</c> writes, and the list is written for
/// the players the room holds when the frame goes out.
/// </para>
/// </remarks>
public static class RulesMemberListBodiesUtils
{
    /// <summary>Group byte a room's own member list is written under.</summary>
    public const byte MemberListGroup = 0xff;

    /// <summary>
    /// The room's live players in the captures' list shape: the group byte, one
    /// byte a player, and the list's two ends.
    /// </summary>
    /// <param name="members">Slots of the players the round holds.</param>
    /// <returns>The record body.</returns>
    public static byte[] Build(IReadOnlyList<byte> members)
    {
        var body = new List<byte>(members.Count + 3) { MemberListGroup };
        foreach (var member in members)
        {
            body.Add(member);
        }

        body.Add(MatchRecordBodiesUtils.RulesListEnd);
        body.Add(MatchRecordBodiesUtils.RulesListEnd);
        return [.. body];
    }
}
