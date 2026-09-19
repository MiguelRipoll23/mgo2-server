using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>The stored host-settings slot the client reads and writes.</summary>
public static class HostSettingsType
{
    /// <summary>The single current settings row of a character.</summary>
    public const short Value = 0;
}

/// <summary>
/// Codec of the host-settings block. The block is the 0x4310 payload the client pushes
/// (345 bytes, wire 0x00..0x158); every byte has a home in the typed
/// <see cref="CharacterHostSettings"/> columns or in the unread catch-alls, so the block
/// survives the round trip through the rows byte for byte. The saved-settings reply
/// (0x4305) is the same structure re-based, which is where <see cref="BuildReply"/> comes in.
/// </summary>
public static class HostSettingsCodec
{
    /// <summary>Size of the reply when the client never pushed settings.</summary>
    public const int EmptyReplySize = 128;

    /// <summary>Size of the reply holding a re-mapped settings block.</summary>
    public const int ReplySize = 0x15c;

    /// <summary>Shortest block the reply mapping reads from.</summary>
    public const int MinimumBlobLength = 0x156;

    /// <summary>Length of the 0x4310 block: the unread tail ends at 0x158.</summary>
    public const int BlockSize = 0x159;

    /// <summary>Number of rotation entries in the block.</summary>
    private const int RotationRounds = 16;

    /// <summary>Offset of the rotation inside the block, rule first.</summary>
    private const int RotationOffset = 0xa3;

    /// <summary>Space between rotation triples.</summary>
    private const int RotationStep = 3;

    /// <summary>Offset of the password-enabled flag inside the block.</summary>
    private const int PasswordFlagOffset = 0x90;

    /// <summary>Offset of the password text inside the block.</summary>
    private const int PasswordOffset = 0x91;

    /// <summary>Length of the password field.</summary>
    private const int PasswordLength = 16;

    /// <summary>Offset of the host-options byte inside the block's unread tail.</summary>
    private const int HostOptionsOffset = 0x155;

    /// <summary>Bit of the host-options byte that marks a non-stat room.</summary>
    private const int NonStatBit = 0b10;

    /// <summary>Offset of the seventeen per-rule timer words.</summary>
    private const int TimersOffset = 0xfc;

    /// <summary>Number of per-rule timer words.</summary>
    private const int TimersCount = 17;

    /// <summary>Offset of the 16-byte weapon restrictions.</summary>
    private const int WeaponRestrictionsOffset = 0xd5;

    /// <summary>Offset of the 14-byte unread tail inside the block.</summary>
    private const int TailOffset = 0x14b;

    /// <summary>Length of the unread tail.</summary>
    private const int TailLength = 14;

    /// <summary>Reads every field of the block into a fresh settings row.</summary>
    /// <param name="payload">Block the client pushed.</param>
    public static CharacterHostSettings FromPayload(byte[] payload)
    {
        // Reads happen against a zero-padded block, so a short push still produces a row
        // whose rebuild does not read past the captured bytes.
        var block = new byte[BlockSize];
        Array.Copy(payload, block, Math.Min(payload.Length, BlockSize));

        var settings = new CharacterHostSettings
        {
            Name = StringUtility.ReadFixedString(block, 0x00, 16),
            Comment = StringUtility.ReadFixedString(block, 0x10, 128),
            Password = block[PasswordFlagOffset] != 0
                ? StringUtility.ReadFixedString(block, PasswordOffset, PasswordLength)
                : null,
            Dedicated = block[0xa1] != 0,
            SettingsLobbySubtype = (short)block[0xa2],
            RotationRules = ReadRotation(block, 0),
            RotationMaps = ReadRotation(block, 1),
            RotationFlags = ReadRotation(block, 2),
            Unread800 = (short)block[0xd3],
            Unread801 = (short)block[0xd4],
            WeaponRestrictions = ReadSegment(block, WeaponRestrictionsOffset, 16),
            MaxPlayers = (short)block[0xe5],
            BriefingTime = unchecked((int)BinaryUtility.ReadUInt32BigEndian(block, 0xe6)),
            Unread824 = BinaryUtility.ReadUInt32BigEndian(block, 0xea),
            Unread832 = BinaryUtility.ReadUInt16BigEndian(block, 0xee),
            Unread836 = BinaryUtility.ReadUInt32BigEndian(block, 0xf0),
            Unread844 = BinaryUtility.ReadUInt16BigEndian(block, 0xf4),
            Stance = (short)block[0xf6],
            LevelLimitTolerance = (short)block[0xf7],
            LevelLimitBase = unchecked((int)BinaryUtility.ReadUInt32BigEndian(block, 0xf8)),
            RuleTimers = ReadTimers(block),
            UniqueRed = (short)block[0x140],
            UniqueBlue = (short)block[0x141],
            CommonA = (short)block[0x142],
            CommonB = (short)block[0x143],
            Unread931 = (short)block[0x144],
            IdleKick = (short)BinaryUtility.ReadUInt16BigEndian(block, 0x145),
            TeamKillKick = (short)BinaryUtility.ReadUInt16BigEndian(block, 0x147),
            CaptureExtraTime = block[0x149] != 0,
            SneakingSnakeKills = (short)block[0x14a],
            UnreadTail = ReadSegment(block, TailOffset, TailLength),
        };

        settings.NonStat = (settings.UnreadTail![HostOptionsOffset - TailOffset] & NonStatBit) != 0;
        return settings;
    }

    /// <summary>Rebuilds the 0x4310 block a settings row stores.</summary>
    /// <param name="settings">Stored settings row.</param>
    public static byte[] ToPayload(CharacterHostSettings settings)
    {
        var block = new byte[BlockSize];

        StringUtility.WriteFixedStringInto(block, 0x00, settings.Name, 16);
        StringUtility.WriteFixedStringInto(block, 0x10, settings.Comment, 128);
        block[PasswordFlagOffset] = string.IsNullOrEmpty(settings.Password) ? (byte)0 : (byte)1;
        if (settings.Password is not null)
        {
            StringUtility.WriteFixedStringInto(block, PasswordOffset, settings.Password, PasswordLength);
        }

        block[0xa1] = settings.Dedicated ? (byte)1 : (byte)0;
        block[0xa2] = (byte)settings.SettingsLobbySubtype;

        for (var index = 0; index < RotationRounds; index++)
        {
            block[RotationOffset + index * RotationStep + 0] = Font(settings.RotationRules, index);
            block[RotationOffset + index * RotationStep + 1] = Font(settings.RotationMaps, index);
            block[RotationOffset + index * RotationStep + 2] = Font(settings.RotationFlags, index);
        }

        block[0xd3] = (byte)settings.Unread800;
        block[0xd4] = (byte)settings.Unread801;
        WriteSegment(block, WeaponRestrictionsOffset, settings.WeaponRestrictions, 16);
        block[0xe5] = (byte)settings.MaxPlayers;
        BinaryUtility.WriteUInt32BigEndian(block, 0xe6, (uint)settings.BriefingTime);
        BinaryUtility.WriteUInt32BigEndian(block, 0xea, (uint)settings.Unread824);
        BinaryUtility.WriteUInt16BigEndian(block, 0xee, (ushort)settings.Unread832);
        BinaryUtility.WriteUInt32BigEndian(block, 0xf0, (uint)settings.Unread836);
        BinaryUtility.WriteUInt16BigEndian(block, 0xf4, (ushort)settings.Unread844);
        block[0xf6] = (byte)settings.Stance;
        block[0xf7] = (byte)settings.LevelLimitTolerance;
        BinaryUtility.WriteUInt32BigEndian(block, 0xf8, (uint)settings.LevelLimitBase);

        for (var index = 0; index < TimersCount; index++)
        {
            var timer = settings.RuleTimers is { } timers && index < timers.Length ? timers[index] : 0;
            BinaryUtility.WriteUInt32BigEndian(block, TimersOffset + index * 4, (uint)timer);
        }

        block[0x140] = (byte)settings.UniqueRed;
        block[0x141] = (byte)settings.UniqueBlue;
        block[0x142] = (byte)settings.CommonA;
        block[0x143] = (byte)settings.CommonB;
        block[0x144] = (byte)settings.Unread931;
        BinaryUtility.WriteUInt16BigEndian(block, 0x145, (ushort)settings.IdleKick);
        BinaryUtility.WriteUInt16BigEndian(block, 0x147, (ushort)settings.TeamKillKick);
        block[0x149] = settings.CaptureExtraTime ? (byte)1 : (byte)0;
        block[0x14a] = (byte)settings.SneakingSnakeKills;

        WriteSegment(block, TailOffset, settings.UnreadTail, TailLength);
        var hostOptionsIndex = HostOptionsOffset - TailOffset;
        if (settings.NonStat)
        {
            block[TailOffset + hostOptionsIndex] |= (byte)NonStatBit;
        }
        else
        {
            block[TailOffset + hostOptionsIndex] = (byte)(block[TailOffset + hostOptionsIndex] & ~NonStatBit);
        }

        return block;
    }

    /// <summary>Re-maps a stored block into the saved-settings reply layout.</summary>
    /// <param name="block">Block bytes.</param>
    public static byte[] BuildReply(byte[] block)
    {
        var output = new byte[ReplySize];
        CopyField(output, 0x004, block, 0x00, 0x10);
        CopyField(output, 0x014, block, 0x10, 0x80);
        CopyField(output, 0x094, block, 0x90, 0x11);
        CopyField(output, 0x0a5, block, 0xa1, 1);
        CopyField(output, 0x0a6, block, 0xa3, 0x30);
        CopyField(output, 0x0d8, block, 0xd5, 0x10);
        CopyField(output, 0x0e8, block, 0xe5, 1);
        CopyField(output, 0x0e9, block, 0xe6, 4);
        CopyField(output, 0x0ed, block, 0xea, 4);
        CopyField(output, 0x0f9, block, 0xf6, 1);
        CopyField(output, 0x0fa, block, 0xf7, 1);
        CopyField(output, 0x0fb, block, 0xf8, 4);
        CopyField(output, 0x0ff, block, 0xfc, 17 * 4);
        CopyField(output, 0x143, block, 0x140, 2);
        CopyField(output, 0x145, block, 0x142, 1);
        CopyField(output, 0x146, block, 0x143, 1);
        CopyField(output, 0x147, block, 0x144, 1);
        CopyField(output, 0x148, block, 0x145, 2);
        CopyField(output, 0x14a, block, 0x147, 2);
        CopyField(output, 0x14c, block, 0x149, 1);
        CopyField(output, 0x14d, block, 0x14a, 1);
        CopyField(output, 0x14e, block, 0x14b, 8);
        CopyField(output, 0x157, block, 0x154, 1);
        CopyField(output, 0x158, block, 0x155, 1);
        return output;
    }

    /// <summary>Reads one byte of every rotation triple.</summary>
    private static short[] ReadRotation(byte[] block, int component)
    {
        var values = new short[RotationRounds];
        for (var index = 0; index < RotationRounds; index++)
        {
            values[index] = block[RotationOffset + index * RotationStep + component];
        }

        return values;
    }

    /// <summary>Reads the seventeen per-rule timer words.</summary>
    private static int[] ReadTimers(byte[] block)
    {
        var timers = new int[TimersCount];
        for (var index = 0; index < TimersCount; index++)
        {
            timers[index] = unchecked((int)BinaryUtility.ReadUInt32BigEndian(block, TimersOffset + index * 4));
        }

        return timers;
    }

    /// <summary>Copies a byte range, padding a short value to the field width.</summary>
    private static byte[] ReadSegment(byte[] block, int offset, int length)
    {
        var segment = new byte[length];
        Array.Copy(block, offset, segment, 0, length);
        return segment;
    }

    /// <summary>Writes a byte range, treating a shorter value as leading zeros.</summary>
    private static void WriteSegment(byte[] block, int offset, byte[]? value, int length)
    {
        if (value is null)
        {
            return;
        }

        Array.Copy(value, 0, block, offset, Math.Min(value.Length, length));
    }

    /// <summary>Single byte of a rotation component, or zero when the row has none.</summary>
    private static byte Font(short[]? rotation, int index) =>
        (byte)(rotation is { } values && index < values.Length ? values[index] : 0);

    /// <summary>Copies what the block holds; a short block leaves the destination zeroed.</summary>
    private static void CopyField(byte[] output, int destination, byte[] block, int source, int length)
    {
        var available = Math.Max(0, Math.Min(length, block.Length - source));
        if (available > 0)
        {
            Array.Copy(block, source, output, destination, available);
        }
    }
}