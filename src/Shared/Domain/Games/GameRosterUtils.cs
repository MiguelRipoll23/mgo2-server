using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// What a room reports as the players in it.
/// <para>
/// A gameplay server hosts rather than plays: it opens the room, keeps it alive
/// and hands its endpoint to whoever joins, and its own row in the roster is the
/// host's seat rather than a participant. Counted as one, a dedicated room looks
/// occupied by somebody who is not in it — the room list reads "1/8" for a room
/// nobody has entered.
/// </para>
/// <para>
/// The rule is about the room, not the row: it applies to a room named for a
/// dedicated host, which is the name a gameplay server opens its room under and
/// which no player may create. An ordinary player's room counts its host, as it
/// always has.
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
    /// How many players a room reports, which is its roster without a dedicated
    /// host's own seat.
    /// </summary>
    /// <param name="room">Room being reported on.</param>
    /// <param name="rosterCount">Rows in the room's roster.</param>
    /// <returns>The number of players in the room.</returns>
    public static int ReportedParticipants(Game room, int rosterCount) =>
        IsDedicatedHostRoom(room) ? Math.Max(0, rosterCount - DedicatedHostRosterSlots) : rosterCount;
}
