namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// One bit of body field 7, the four-byte word the client reads its room switches from.
/// </summary>
/// <remarks>
/// <para>
/// Only two bits carry a reading. Bit <c>0x02</c> is Drebin Points, set in every
/// captured room (<c>docs/GATES.md</c> §2); bit <c>0x40</c> is set in the one room whose
/// joins asked the client which team to join, so it reads as that room leaving the team
/// to the player instead of assigning one
/// (<c>docs/analysis/rotation-payload-corpus.md</c> §8).
/// </para>
/// <para>
/// The client also tests <c>0x20</c> at five call sites and <c>0x04</c> at one, and the
/// corpus sets further bits — <c>0x2000</c>, <c>0x10000</c>, <c>0x40000</c>,
/// <c>0x100000</c>, <c>0x200000</c> — none of which is named yet, so they stay as part
/// of the pinned word rather than as members here.
/// </para>
/// </remarks>
[Flags]
internal enum RoomCommonSettingsFlag : uint
{
    /// <summary>Bit <c>0x02</c>: the room plays Drebin Points rounds.</summary>
    DrebinPoints = 0x0000_0002,

    /// <summary>Bit <c>0x40</c>: a joining player is asked which team to join.</summary>
    AskWhichTeam = 0x0000_0040,
}
