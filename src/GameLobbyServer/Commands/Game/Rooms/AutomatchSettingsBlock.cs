namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>
/// Builds the settings block an automatch hands to the elected host, which the
/// host passes straight to the room-creation task.
/// </summary>
public static class AutomatchSettingsBlock
{
    /// <summary>Size of the block.</summary>
    public const int Size = 204;

    /// <summary>Offset of the rotation.</summary>
    private const int Rotation = 0;

    /// <summary>Number of rotation entries.</summary>
    private const int RotationEntries = 16;

    /// <summary>Bytes one rotation entry occupies.</summary>
    private const int EntryBytes = 3;

    /// <summary>Offset of the player cap.</summary>
    private const int MaximumPlayers = 66;

    /// <summary>Player cap of an automatch room.</summary>
    private const int MaximumPlayersValue = 16;

    /// <summary>Offset of the current player count.</summary>
    private const int CurrentPlayers = 67;

    /// <summary>Players present when the host creates the room.</summary>
    private const int PlayersAtCreate = 1;

    /// <summary>Offset of the briefing time.</summary>
    private const int BriefingTime = 68;

    /// <summary>Briefing time of an automatch room.</summary>
    private const int BriefingSeconds = 2;

    /// <summary>Offset of the level-limit tolerance.</summary>
    private const int LevelLimitTolerance = 95;

    /// <summary>Level cap of an automatch room.</summary>
    private const int LevelCap = 22;

    /// <summary>Offset of the per-rule timer array.</summary>
    private const int Timers = 100;

    /// <summary>Offset of the unique-blue byte.</summary>
    private const int UniqueBlue = 169;

    /// <summary>Offset of the first common byte.</summary>
    private const int CommonA = 177;

    /// <summary>Value of the first common byte.</summary>
    private const int CommonAValue = 0x24;

    /// <summary>Offset of the team-kill kick limit.</summary>
    private const int TeamKillKick = 182;

    /// <summary>Value of the team-kill kick limit.</summary>
    private const int TeamKillKickValue = 3;

    /// <summary>Offset of the snake-defeat count.</summary>
    private const int Snake = 189;

    /// <summary>Snake-defeat count of an automatch room.</summary>
    private const int AutomatchSnake = 3;

    /// <summary>Offset of the first unexplained word.</summary>
    private const int UnknownWord = 72;

    /// <summary>Value of the first unexplained word.</summary>
    private const int UnknownWordValue = 0x02000000;

    /// <summary>Offset of the unexplained byte.</summary>
    private const int UnknownByte = 179;

    /// <summary>Value of the unexplained byte.</summary>
    private const int UnknownByteValue = 0x20;

    /// <summary>Timer slot and count of each rule's entries.</summary>
    private static readonly Dictionary<int, int[]> RuleTimers = new()
    {
        [0] = [9, 2],
        [1] = [6, 3],
        [2] = [4, 2],
        [3] = [2, 2],
        [4] = [0, 2],
        [5] = [11, 2],
        [6] = [13, 2],
        [7] = [15, 2],
    };

    /// <summary>Timer values a real automatch room was observed to use.</summary>
    private static readonly Dictionary<int, int[]> ObservedTimers = new()
    {
        [0] = [10, 50],
        [1] = [5, 4, 25],
        [4] = [7, 4],
    };

    /// <summary>Client-default timer array.</summary>
    private static readonly int[] DefaultTimers = [8, 4, 4, 4, 4, 4, 3, 4, 15, 5, 30, 5, 4, 30, 4, 10, 4];

    /// <summary>Builds the block for a match.</summary>
    /// <param name="rules">Rotation rules, entry zero first.</param>
    /// <param name="map">Map the match runs.</param>
    public static byte[] Build(IReadOnlyList<int> rules, int map)
    {
        if (rules.Count == 0)
        {
            throw new ArgumentException("An automatch rotation needs at least one rule.", nameof(rules));
        }

        if (rules.Count > RotationEntries)
        {
            throw new ArgumentException($"At most {RotationEntries} rotation entries are allowed.", nameof(rules));
        }

        if (map == 0)
        {
            throw new ArgumentException("An automatch map must not be zero.", nameof(map));
        }

        var block = Template();
        block[CurrentPlayers] = PlayersAtCreate;

        for (var entry = 0; entry < rules.Count; entry++)
        {
            var at = Rotation + entry * EntryBytes;
            block[at] = (byte)rules[entry];
            block[at + 1] = (byte)map;
            block[at + 2] = 0;
            ApplyObservedTimers(block, rules[entry]);
        }

        return block;
    }

    private static byte[] Template()
    {
        var block = new byte[Size];
        block[MaximumPlayers] = MaximumPlayersValue;
        WriteUInt32(block, BriefingTime, BriefingSeconds);
        block[LevelLimitTolerance] = LevelCap;

        for (var index = 0; index < DefaultTimers.Length; index++)
        {
            WriteUInt32(block, Timers + index * 4, DefaultTimers[index]);
        }

        block[UniqueBlue] = 1;
        block[CommonA] = CommonAValue;
        WriteUInt16(block, TeamKillKick, TeamKillKickValue);
        block[Snake] = AutomatchSnake;
        WriteUInt32(block, UnknownWord, UnknownWordValue);
        block[UnknownByte] = UnknownByteValue;
        return block;
    }

    private static void ApplyObservedTimers(byte[] block, int rule)
    {
        if (rule == 4)
        {
            block[Snake] = AutomatchSnake;
        }

        if (!ObservedTimers.TryGetValue(rule, out var observed) || !RuleTimers.TryGetValue(rule, out var slots))
        {
            return;
        }

        for (var index = 0; index < observed.Length && index < slots[1]; index++)
        {
            WriteUInt32(block, Timers + slots[0] * 4 + index * 4, observed[index]);
        }
    }

    private static void WriteUInt32(byte[] block, int at, int value) =>
        Mgo2Server.Shared.Utils.BinaryUtility.WriteUInt32BigEndian(block, at, (uint)value);

    private static void WriteUInt16(byte[] block, int at, int value) =>
        Mgo2Server.Shared.Utils.BinaryUtility.WriteUInt16BigEndian(block, at, (ushort)value);
}
