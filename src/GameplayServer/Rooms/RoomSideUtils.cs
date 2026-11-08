namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The side a player holds in a team match, as the room's rows state it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two sides, and the range is read from the one place that already enforces
/// it.</b> <see cref="LobbySlotService"/> treats a stored side other than
/// <c>0</c> or <c>1</c> as a player the room puts on no side, and the replay
/// console's loadout byte codes the same two values as <c>2 × side</c> over 323
/// players. The pair is therefore the match's own two sides rather than a choice
/// this server makes, which is why nothing else in the bot system names a side by
/// number: a bot's side is resolved against the sides the players are actually
/// on (<c>Mgo2Server.GameplayServer.Bots.BotTeamAssignmentService</c>).
/// </para>
/// </remarks>
public static class RoomSideUtils
{
    /// <summary>The first side a player row can state.</summary>
    public const short FirstSide = 0;

    /// <summary>The last side a player row can state.</summary>
    public const short LastSide = 1;

    /// <summary>Number of sides a team match has.</summary>
    public const int SideCount = LastSide - FirstSide + 1;

    /// <summary>Whether a stored value names a side.</summary>
    /// <param name="side">Value a room row holds.</param>
    /// <returns><c>true</c> for a side the room can put a player on.</returns>
    public static bool IsSide(short? side) => side is >= FirstSide and <= LastSide;

    /// <summary>The side opposite a side a player holds.</summary>
    /// <param name="side">A side the room states.</param>
    /// <returns>The other side, or <c>null</c> when the value names no side.</returns>
    public static short? Opposite(short? side) => side switch
    {
        FirstSide => LastSide,
        LastSide => FirstSide,
        _ => null,
    };
}
