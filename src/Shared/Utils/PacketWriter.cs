namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Builds a packet payload field by field. Integers are written big-endian, the
/// byte order the client's readers expect.
/// </summary>
public sealed class PacketWriter
{
    private readonly MemoryStream buffer = new();

    /// <summary>Number of bytes written so far.</summary>
    public int Size => (int)buffer.Length;

    /// <summary>Writes an unsigned eight-bit value.</summary>
    /// <param name="value">Value to write.</param>
    public PacketWriter WriteUInt8(int value)
    {
        buffer.WriteByte((byte)(value & 0xff));
        return this;
    }

    /// <summary>Writes an unsigned 16-bit value.</summary>
    /// <param name="value">Value to write.</param>
    public PacketWriter WriteUInt16(int value)
    {
        Span<byte> encoded = stackalloc byte[2];
        BinaryUtility.WriteUInt16BigEndian(encoded, 0, (ushort)value);
        buffer.Write(encoded);
        return this;
    }

    /// <summary>Writes an unsigned 32-bit value.</summary>
    /// <param name="value">Value to write.</param>
    public PacketWriter WriteUInt32(uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        BinaryUtility.WriteUInt32BigEndian(encoded, 0, value);
        buffer.Write(encoded);
        return this;
    }

    /// <summary>Writes a signed eight-bit value.</summary>
    /// <param name="value">Value to write.</param>
    public PacketWriter WriteInt8(int value)
    {
        buffer.WriteByte((byte)(sbyte)value);
        return this;
    }

    /// <summary>Writes a signed 32-bit value.</summary>
    /// <param name="value">Value to write.</param>
    public PacketWriter WriteInt32(int value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(encoded, value);
        buffer.Write(encoded);
        return this;
    }

    /// <summary>Writes raw bytes.</summary>
    /// <param name="bytes">Bytes to write.</param>
    public PacketWriter WriteBytes(ReadOnlySpan<byte> bytes)
    {
        buffer.Write(bytes);
        return this;
    }

    /// <summary>Writes a fixed-length ISO-8859-1 string, padded with zeroes.</summary>
    /// <param name="value">Text to write.</param>
    /// <param name="length">Number of bytes to write.</param>
    public PacketWriter WriteFixedString(string value, int length)
    {
        buffer.Write(StringUtility.WriteFixedString(value, length));
        return this;
    }

    /// <summary>Writes zero bytes.</summary>
    /// <param name="length">Number of bytes to write.</param>
    public PacketWriter WritePadding(int length)
    {
        if (length <= 0)
        {
            return this;
        }

        buffer.Write(new byte[length]);
        return this;
    }

    /// <summary>Returns the payload written so far.</summary>
    public byte[] Build() => buffer.ToArray();
}
