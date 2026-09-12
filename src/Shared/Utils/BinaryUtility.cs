using System.Buffers.Binary;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Binary readers and writers used by the wire formats. All helpers operate on
/// spans so they can be used on pooled buffers without copying.
/// </summary>
public static class BinaryUtility
{
    /// <summary>Reads a little-endian unsigned 16-bit value.</summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Byte offset of the value.</param>
    public static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> buffer, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(buffer[offset..]);

    /// <summary>Reads a little-endian unsigned 32-bit value.</summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Byte offset of the value.</param>
    public static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> buffer, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(buffer[offset..]);

    /// <summary>Writes a little-endian unsigned 16-bit value.</summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="offset">Byte offset of the value.</param>
    /// <param name="value">Value to write.</param>
    public static void WriteUInt16LittleEndian(Span<byte> buffer, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[offset..], value);

    /// <summary>Writes a little-endian unsigned 32-bit value.</summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="offset">Byte offset of the value.</param>
    /// <param name="value">Value to write.</param>
    public static void WriteUInt32LittleEndian(Span<byte> buffer, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[offset..], value);

    /// <summary>Reads a big-endian unsigned 16-bit value.</summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Byte offset of the value.</param>
    public static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> buffer, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(buffer[offset..]);

    /// <summary>Reads a big-endian unsigned 32-bit value.</summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Byte offset of the value.</param>
    public static uint ReadUInt32BigEndian(ReadOnlySpan<byte> buffer, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(buffer[offset..]);

    /// <summary>Writes a big-endian unsigned 16-bit value.</summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="offset">Byte offset of the value.</param>
    /// <param name="value">Value to write.</param>
    public static void WriteUInt16BigEndian(Span<byte> buffer, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16BigEndian(buffer[offset..], value);

    /// <summary>Writes a big-endian unsigned 32-bit value.</summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="offset">Byte offset of the value.</param>
    /// <param name="value">Value to write.</param>
    public static void WriteUInt32BigEndian(Span<byte> buffer, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32BigEndian(buffer[offset..], value);

    /// <summary>Concatenates two buffers into a new array.</summary>
    /// <param name="first">First buffer.</param>
    /// <param name="second">Second buffer.</param>
    public static byte[] Concatenate(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var result = new byte[first.Length + second.Length];
        first.CopyTo(result);
        second.CopyTo(result.AsSpan(first.Length));
        return result;
    }

    /// <summary>Concatenates three buffers into a new array.</summary>
    /// <param name="first">First buffer.</param>
    /// <param name="second">Second buffer.</param>
    /// <param name="third">Third buffer.</param>
    public static byte[] Concatenate(
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> third)
    {
        var result = new byte[first.Length + second.Length + third.Length];
        first.CopyTo(result);
        second.CopyTo(result.AsSpan(first.Length));
        third.CopyTo(result.AsSpan(first.Length + second.Length));
        return result;
    }
}
