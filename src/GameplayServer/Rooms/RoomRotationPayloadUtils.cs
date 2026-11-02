using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Rooms;

internal static class RoomRotationPayloadUtils
{
    public const int PayloadLength = 161;
    private const int RotationEntryCount = 16;
    private const byte ReferenceRoundNumber = 2;
    private const int ReferenceRotationIndex = 4;
    private const byte ReferencePlayerCount = 2;
    private const int ReferenceTimerSeconds = 300;
    private const byte NoReferencedSlot = byte.MaxValue;
    private const byte ReferenceStateField51 = 0x8c;
    private const ushort ReferenceStateField52 = 0x0128;
    private const byte ReferenceField53Selector = 0;

    private static readonly int[] RuleTimerBankIndexes =
    [
        MatchRotationUtils.TimeIndex(MatchRuleUtils.Deathmatch),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.TeamDeathmatch),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.Rescue),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.Capture),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.Sneaking),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.Base),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.Bomb),
        MatchRotationUtils.TimeIndex(MatchRuleUtils.TeamSneaking),
    ];

    private static readonly MatchRotationEntry[] ReferenceRotation =
    [
        new(Rule: 5, Map: 12, Flags: 0, TimeLimitMinutes: 5),
        new(Rule: 6, Map: 4, Flags: 0, TimeLimitMinutes: 5),
        new(Rule: 3, Map: 5, Flags: 0, TimeLimitMinutes: 5),
        new(Rule: 16, Map: 2, Flags: 0, TimeLimitMinutes: 5),
        new(Rule: 3, Map: 7, Flags: 0, TimeLimitMinutes: 5),
        new(Rule: 1, Map: 3, Flags: 0, TimeLimitMinutes: 5),
        new(Rule: 2, Map: 14, Flags: 0, TimeLimitMinutes: 5),
        new(Rule: 1, Map: 1, Flags: 0, TimeLimitMinutes: 5),
        new(Rule: 5, Map: 6, Flags: 0, TimeLimitMinutes: 5),
    ];

    public static byte[] BuildReferenceBody() =>
        BuildCore(
            ReferenceRotation,
            ReferenceRoundNumber,
            ReferenceRotationIndex,
            ReferencePlayerCount,
            ruleTimers: null);

    public static byte[] BuildReferenceBody(IReadOnlyList<MatchRotationEntry> rotation) =>
        BuildCore(
            rotation,
            ReferenceRoundNumber,
            currentRotationIndex: 0,
            playerCount: ReferencePlayerCount,
            ruleTimers: null);

    public static byte[] Build(
        IReadOnlyList<MatchRotationEntry> rotation,
        byte roundNumber,
        int currentRotationIndex,
        byte playerCount,
        IReadOnlyList<int> ruleTimers)
    {
        ArgumentNullException.ThrowIfNull(ruleTimers);
        return BuildCore(rotation, roundNumber, currentRotationIndex, playerCount, ruleTimers);
    }

    private static byte[] BuildCore(
        IReadOnlyList<MatchRotationEntry> rotation,
        byte roundNumber,
        int currentRotationIndex,
        byte playerCount,
        IReadOnlyList<int>? ruleTimers)
    {
        ArgumentNullException.ThrowIfNull(rotation);

        if (rotation.Count == 0)
        {
            throw new ArgumentException("A room's rotation must contain at least one entry.", nameof(rotation));
        }

        if (currentRotationIndex is < 0 or >= RotationEntryCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentRotationIndex),
                currentRotationIndex,
                $"The current rotation index must fit the {RotationEntryCount}-slot field.");
        }

        if (ruleTimers is not null && ruleTimers.Count != MatchRotationUtils.RuleTimerCount)
        {
            throw new ArgumentException(
                $"The room's timer bank must contain {MatchRotationUtils.RuleTimerCount} values.",
                nameof(ruleTimers));
        }

        List<byte> body = new(PayloadLength)
        {
            PlayerSettingsBurstService.SettingsPayloadType,
            PlayerSettingsBurstService.RoomRotationSlot,
            0xff,
            0x27,
            0xfe,
            0xee,
            0xff,
            0xff,
            0x3f,
        };

        // Field 0 is also sent as the room's standalone round-phase value.
        body.Add(roundNumber);
        WriteZeroBytes(body, 16);

        for (var slot = 0; slot < RotationEntryCount; slot++)
        {
            if (slot >= rotation.Count)
            {
                WriteZeroBytes(body, PlayerSettingsBurstService.RotationEntryLength);
                continue;
            }

            var entry = rotation[slot];
            body.Add(Component(nameof(entry.Map), entry.Map));
            body.Add(Component(nameof(entry.Rule), entry.Rule));
            body.Add(Component(nameof(entry.Flags), entry.Flags));
        }

        // State fields 3-8 are not semantically resolved; use the working capture.
        body.Add(0x11);
        WriteUInt16LittleEndian(body, 0x00b4);
        body.Add(0x02);
        body.Add(0x03);
        WriteUInt32LittleEndian(body, 0x00274022);
        WriteUInt16LittleEndian(body, 0);

        // Native readers use field 9 to select the current rotation triple.
        body.Add((byte)currentRotationIndex);
        // Field 10 is the room population, including the host.
        body.Add(playerCount);

        // State fields 13 and 17-27 retain their working-capture values.
        body.Add(0x00);
        body.Add(0x00);
        body.Add(0x00);
        WriteUInt16LittleEndian(body, 5);
        body.Add(0x00);
        body.Add(0x00);
        WriteUInt16LittleEndian(body, 5);
        body.Add(0x00);
        WriteUInt16LittleEndian(body, 1);
        body.Add(0x00);
        body.Add(0x00);

        WriteRuleTimerField(body, ruleTimers);

        // Most of these fields remain unnamed; keep their working-capture values.
        body.Add(0x02);
        body.Add(0x02);
        body.Add(0x02);
        body.Add(0x02);
        body.Add(0x02);
        body.Add(0x02);
        body.Add(0x02);
        body.Add(0x02);
        body.Add(0x01);
        body.Add(0x02);
        body.Add(0x02);
        body.Add(0x02);
        body.Add(0x32);
        body.Add(0x1e);
        body.Add(0x05);
        body.Add(0x03);
        body.Add(0x00);
        body.Add(0x00);
        body.Add(0x00);
        WriteUInt16LittleEndian(body, 0x7f7f);
        WriteNativeStateTail(body);

        if (body.Count != PayloadLength)
        {
            throw new InvalidOperationException(
                $"The state-field writer produced {body.Count} bytes instead of {PayloadLength}.");
        }

        return body.ToArray();
    }

    private static void WriteNativeStateTail(List<byte> body)
    {
        // Native code uses 0xff as the unassigned value for this slot reference.
        body.Add(NoReferencedSlot);

        // The sources of fields 51-52 are known, but their meanings are not.
        body.Add(ReferenceStateField51);
        WriteUInt16LittleEndian(body, ReferenceStateField52);

        // Native readers use bit 0 to select between two entries in a global table.
        body.Add(ReferenceField53Selector);
    }

    private static void WriteRuleTimerField(List<byte> body, IReadOnlyList<int>? ruleTimers)
    {
        foreach (var timerIndex in RuleTimerBankIndexes)
        {
            var minutes = ruleTimers is null ? ReferenceTimerSeconds / 60 : ruleTimers[timerIndex];
            if (minutes < 0 || minutes > ushort.MaxValue / 60)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ruleTimers),
                    minutes,
                    "A state-field rule timer must fit in an unsigned 16-bit count of seconds.");
            }

            WriteUInt16LittleEndian(body, minutes * 60);
        }

        // These entries are not mapped to the configured timer bank yet.
        for (var index = 0; index < 4; index++)
        {
            WriteUInt16LittleEndian(body, 0);
        }

        for (var index = 0; index < 5; index++)
        {
            WriteUInt16LittleEndian(body, ReferenceTimerSeconds);
        }
    }

    private static void WriteZeroBytes(List<byte> destination, int count)
    {
        for (var index = 0; index < count; index++)
        {
            destination.Add(0);
        }
    }

    private static void WriteUInt16LittleEndian(List<byte> destination, int value)
    {
        if (value is < ushort.MinValue or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The state field is an unsigned 16-bit value.");
        }

        destination.Add((byte)value);
        destination.Add((byte)(value >> 8));
    }

    private static void WriteUInt32LittleEndian(List<byte> destination, uint value)
    {
        destination.Add((byte)value);
        destination.Add((byte)(value >> 8));
        destination.Add((byte)(value >> 16));
        destination.Add((byte)(value >> 24));
    }

    private static byte Component(string field, int value) =>
        value is >= byte.MinValue and <= byte.MaxValue
            ? (byte)value
            : throw new ArgumentOutOfRangeException(field, value, "A rotation component is one byte.");
}
