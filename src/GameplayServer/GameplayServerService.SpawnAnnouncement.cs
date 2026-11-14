using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The room's spawn announcement: a member's entry is written to the other peers
/// when that member's first position shows it has entered the map.
/// </summary>
/// <remarks>
/// Split out of <c>GameplayServerService.FrameDispatch.cs</c> to keep that file
/// inside the repository's size limit. The position relay itself stays in the
/// dispatch loop; this file only acts on what the relay just carried.
/// </remarks>
public sealed partial class GameplayServerService
{
    /// <summary>
    /// Announces a member's spawn to the rest of the room on its first position.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The first position is the trigger.</b> The 25-byte entry carries the
    /// map's spawn and gives the player a body, so it is written once the player
    /// reports where it is, not when the profile seats it: the recorded late joins
    /// in <c>pcapng2</c> write the entry five to twenty seconds after the profile,
    /// beside the first position records (t+265.622, t+312.506).
    /// </para>
    /// <para>
    /// A room whose round is not open writes nothing: an entry handed to a client
    /// during its briefing leaves the briefing for a map that is not playing
    /// (<see cref="Rooms.TeamArrayService.EnterRoomAsync"/>).
    /// </para>
    /// </remarks>
    /// <param name="session">Session whose position record was just relayed.</param>
    /// <param name="firstPosition">Whether this was that session's first position.</param>
    private async Task AnnounceSpawnIfFirstAsync(PeerSession session, bool firstPosition)
    {
        if (!firstPosition || !roundStates.IsOpen)
        {
            return;
        }

        if (roster.Find(session.RemoteAddress) is not { } spawned)
        {
            return;
        }

        var spawn = await roomMembers.BuildSpawnAsync(spawned);
        foreach (var record in spawn)
        {
            SendToOthers(session, record.Type, record.Body, record.Ordinal);
        }
    }
}
