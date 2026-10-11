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
/// The marker is the reader's only clean exit and every stream this project has
/// seen ends at one: all 5157 compressed frames of <c>mgo2-game1..3.pcapng</c>,
/// in both directions, and every live joiner frame the tests hold. A stream that
/// runs out of input before the marker is therefore refused, as the peer's own
/// reader refuses it (it returns -1 at <c>0xefdae0</c> and its caller drops the
/// frame), and the offset is read and tested before the four-bit length field is
/// read, because the marker is ten bits and may be followed by fewer than four
/// bits of padding.
/// </remarks>
public static class LzssUtility
{
    /// <summary>
    /// Decompresses an LZSS stream. A stream that ends at the zero-offset marker
    /// yields what it carried; one that runs out of input first is refused.
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
                return [];
            }

            if (flag == 1)
            {
                var literal = bits.ReadBits(8);
                if (literal < 0)
                {
                    return [];
                }

                output[outputIndex++] = (byte)literal;
                ring[writeIndex++ & 0x1ff] = (byte)literal;
                continue;
            }

            // The offset comes before the length field on purpose: the marker is
            // the flag plus these nine bits, and a stream may end with fewer than
            // four bits behind it. Reading the length first reports the marker as
            // a truncated reference and loses the frame it closes.
            var offset = bits.ReadBits(9);
            if (offset < 0)
            {
                return [];
            }

            if (offset == 0)
            {
                return output[..outputIndex];
            }

            var lengthField = bits.ReadBits(4);
            if (lengthField < 0)
            {
                return [];
            }

            for (var index = 0; index < lengthField + 2 && outputIndex < maximumOutput; index++)
            {
                var value = ring[(offset + index) & 0x1ff];
                output[outputIndex++] = value;
                ring[writeIndex++ & 0x1ff] = value;
            }
        }

        // Either the input ran out before a marker or the output ceiling was
        // reached without one: neither is a stream that was read to its end, so
        // nothing in it is handed to the caller to parse as records.
        return [];
    }

    /// <summary>
    /// Compresses to the bitstream <see cref="Decompress"/> reads, so a frame
    /// written here is one the peer rebuilds with its own decompressor. The
    /// encoder closes the stream with the zero-offset marker, as the recorded
    /// host does.
    /// </summary>
    /// <remarks>
    /// The marker is not optional. The peer's reader has one clean exit, the
    /// zero-offset marker at <c>0xefdc38</c>; a stream that runs out of input
    /// first returns -1 and the frame carrying it is abandoned before any record
    /// in it is reached. That is what a compressed roster run written without the
    /// marker did: the run and its repeat went out marked, the peer answered
    /// nothing, and the same two runs sent in the clear were answered 56 ms later.
    /// <para>
    /// The ring is written from index 1, so the byte at logical position
    /// <c>p</c> lives in slot <c>(p + 1) &amp; 0x1ff</c> and a back-reference at
    /// distance <c>d</c> is emitted as offset <c>(writeIndex - d) &amp; 0x1ff</c>.
    /// That offset is nine bits of ring position and the decoder reads zero as
    /// its end-of-stream marker, so a distance whose position lands on the
    /// ring's start is skipped: taking it would encode a reference the peer
    /// reads as the end of the stream. Only content longer than the 512-byte
    /// ring can reach it, which is why a roster of one or two players never
    /// showed it and a full room's run does. A match may
    /// overlap itself: the decoder reads and writes the same slot on each step of
    /// a copy, so a run longer than its own distance decodes correctly, and the
    /// match search has to look ahead into the source for the part that has not
    /// been emitted yet. The encoder caps matches at 16 bytes for content up to
    /// 128 bytes and 15 bytes for larger content: those settings reproduce the
    /// captured 13-byte state-blob and 148-byte match-start compressed regions.
    /// </para>
    /// </remarks>
    /// <param name="source">Bytes to compress.</param>
    /// <returns>An LZSS stream closed by the zero-offset end-of-stream marker.</returns>
    public static byte[] Compress(ReadOnlySpan<byte> source)
    {
        const int ringMask = UdpCommandConstants.LzssRingSize - 1;
        const int minProfitableLength = 2;
        const int longContentThreshold = 128;
        var maxLength = source.Length > longContentThreshold ? 15 : 16;

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

                if (length <= bestLength)
                {
                    continue;
                }

                // The offset is a ring position in nine bits and a zero offset
                // is the decoder's end-of-stream marker, so a reference whose
                // position falls on the ring's start would end the peer's
                // stream there and drop everything behind it. Content shorter
                // than the ring cannot reach it; a longer run can, and the
                // escape is to leave that distance out rather than mask its
                // bits, which would decode as the marker it collides with.
                var offset = (writeIndex - distance) & ringMask;
                if (offset == 0)
                {
                    continue;
                }

                bestLength = length;
                bestOffset = offset;
                if (bestLength == maxLength)
                {
                    break;
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

        // The stream's end: a zero flag and a nine-bit zero offset. The writer
        // pads the last byte behind it, so the marker is the ten bits before
        // that padding and never straddles the region the frame declares.
        writer.WriteBit(0);
        writer.WriteBits(0, 9);

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
