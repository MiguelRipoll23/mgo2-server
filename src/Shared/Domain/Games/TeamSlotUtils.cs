namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// Converts the team slot a client reports into the one the roster stores.
/// <para>
/// Both <c>0x4344</c> and <c>0x4440</c> carry the same character-record field and
/// neither is a plain copy of it: <c>0x4344</c> sends the raw slot, so <c>0</c>,
/// <c>1</c>, <c>2</c> and the <c>254</c> "no team" sentinel are all reachable,
/// while <c>0x4440</c> sends <c>v == 1 ? 2 : 1</c> — the 1-based team, which
/// collapses everything else onto the first team. The same admin action can fire
/// both packets, so they are read the same way in exactly one place: here.
/// </para>
/// </summary>
public static class TeamSlotUtils
{
    /// <summary>Slot stored for a player who is on no team. The client's own sentinel, 0xFE.</summary>
    public const short NoTeam = 254;

    /// <summary>Highest slot the two-team roster uses; 2 is the third role, beyond both teams.</summary>
    public const short HighestTeam = 2;

    /// <summary>Reads the raw slot <c>0x4344</c> carries. An out-of-range byte is read as no team.</summary>
    /// <param name="slot">Raw byte from the register.</param>
    public static short FromRawSlot(int slot) =>
        slot is >= 0 and <= HighestTeam ? (short)slot : NoTeam;

    /// <summary>
    /// Reads the 1-based team <c>0x4440</c> carries. The client can only build 1 or 2,
    /// and it builds 2 for the second team, so anything else is the first team.
    /// </summary>
    /// <param name="team">Byte from the team change.</param>
    public static short FromOneBasedTeam(int team) => (short)(team == 2 ? 1 : 0);
}
