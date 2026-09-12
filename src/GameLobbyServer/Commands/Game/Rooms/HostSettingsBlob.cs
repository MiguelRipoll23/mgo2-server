using System.Text.Json;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>The stored host-settings slot the client reads and writes.</summary>
public static class HostSettingsType
{
    /// <summary>The single current settings blob of a character.</summary>
    public const int Value = 0;
}

/// <summary>Fields read out of the host-settings blob the client pushes.</summary>
/// <param name="Name">Room name.</param>
/// <param name="Comment">Room comment.</param>
/// <param name="PasswordEnabled">Whether the room is password-locked.</param>
/// <param name="Password">Room password, empty when the room is open.</param>
/// <param name="MaximumPlayers">Player cap the host chose.</param>
/// <param name="Rotation">Rotation triples, rule first.</param>
public sealed record HostSettingsBlock(
    string Name,
    string Comment,
    bool PasswordEnabled,
    string Password,
    int MaximumPlayers,
    IReadOnlyList<int[]> Rotation);

/// <summary>
/// Codec of the host-settings blob. The request shape and the saved-settings
/// reply are the same structure re-based, so both directions live here.
/// </summary>
public static class HostSettingsBlobCodec
{
    /// <summary>Size of the reply when the client never pushed settings.</summary>
    public const int EmptyReplySize = 128;

    /// <summary>Size of the reply holding a re-mapped settings blob.</summary>
    public const int ReplySize = 0x15c;

    /// <summary>Shortest request blob the reply mapping reads from.</summary>
    public const int MinimumBlobLength = 0x156;

    /// <summary>Number of rotation entries in the blob.</summary>
    private const int RotationRounds = 16;

    /// <summary>Offset of the rotation inside the request blob.</summary>
    private const int RotationOffset = 0xa3;

    /// <summary>Decodes the stored JSON byte array; returns <c>null</c> on garbage.</summary>
    /// <param name="settingsJson">Stored settings text.</param>
    public static byte[]? Decode(string settingsJson)
    {
        try
        {
            var numbers = JsonSerializer.Deserialize<int[]>(settingsJson);
            if (numbers is null)
            {
                return null;
            }

            return [.. numbers.Select(number => (byte)number)];
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Decodes the stored JSON byte array into a byte array.</summary>
    /// <param name="settingsJson">Stored settings text.</param>
    public static string Encode(byte[] bytes) =>
        JsonSerializer.Serialize(bytes.Select(value => (int)value).ToArray());

    /// <summary>Reads the fields of the request blob.</summary>
    /// <param name="blob">Request blob bytes.</param>
    public static HostSettingsBlock? Parse(byte[] blob)
    {
        if (blob.Length < 0xf9)
        {
            return null;
        }

        var rotation = new List<int[]>();
        for (var index = 0; index < RotationRounds && RotationOffset + index * 3 + 2 < blob.Length; index++)
        {
            var rule = blob[RotationOffset + index * 3];
            var map = blob[RotationOffset + index * 3 + 1];
            var flags = blob[RotationOffset + index * 3 + 2];
            if (rule == 0 && map == 0)
            {
                break;
            }

            rotation.Add([rule, map, flags]);
        }

        return new HostSettingsBlock(
            StringUtility.ReadFixedString(blob, 0x00, 16),
            StringUtility.ReadFixedString(blob, 0x10, 128),
            blob[0x90] != 0,
            StringUtility.ReadFixedString(blob, 0x91, 16),
            blob[0xe5],
            rotation);
    }

    /// <summary>Re-maps a stored request blob into the saved-settings reply layout.</summary>
    /// <param name="blob">Request blob bytes.</param>
    public static byte[] BuildReply(byte[] blob)
    {
        var output = new byte[ReplySize];
        CopyField(output, 0x004, blob, 0x00, 0x10);
        CopyField(output, 0x014, blob, 0x10, 0x80);
        CopyField(output, 0x094, blob, 0x90, 0x11);
        CopyField(output, 0x0a5, blob, 0xa1, 1);
        CopyField(output, 0x0a6, blob, 0xa3, 0x30);
        CopyField(output, 0x0d8, blob, 0xd5, 0x10);
        CopyField(output, 0x0e8, blob, 0xe5, 1);
        CopyField(output, 0x0e9, blob, 0xe6, 4);
        CopyField(output, 0x0ed, blob, 0xea, 4);
        CopyField(output, 0x0f9, blob, 0xf6, 1);
        CopyField(output, 0x0fa, blob, 0xf7, 1);
        CopyField(output, 0x0fb, blob, 0xf8, 4);
        CopyField(output, 0x0ff, blob, 0xfc, 17 * 4);
        CopyField(output, 0x143, blob, 0x140, 2);
        CopyField(output, 0x145, blob, 0x142, 1);
        CopyField(output, 0x146, blob, 0x143, 1);
        CopyField(output, 0x147, blob, 0x144, 1);
        CopyField(output, 0x148, blob, 0x145, 2);
        CopyField(output, 0x14a, blob, 0x147, 2);
        CopyField(output, 0x14c, blob, 0x149, 1);
        CopyField(output, 0x14d, blob, 0x14a, 1);
        CopyField(output, 0x14e, blob, 0x14b, 8);
        CopyField(output, 0x157, blob, 0x154, 1);
        CopyField(output, 0x158, blob, 0x155, 1);
        return output;
    }

    /// <summary>Copies what the blob holds; a short blob leaves the destination zeroed.</summary>
    private static void CopyField(byte[] output, int destination, byte[] blob, int source, int length)
    {
        var available = Math.Max(0, Math.Min(length, blob.Length - source));
        if (available > 0)
        {
            Array.Copy(blob, source, output, destination, available);
        }
    }
}
