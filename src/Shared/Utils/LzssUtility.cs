using Mgo2Server.Shared.Constants;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// LZSS decompressor matching the game's bitstream exactly: most significant
/// bit first, flag 1 means a literal byte, flag 0 means a back-reference with a
/// nine-bit absolute ring offset and a four-bit length field (length = field +
/// 2). The ring write index starts at 1 and offset 0 is the end-of-stream
/// marker.
/// </summary>
public static class LzssUtility
{
    /// <summary>Decompresses an LZSS stream, or returns <c>null</c> when the stream is truncated.</summary>
    /// <param name="source">Compressed bytes.</param>
    public static byte[]? Decompress(ReadOnlySpan<byte> source)
    {
        var maximumOutput = UdpCommandConstants.LzssMaximumOutput;
        var output = new byte[maximumOutput];
        var ring = new byte[UdpCommandConstants.LzssRingSize];
        var writeIndex = 1;
        var outputIndex = 0;
        var totalBits = source.Length * 8;

        var bits = new BitReader(source, totalBits);

        while (bits.Position < totalBits && outputIndex < maximumOutput)
        {
            var flag = bits.ReadBit();
            if (flag < 0)
            {
                break;
            }

            if (flag == 1)
            {
                var literal = bits.ReadBits(8);
                if (literal < 0)
                {
                    return null;
                }

                output[outputIndex++] = (byte)literal;
                ring[writeIndex++ & 0x1ff] = (byte)literal;
                continue;
            }

            var offset = bits.ReadBits(9);
            var lengthField = bits.ReadBits(4);
            if (offset < 0 || lengthField < 0)
            {
                return null;
            }

            if (offset == 0)
            {
                break;
            }

            for (var index = 0; index < lengthField + 2 && outputIndex < maximumOutput; index++)
            {
                var value = ring[(offset + index) & 0x1ff];
                output[outputIndex++] = value;
                ring[writeIndex++ & 0x1ff] = value;
            }
        }

        return output[..outputIndex];
    }

    /// <summary>Most-significant-bit-first reader over a byte span.</summary>
    /// <param name="source">Compressed bytes.</param>
    /// <param name="totalBits">Number of readable bits.</param>
    private ref struct BitReader(ReadOnlySpan<byte> source, int totalBits)
    {
        private readonly ReadOnlySpan<byte> source = source;

        /// <summary>Bit position of the cursor.</summary>
        public int Position { get; private set; }

        /// <summary>Reads a single bit, or -1 when the stream is exhausted.</summary>
        public int ReadBit()
        {
            if (Position >= totalBits)
            {
                return -1;
            }

            var value = (source[Position >> 3] >> (7 - (Position & 7))) & 1;
            Position++;
            return value;
        }

        /// <summary>Reads <paramref name="count"/> bits, or -1 when the stream is exhausted.</summary>
        /// <param name="count">Number of bits to read.</param>
        public int ReadBits(int count)
        {
            var value = 0;
            for (var index = 0; index < count; index++)
            {
                var bit = ReadBit();
                if (bit < 0)
                {
                    return -1;
                }

                value = (value << 1) | bit;
            }

            return value;
        }
    }
}
