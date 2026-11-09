namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Hands out the two keys a joining player takes: its roster index, which orders the
/// roster run, and its slot in the room's 24-entry player table, which is the key a
/// record is addressed by.
/// </summary>
public static class RosterSlotUtils
{
    /// <summary>
    /// Lowest roster index no member holds, counted from <paramref name="first"/>.
    /// </summary>
    /// <param name="members">The room's current members.</param>
    /// <param name="first">First index a player may take; the host is not one.</param>
    /// <returns>The index to hand out.</returns>
    /// <exception cref="InvalidOperationException">Every index is taken.</exception>
    public static sbyte NextFreeRosterIndex(IReadOnlyList<RosterMember> members, sbyte first)
    {
        var taken = members.Select(member => member.RosterIndex).ToHashSet();

        for (var index = first; index <= sbyte.MaxValue; index++)
        {
            if (!taken.Contains((sbyte)index))
            {
                return (sbyte)index;
            }
        }

        throw new InvalidOperationException("The room roster is full; no slot is left for another player.");
    }

    /// <summary>
    /// Lowest free slot of the room's player table, the host's zero excepted. Distinct
    /// per live player and released when they leave.
    /// </summary>
    /// <param name="members">The room's current members.</param>
    /// <param name="first">First slot a player may take.</param>
    /// <param name="count">Slots in the table, so the last usable one is one below it.</param>
    /// <returns>The slot to hand out.</returns>
    /// <exception cref="InvalidOperationException">Every slot is taken.</exception>
    public static byte NextFreeSessionSlot(IReadOnlyList<RosterMember> members, byte first, int count)
    {
        var taken = members.Select(member => member.SessionSlot).ToHashSet();

        for (var value = first; value < count; value++)
        {
            if (!taken.Contains((byte)value))
            {
                return (byte)value;
            }
        }

        throw new InvalidOperationException("The room's player table is full; no key is left for another player.");
    }
}
