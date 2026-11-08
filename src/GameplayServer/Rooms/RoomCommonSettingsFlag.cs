namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The bits of body field 7 that carry a reading, the four-byte word the client both
/// writes when it hosts and reads its room switches from.
/// </summary>
/// <remarks>
/// <para>
/// Two bits were read off the wire and two off the client's behaviour: bit <c>0x02</c> is
/// Drebin Points, set in every captured room (<c>docs/GATES.md</c> §2), and bit <c>0x40</c>
/// is set in the one room whose joins asked the client which team to join
/// (<c>docs/analysis/rotation-payload-corpus.md</c> §8).
/// </para>
/// <para>
/// <b>The Create Game switches are the block at <c>0x200</c>.</b> The gameplay client's
/// own state builder expands the room record's <c>+0x3a0</c> bitfield into this word,
/// and the Create Game summary renderer (`0x8A8A2C`–`0x8A93C4`) binds the same bit
/// positions to named rows, so these seven are read rather than guessed
/// (<c>docs/proto/inbound/mgo2_cmd_4310_c2s.ksy</c>). Each is an *enable*: the value
/// it gates — the level band, the kick counter — travels in the settings block, not
/// here.
/// </para>
/// <para>
/// Bits 0-7 vary per room and no instruction in the image tests them, and bits 17, 18,
/// 22 and 23 are positions with no rendered row, so neither group is named here.
/// </para>
/// </remarks>
[Flags]
internal enum RoomCommonSettingsFlag : uint
{
    /// <summary>Bit <c>0x02</c>: the room plays Drebin Points rounds.</summary>
    DrebinPoints = 0x0000_0002,

    /// <summary>Bit <c>0x40</c>: a joining player is asked which team to join.</summary>
    AskWhichTeam = 0x0000_0040,

    /// <summary>Bit <c>0x200</c>: <c>Auto Assign Teams</c> — the room puts joiners on a side.</summary>
    AutoAssignTeams = 0x0000_0200,

    /// <summary>Bit <c>0x1000</c>: the <c>Level Limit</c> band is in force.</summary>
    LevelLimitEnabled = 0x0000_1000,

    /// <summary>Bit <c>0x2000</c>: <c>Allow Quick-Join</c> — the room accepts automatched joiners.</summary>
    AllowQuickJoin = 0x0000_2000,

    /// <summary>Bit <c>0x4000</c>: <c>Voice Chat</c> is allowed.</summary>
    VoiceChat = 0x0000_4000,

    /// <summary>Bit <c>0x8000</c>: <c>Team Kill Kick</c> is in force.</summary>
    TeamKillKick = 0x0000_8000,

    /// <summary>Bit <c>0x10000</c>: <c>Idle Kick</c> is in force.</summary>
    IdleKick = 0x0001_0000,
}
