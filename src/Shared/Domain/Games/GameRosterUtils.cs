using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// What a room reports as the players in it, including its host.
/// <para>
/// A dedicated host's roster row is its host seat. It still counts as the room's
/// first player so an empty dedicated room is listed as "1/8", not "0/8".
/// </para>
/// <para>
/// The rule is about the room, not the row: it applies to a room named for a
/// dedicated host, which is the name a gameplay server opens its room under.
/// </para>
/// <para>
/// This is the count only. A room's player-list reply keeps the host's entry
/// whatever the room is, because the joining client reads the host character id
/// out of that list and gates the host's peer-to-peer handshake on it.
/// </para>
/// </summary>
public static class GameRosterUtils
{
    /// <summary>Roster slots a dedicated host's own room leaves to its participants.</summary>
    public const int DedicatedHostRosterSlots = 1;

    /// <summary>Whether a room is a dedicated host's own room.</summary>
    /// <param name="room">Room being asked about.</param>
    public static bool IsDedicatedHostRoom(Game room)
    {
        ArgumentNullException.ThrowIfNull(room);

        return room.HostIdentifier > 0 && DedicatedHostNameUtils.IsDedicatedHostName(room.Name);
    }

    /// <summary>
    /// How many players a room reports, including a dedicated host's own seat.
    /// </summary>
    /// <param name="room">Room being reported on.</param>
    /// <param name="rosterCount">Rows in the room's roster.</param>
    /// <returns>The number of players in the room.</returns>
    public static int ReportedParticipants(Game room, int rosterCount) =>
        IsDedicatedHostRoom(room) ? Math.Max(DedicatedHostRosterSlots, rosterCount) : rosterCount;
}
