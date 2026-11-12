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
    /// Group byte a side's members are written under: the working host's
    /// <c>0x18</c> for one side and <c>0x19</c> for the other.
    /// </summary>
    /// <remarks>
    /// <c>.pcapngs/actions/actions.pcapng</c> writes its room as
    /// <c>19 01 03 05 08 09 0b 0e 10 fe 18 02 04 06 07 0a 0c 0d 0f fe fe</c> — two
    /// groups of eight, the first under <c>0x19</c> and the second under
    /// <c>0x18</c>, each closed with <c>0xfe</c> and the list with one more. A room
    /// with no sides writes the flat <c>ff 0a fe fe</c> <see cref="briefing-then-solo-game"/>
    /// holds, which is what <see cref="MemberListGroup"/> is for.
    /// </remarks>
    public const byte SideGroupBase = 0x18;

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

    /// <summary>
    /// The room's players in the list shape the working host writes when it holds
    /// sides: one group per side, under that side's own group byte, and any slot
    /// whose side is unknown behind them in the flat group.
    /// </summary>
    /// <remarks>
    /// <b>Every member of a sided room goes in a side's group, not in one flat
    /// list.</b> A body that put both sides in <c>ff 01 02 … fe fe</c> names the
    /// players and says nothing about which of them are on which side, which is the
    /// one thing the client reads this list for.
    /// </remarks>
    /// <param name="slots">The room's players, each with the side it holds.</param>
    /// <returns>The record body.</returns>
    public static byte[] Build(IReadOnlyList<Rooms.LobbyTeamSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        // Slot zero is the host's own seat and never a player of the round. The
        // client builds a player entry for every slot this list names, and an entry
        // whose handle it never registered resolves to nothing
        // — `system.dll`: `FUN_10064660(slot)` reads the 24-entry player table and
        // `FUN_1010a2e0` returns null when the handle in it is not the registered
        // one — so a record that later names slot zero is read through a null
        // player (the fault of 2026-11-12, `system.dll+0x224D7B`, `read=0x974`,
        // which is that null plus the field offset). The working host's list names
        // slots 1 to 16 and no zero.
        var seated = slots.Where(slot => slot.Slot != 0).ToList();

        var body = new List<byte>(seated.Count + 6);
        foreach (var group in seated
                     .Where(slot => slot.Team is not null)
                     .GroupBy(slot => slot.Team!.Value)
                     .OrderBy(group => group.Key))
        {
            body.Add((byte)(SideGroupBase + group.Key));
            body.AddRange(group.Select(slot => slot.Slot).Order());
            body.Add(MatchRecordBodiesUtils.RulesListEnd);
        }

        var unsided = seated
            .Where(slot => slot.Team is null)
            .Select(slot => slot.Slot)
            .Order()
            .ToList();
        if (unsided.Count > 0)
        {
            body.Add(MemberListGroup);
            body.AddRange(unsided);
            body.Add(MatchRecordBodiesUtils.RulesListEnd);
        }

        body.Add(MatchRecordBodiesUtils.RulesListEnd);
        return [.. body];
    }
}
