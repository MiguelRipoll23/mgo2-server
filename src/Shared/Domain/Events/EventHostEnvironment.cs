namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The game settings an event record or an assigned match advertises. It is the
/// same 204-byte block the ordinary room codec carries, modelled here so the
/// event records can embed it without reaching into the room screens.
/// <para>
/// Every field is a plain value; the byte layout lives in
/// <see cref="EventHostEnvironmentUtils"/>, because offsets in two places drift
/// apart and the schema is the wrong place to look for a wire layout.
/// </para>
/// </summary>
public sealed class EventHostEnvironment
{
    /// <summary>Rotation slots the block carries.</summary>
    public const int RotationSlots = 16;

    /// <summary>Twelve deathmatch-style rules, entry zero first.</summary>
    public int SneakingTime { get; set; }

    /// <summary>Rounds of the sneaking rule.</summary>
    public int SneakingRounds { get; set; }

    /// <summary>Minutes of the capture rule.</summary>
    public int CaptureTime { get; set; }

    /// <summary>Rounds of the capture rule.</summary>
    public int CaptureRounds { get; set; }

    /// <summary>Minutes of the rescue rule.</summary>
    public int RescueTime { get; set; }

    /// <summary>Rounds of the rescue rule.</summary>
    public int RescueRounds { get; set; }

    /// <summary>Minutes of team deathmatch.</summary>
    public int TeamDeathmatchTime { get; set; }

    /// <summary>Rounds of team deathmatch.</summary>
    public int TeamDeathmatchRounds { get; set; }

    /// <summary>Tickets of team deathmatch.</summary>
    public int TeamDeathmatchTickets { get; set; }

    /// <summary>Minutes of deathmatch.</summary>
    public int DeathmatchTime { get; set; }

    /// <summary>Tickets of deathmatch.</summary>
    public int DeathmatchTickets { get; set; }

    /// <summary>Minutes of base.</summary>
    public int BaseTime { get; set; }

    /// <summary>Rounds of base.</summary>
    public int BaseRounds { get; set; }

    /// <summary>Minutes of bomb.</summary>
    public int BombTime { get; set; }

    /// <summary>Rounds of bomb.</summary>
    public int BombRounds { get; set; }

    /// <summary>Minutes of team sneaking.</summary>
    public int TeamSneakingTime { get; set; }

    /// <summary>Rounds of team sneaking.</summary>
    public int TeamSneakingRounds { get; set; }

    /// <summary>Minutes of the stealth deathmatch rule.</summary>
    public int StealthDeathmatchTime { get; set; }

    /// <summary>Rounds of the stealth deathmatch rule.</summary>
    public int StealthDeathmatchRounds { get; set; }

    /// <summary>Minutes of the interval rule.</summary>
    public int IntervalTime { get; set; }

    /// <summary>Rounds of deathmatch, in the trailing single-byte fields.</summary>
    public int DeathmatchRounds { get; set; }

    /// <summary>Minutes of the solo capture rule.</summary>
    public int SoloCaptureTime { get; set; }

    /// <summary>Rounds of the solo capture rule.</summary>
    public int SoloCaptureRounds { get; set; }

    /// <summary>Minutes of the race rule.</summary>
    public int RaceTime { get; set; }

    /// <summary>Rounds of the race rule.</summary>
    public int RaceRounds { get; set; }

    /// <summary>Skin the red team uses.</summary>
    public int RedTeamSkin { get; set; }

    /// <summary>Skin the blue team uses.</summary>
    public int BlueTeamSkin { get; set; }

    /// <summary>Maximum players the room accepts.</summary>
    public int MaximumPlayers { get; set; }

    /// <summary>Players currently in the room.</summary>
    public int CurrentPlayers { get; set; }

    /// <summary>Briefing time in minutes.</summary>
    public int BriefingTime { get; set; }

    /// <summary>Stance the room allows.</summary>
    public int Stance { get; set; }

    /// <summary>Level half-width tolerance.</summary>
    public int LevelLimitTolerance { get; set; }

    /// <summary>Standard rate, or the level-limit base.</summary>
    public int StandardRateOrLevelLimitBase { get; set; }

    /// <summary>Red-team unique-character flag byte.</summary>
    public int UniqueRed { get; set; }

    /// <summary>Blue-team unique-character flag byte.</summary>
    public int UniqueBlue { get; set; }

    /// <summary>First of the two common-settings bytes.</summary>
    public int CommonA { get; set; }

    /// <summary>Second of the two common-settings bytes.</summary>
    public int CommonB { get; set; }

    /// <summary>Team-balance byte.</summary>
    public int TeamBalance { get; set; }

    /// <summary>Idle-kick threshold, or zero when disabled.</summary>
    public int IdleKick { get; set; }

    /// <summary>Team-kill-kick threshold, or zero when disabled.</summary>
    public int TeamKillKick { get; set; }

    /// <summary>Network status byte.</summary>
    public int NetworkStatus { get; set; } = 0x2e;

    /// <summary>Capture extra-time flag byte.</summary>
    public int CaptureExtraTime { get; set; }

    /// <summary>Snake-kill count of the sneaking rule.</summary>
    public int SneakingSnakeKills { get; set; }

    /// <summary>Trailing host-options byte.</summary>
    public int HostOptionsExtraTimeFlags { get; set; }

    /// <summary>Unread field at wire offset 0xC6.</summary>
    public int Field0C6 { get; set; }

    /// <summary>Sixteen rule/map/flags triples.</summary>
    public byte[][] Rotations { get; } = CreateRotations();

    /// <summary>Sixteen bytes of weapon restrictions.</summary>
    public byte[] WeaponRestrictions { get; } = new byte[16];

    /// <summary>Sets one rotation triple.</summary>
    /// <param name="index">Slot to set.</param>
    /// <param name="rule">Rule of the slot.</param>
    /// <param name="map">Map of the slot.</param>
    /// <param name="flags">Flags of the slot.</param>
    public void SetRotation(int index, int rule, int map, int flags)
    {
        if (index < 0 || index >= RotationSlots)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Rotation index is out of range.");
        }

        Rotations[index][0] = CheckedByte(nameof(rule), rule);
        Rotations[index][1] = CheckedByte(nameof(map), map);
        Rotations[index][2] = CheckedByte(nameof(flags), flags);
    }

    /// <summary>
    /// The shipping Survival preset: four team-deathmatch rotations with the
    /// rule timers and common byte the screen expects when no operator override
    /// is configured.
    /// </summary>
    /// <returns>A fresh environment with the default preset.</returns>
    public static EventHostEnvironment CreateDefault()
    {
        var environment = new EventHostEnvironment
        {
            MaximumPlayers = 17,
            BriefingTime = 1,
            TeamDeathmatchTime = 5,
            TeamDeathmatchRounds = 2,
            TeamDeathmatchTickets = 50,
            CommonA = 0x04,
            StandardRateOrLevelLimitBase = 0x16,
        };

        environment.SetRotation(0, 4, 1, 0);
        environment.SetRotation(1, 4, 2, 0);
        environment.SetRotation(2, 4, 3, 0);
        environment.SetRotation(3, 4, 4, 0);
        return environment;
    }

    private static byte[][] CreateRotations()
    {
        var rotations = new byte[RotationSlots][];
        for (var index = 0; index < rotations.Length; index++)
        {
            rotations[index] = new byte[3];
        }

        return rotations;
    }

    private static byte CheckedByte(string field, int value) =>
        value is >= 0 and <= 0xff
            ? (byte)value
            : throw new ArgumentOutOfRangeException(field, value, $"{field} is outside the unsigned byte range.");
}
