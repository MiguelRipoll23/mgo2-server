using System.Text;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Helpers for the fixed-length, ISO-8859-1 encoded strings used by the wire
/// format. Multi-byte encodings would shift every field that follows, so the
/// text is never encoded as UTF-8.
/// </summary>
public static class StringUtility
{
    /// <summary>Reads a NUL-terminated fixed-length string.</summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Byte offset of the string.</param>
    /// <param name="maximumLength">Maximum number of bytes to read.</param>
    public static string ReadFixedString(ReadOnlySpan<byte> buffer, int offset, int maximumLength)
    {
        var slice = buffer.Slice(offset, maximumLength);
        var terminator = slice.IndexOf((byte)0);
        return Encoding.Latin1.GetString(terminator >= 0 ? slice[..terminator] : slice);
    }

    /// <summary>Writes a fixed-length NUL-padded ISO-8859-1 string.</summary>
    /// <param name="value">Text to write.</param>
    /// <param name="maximumLength">Length of the returned buffer.</param>
    public static byte[] WriteFixedString(string value, int maximumLength)
    {
        var bytes = new byte[maximumLength];
        for (var index = 0; index < maximumLength; index++)
        {
            bytes[index] = index < value.Length ? (byte)value[index] : (byte)0;
        }

        return bytes;
    }

    /// <summary>Writes a fixed-length NUL-padded ISO-8859-1 string into a buffer.</summary>
    /// <param name="destination">Buffer to write into.</param>
    /// <param name="offset">Byte offset of the field.</param>
    /// <param name="value">Text to write.</param>
    /// <param name="maximumLength">Length of the field.</param>
    public static void WriteFixedStringInto(Span<byte> destination, int offset, string value, int maximumLength)
    {
        for (var index = 0; index < maximumLength; index++)
        {
            destination[offset + index] = index < value.Length ? (byte)value[index] : (byte)0;
        }
    }

    /// <summary>
    /// Validates a character name: printable ASCII and Latin-1, excluding
    /// leading and trailing spaces.
    /// </summary>
    /// <param name="name">Name to validate.</param>
    public static bool IsValidName(string name)
    {
        if (string.IsNullOrEmpty(name) || name[0] == ' ' || name[^1] == ' ')
        {
            return false;
        }

        foreach (var character in name)
        {
            var isPrintableAscii = character is >= '\x21' and <= '\x7e';
            var isLatin1Supplement = character is >= '\xa1' and <= '\xff';
            if (!isPrintableAscii && !isLatin1Supplement && character != ' ')
            {
                return false;
            }
        }

        return true;
    }
}
