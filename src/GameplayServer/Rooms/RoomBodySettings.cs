namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The room facts the state body states about itself, as this instance configures
/// them.
/// </summary>
/// <remarks>
/// <para>
/// It exists so the body's writers and the services that call them agree on one
/// shape. Every value is a room setting with a default that reproduces the
/// working capture, and <see cref="Defaults"/> is that set — a body built without
/// a room's own settings is byte for byte the capture's.
/// </para>
/// <para>
/// <b>Widths are the body's, not the screen's.</b> A value the body states in one
/// byte stays an <c>int</c> here and the writer narrows it after validation
/// (<see cref="MatchSettingsUtils.Validate"/>), so a configured number is never
/// silently wrapped. <see cref="MaximumCharacters"/>, for one, is a <c>u8</c> at
/// body <c>0x4a</c> and a <c>u16</c> in the client's settings block.
/// </para>
/// </remarks>
/// <param name="MaximumCharacters">Character cap, body <c>0x4a</c>.</param>
/// <param name="BriefingMinutes">Briefing time in minutes, body <c>0x61</c>–<c>0x62</c>.</param>
/// <param name="TeamKillKick">Team kills before removal, body <c>0x4e</c>.</param>
/// <param name="SnakeDefeats">Times Snake must be defeated, body <c>0x96</c>.</param>
/// <param name="DeathmatchRounds">Deathmatch rounds, body <c>0x8f</c>.</param>
/// <param name="StealthDeathmatchRounds">SDM rounds, body <c>0x90</c>.</param>
/// <param name="SneakingCaptureRounds">Sneaking Capture rounds, body <c>0x91</c>.</param>
/// <param name="RaceRounds">Race rounds, body <c>0x92</c>.</param>
/// <param name="MinuteLimitMinutes">The whole-minute limit at body <c>0x53</c>–<c>0x54</c>.</param>
/// <param name="HostStance">The host's stance, body <c>0x60</c>.</param>
/// <param name="CaptureExtraTime">Capture's extra time, body <c>0x97</c>.</param>
/// <param name="HostFlag0">Unnamed host flag 0, body <c>0x98</c>.</param>
/// <param name="HostFlag2">Unnamed host flag 2, body <c>0x99</c>.</param>
/// <param name="DeathmatchTickets">DM ticket count when no bank states one, body <c>0x94</c>.</param>
/// <param name="RoomTailWord">The room's tail word, body <c>0x9e</c>–<c>0x9f</c>.</param>
public sealed record RoomBodySettings(
    int MaximumCharacters,
    int BriefingMinutes,
    int TeamKillKick,
    int SnakeDefeats,
    int DeathmatchRounds,
    int StealthDeathmatchRounds,
    int SneakingCaptureRounds,
    int RaceRounds,
    int MinuteLimitMinutes,
    RoomHostStance HostStance,
    bool CaptureExtraTime,
    bool HostFlag0,
    bool HostFlag2,
    int DeathmatchTickets,
    ushort RoomTailWord)
{
    /// <summary>
    /// The defaults: every field the working capture states, before a room
    /// configures any of them.
    /// </summary>
    public static RoomBodySettings Defaults { get; } = new(
        RoomStateBodyConstants.DefaultMaximumCharacters,
        RoomStateBodyConstants.DefaultBriefingMinutes,
        RoomStateBodyConstants.DefaultTeamKillKick,
        RoomStateBodyConstants.DefaultSnakeDefeats,
        RoomStateBodyConstants.DefaultDeathmatchRounds,
        RoomStateBodyConstants.DefaultStealthDeathmatchRounds,
        RoomStateBodyConstants.DefaultSneakingCaptureRounds,
        RoomStateBodyConstants.DefaultRaceRounds,
        RoomStateBodyConstants.DefaultMinuteLimitMinutes,
        RoomStateBodyConstants.DefaultHostStance,
        RoomStateBodyConstants.DefaultCaptureExtraTime,
        RoomStateBodyConstants.DefaultHostFlag0,
        RoomStateBodyConstants.DefaultHostFlag2,
        RoomStateBodyConstants.DefaultDeathmatchTickets,
        RoomStateBodyConstants.DefaultRoomTailWord);
}
