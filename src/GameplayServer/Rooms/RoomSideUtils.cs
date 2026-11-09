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
    /// <summary>The first side a player row can state: the blue team.</summary>
    public const short FirstSide = BlueSide;

    /// <summary>
    /// The blue team's side id, which is also the lowest a row can state.
    /// </summary>
    public const short BlueSide = 0;

    /// <summary>The red team's side id.</summary>
    public const short RedSide = 1;

    /// <summary>
    /// The orange team's side id, and the last a row can state.
    /// </summary>
    /// <remarks>
    /// <b>A third side exists, and it was this file that said there were two.</b>
    /// The in-game player list draws its rows with three team colours — blue, red
    /// and orange — and the state block's own result coding numbers its sides as
    /// <c>2 + side</c> rather than as a pair (`MatchRecordBodiesUtils.ResultCode`).
    /// The room's rows carry the same ids, so what a row holds is a side of that
    /// range; the earlier reading took the lobby's own <c>0</c>/<c>1</c> as the whole
    /// of it and dropped every player a client reported otherwise
    /// (`LobbySlotService`).
    /// </remarks>
    public const short OrangeSide = 2;

    /// <summary>The last side a player row can state.</summary>
    public const short LastSide = OrangeSide;

    /// <summary>Number of sides a match can have.</summary>
    public const int SideCount = LastSide - FirstSide + 1;

    /// <summary>Whether a stored value names a side.</summary>
    /// <param name="side">Value a room row holds.</param>
    /// <returns><c>true</c> for a side the room can put a player on.</returns>
    public static bool IsSide(short? side) => side is >= FirstSide and <= LastSide;

    /// <summary>
    /// The side a two-side match opposes to the one given.
    /// </summary>
    /// <remarks>
    /// <b>It names one side because one caller needs exactly one.</b> A bot placed
    /// against a lone human needs an enemy, and the pair blue/red is the pair every
    /// two-side rule uses; the orange side has no single opposite, so it opposes
    /// blue, which is the first side of the match and the one the reference rooms
    /// put the host on. A room that fields all three sides does not come through
    /// here: its bots are placed by the count of players per side
    /// (<see cref="Mgo2Server.GameplayServer.Bots.BotTeamAssignmentService"/>).
    /// </remarks>
    /// <param name="side">A side the room states.</param>
    /// <returns>The opposing side, or <c>null</c> when the value names no side.</returns>
    public static short? Opposite(short? side) => side switch
    {
        BlueSide => RedSide,
        RedSide => BlueSide,
        OrangeSide => BlueSide,
        _ => null,
    };
}
