using System.Buffers.Binary;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Reads a packet payload field by field. Integers are read big-endian, the byte
/// order the client writes them in. Reads past the end of the payload are
/// clamped so a short packet cannot fault a handler.
/// </summary>
public struct PacketReader(byte[] buffer)
{
    private readonly byte[] buffer = buffer;
    private int position;

    /// <summary>Number of bytes consumed so far.</summary>
    public readonly int Position => position;

    /// <summary>Number of bytes left to read.</summary>
    public readonly int Remaining => buffer.Length - position;

    /// <summary>Reads an unsigned eight-bit value.</summary>
    public byte ReadUInt8()
    {
        var value = position < buffer.Length ? buffer[position] : (byte)0;
        Advance(1);
        return value;
    }

    /// <summary>Reads an unsigned 16-bit value.</summary>
    public ushort ReadUInt16()
    {
        var value = Remaining >= 2 ? BinaryUtility.ReadUInt16BigEndian(buffer, position) : (ushort)0;
        Advance(2);
        return value;
    }

    /// <summary>Reads an unsigned 32-bit value.</summary>
    public uint ReadUInt32()
    {
        var value = Remaining >= 4 ? BinaryUtility.ReadUInt32BigEndian(buffer, position) : 0;
        Advance(4);
        return value;
    }

    /// <summary>Reads a signed 32-bit value.</summary>
    public int ReadInt32()
    {
        var value = Remaining >= 4 ? BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(position)) : 0;
        Advance(4);
        return value;
    }

    /// <summary>Reads a signed eight-bit value.</summary>
    public sbyte ReadInt8() => unchecked((sbyte)ReadUInt8());

    /// <summary>Reads a number of raw bytes.</summary>
    /// <param name="length">Number of bytes to read.</param>
    public byte[] ReadBytes(int length)
    {
        var count = Math.Min(length, Remaining);
        var slice = buffer.AsSpan(position, count).ToArray();
        Advance(length);
        return slice;
    }

    /// <summary>Reads a fixed-length ISO-8859-1 string.</summary>
    /// <param name="maximumLength">Length of the field.</param>
    public string ReadFixedString(int maximumLength)
    {
        var count = Math.Min(maximumLength, Remaining);
        var value = StringUtility.ReadFixedString(buffer, position, count);
        Advance(maximumLength);
        return value;
    }

    /// <summary>Skips a number of bytes.</summary>
    /// <param name="length">Number of bytes to skip.</param>
    public PacketReader Skip(int length)
    {
        Advance(length);
        return this;
    }

    private void Advance(int length) => position = Math.Min(position + length, buffer.Length);
}
