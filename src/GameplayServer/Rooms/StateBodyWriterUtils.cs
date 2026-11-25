namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The appends the room-state body's writers share.
/// </summary>
/// <remarks>
/// The body is little-endian throughout — its rule times, ticket counts, settings
/// switches and briefing time all appear low byte first — so every multi-byte append
/// goes through here rather than through a shift written at the call site.
/// </remarks>
internal static class StateBodyWriterUtils
{
    /// <summary>Appends <paramref name="count"/> zero bytes.</summary>
    /// <param name="destination">The body being built.</param>
    /// <param name="count">How many bytes to append.</param>
    internal static void WriteZeroBytes(List<byte> destination, int count)
    {
        for (var index = 0; index < count; index++)
        {
            destination.Add(0);
        }
    }

    /// <summary>Appends an unsigned 8-bit value, which must fit in a byte.</summary>
    /// <param name="destination">The body being built.</param>
    /// <param name="value">The value, which must fit in 8 bits.</param>
    internal static void WriteUInt8(List<byte> destination, int value)
    {
        if (value is < byte.MinValue or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The state field is an unsigned 8-bit value.");
        }

        destination.Add((byte)value);
    }

    /// <summary>Appends an unsigned 16-bit value, low byte first.</summary>
    /// <param name="destination">The body being built.</param>
    /// <param name="value">The value, which must fit in 16 bits.</param>
    internal static void WriteUInt16LittleEndian(List<byte> destination, int value)
    {
        if (value is < ushort.MinValue or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The state field is an unsigned 16-bit value.");
        }

        destination.Add((byte)value);
        destination.Add((byte)(value >> 8));
    }

    /// <summary>Appends an unsigned 32-bit value, low byte first.</summary>
    /// <param name="destination">The body being built.</param>
    /// <param name="value">The value.</param>
    internal static void WriteUInt32LittleEndian(List<byte> destination, uint value)
    {
        destination.Add((byte)value);
        destination.Add((byte)(value >> 8));
        destination.Add((byte)(value >> 16));
        destination.Add((byte)(value >> 24));
    }
}
