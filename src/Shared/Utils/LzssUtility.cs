using Mgo2Server.Shared.Constants;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// LZSS decompressor matching the game's bitstream exactly: most significant
/// bit first, flag 1 means a literal byte, flag 0 means a back-reference with a
/// nine-bit absolute ring offset and a four-bit length field (length = field +
/// 2). The ring write index starts at 1 and offset 0 is the end-of-stream
/// marker.
/// </summary>
/// <remarks>
/// The marker is what the decoder implements, but a captured joiner frame runs
/// out of input mid-token without ever writing it: the stream ends by
/// exhaustion. Exhaustion is therefore not a failure — it returns what was
/// decoded so far. Returning nothing instead loses the whole frame, and the
/// frame is a join request: the digest still verifies, the host has a session
/// and no profile, and the joiner re-sends the same body indefinitely waiting
/// for a roster that never comes.
/// </remarks>
public static class LzssUtility
{
    /// <summary>
    /// Decompresses an LZSS stream. A stream that ends by exhaustion rather
    /// than by the marker yields the bytes decoded so far.
    /// </summary>
    /// <param name="source">Compressed bytes.</param>
    public static byte[] Decompress(ReadOnlySpan<byte> source)
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
                    return output[..outputIndex];
                }

                output[outputIndex++] = (byte)literal;
                ring[writeIndex++ & 0x1ff] = (byte)literal;
                continue;
            }

            var offset = bits.ReadBits(9);
            var lengthField = bits.ReadBits(4);
            if (offset < 0 || lengthField < 0)
            {
                return output[..outputIndex];
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

    /// <summary>
    /// Compresses to the same bitstream <see cref="Decompress"/> reads, so a frame
    /// written here is one the peer rebuilds with its own decompressor.
    /// </summary>
    /// <remarks>
    /// The recorded host marks the compression bit on the roster run, the roster
    /// repeat and the post-join burst, and leaves the short trailer and follow-up
    /// records unmarked, so this is the other half of a format that is only half
    /// implemented here: <see cref="Decompress"/> reads the marker and nothing in
    /// the outbound path ever set it.
    /// <para>
    /// The ring is written from index 1, so the byte at logical position
    /// <c>p</c> lives in slot <c>(p + 1) &amp; 0x1ff</c> and a back-reference at
    /// distance <c>d</c> is emitted as offset <c>writeIndex - d</c>. A match may
    /// overlap itself: the decoder reads and writes the same slot on each step of
    /// a copy, so a run longer than its own distance decodes correctly, and the
    /// match search has to look ahead into the source for the part that has not
    /// been emitted yet.
    /// </para>
    /// </remarks>
    /// <param name="source">Bytes to compress.</param>
    /// <returns>An LZSS stream ending on the zero-offset marker.</returns>
    public static byte[] Compress(ReadOnlySpan<byte> source)
    {
        const int ringMask = UdpCommandConstants.LzssRingSize - 1;
        const int maxLength = 17;
        const int minProfitableLength = 3;

        var ring = new byte[UdpCommandConstants.LzssRingSize];
        var writer = new BitWriter();
        var emitted = 0;
        var writeIndex = 1;

        // A ReadOnlySpan cannot be captured by the look-ahead below, so the
        // match search reads the same bytes through a plain array.
        var data = source.ToArray();

        int ByteAt(int logicalPosition) => logicalPosition >= emitted
            ? data[logicalPosition]
            : ring[(logicalPosition + 1) & ringMask];

        while (emitted < data.Length)
        {
            var bestLength = 0;
            var bestOffset = 0;
            var maxDistance = Math.Min(emitted, 0x1ff);

            for (var distance = 1; distance <= maxDistance; distance++)
            {
                var length = 0;
                while (length < maxLength && emitted + length < data.Length)
                {
                    if (ByteAt(emitted - distance + length) != data[emitted + length])
                    {
                        break;
                    }

                    length++;
                }

                if (length > bestLength)
                {
                    bestLength = length;
                    bestOffset = writeIndex - distance;
                    if (bestLength == maxLength)
                    {
                        break;
                    }
                }
            }

            if (bestLength >= minProfitableLength)
            {
                writer.WriteBit(0);
                writer.WriteBits(bestOffset, 9);
                writer.WriteBits(bestLength - 2, 4);
                for (var index = 0; index < bestLength; index++)
                {
                    ring[writeIndex++ & ringMask] = data[emitted + index];
                }

                emitted += bestLength;
                continue;
            }

            writer.WriteBit(1);
            writer.WriteBits(data[emitted], 8);
            ring[writeIndex++ & ringMask] = data[emitted];
            emitted++;
        }

        // The marker is a back-reference whose offset is zero. The decoder reads
        // the offset and the length field before it tests the offset, so both
        // have to be on the wire even though the length is never used.
        writer.WriteBit(0);
        writer.WriteBits(0, 9);
        writer.WriteBits(0, 4);

        return writer.ToArray();
    }

    /// <summary>Most-significant-bit-first writer over a growable byte buffer.</summary>
    private sealed class BitWriter
    {
        private readonly List<byte> buffer = [];

        /// <summary>Bit cursor inside the current byte.</summary>
        private int bit;

        /// <summary>Appends one bit.</summary>
        /// <param name="value">0 or 1.</param>
        public void WriteBit(int value)
        {
            if (bit == 0)
            {
                buffer.Add(0);
            }

            if (value != 0)
            {
                buffer[^1] |= (byte)(1 << (7 - bit));
            }

            bit = (bit + 1) & 7;
        }

        /// <summary>Appends <paramref name="count"/> bits, most significant first.</summary>
        /// <param name="value">Value to write.</param>
        /// <param name="count">Number of bits.</param>
        public void WriteBits(int value, int count)
        {
            for (var index = count - 1; index >= 0; index--)
            {
                WriteBit((value >> index) & 1);
            }
        }

        /// <summary>The bytes written so far.</summary>
        /// <returns>A copy of the buffer.</returns>
        public byte[] ToArray() => [.. buffer];
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
