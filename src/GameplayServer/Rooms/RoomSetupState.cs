namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The room's setup state at body <c>0x4d</c> — what the room is doing when the body is
/// written.
/// </summary>
/// <remarks>
/// <para>
/// The state transfer's field at object <c>0x48</c>, one byte wide, so it is a state and
/// not a setting: the same host's bodies change it with the room. Every captured body of
/// a room in its game phase carries <see cref="InGame"/>; the one body written while that
/// room was held at its briefing carries <see cref="Briefing"/>.
/// </para>
/// <para>
/// The value is the room's and not the host's — the briefing body and the game bodies
/// come from the same host — which is why it is written from the room's state rather than
/// pinned as a constant.
/// </para>
/// </remarks>
internal enum RoomSetupState : byte
{
    /// <summary>
    /// The room is playing — <c>0x02</c> in twenty-one of the twenty-two captured bodies,
    /// and the value every body this server sends carries.
    /// </summary>
    InGame = 0x02,

    /// <summary>
    /// The room is held at its briefing — <c>0x14</c> (20) in the one body written while
    /// that room was at its briefing.
    /// </summary>
    Briefing = 0x14,
}
