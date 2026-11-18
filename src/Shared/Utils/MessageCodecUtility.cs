using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Udp;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Codec for the content region of a peer-to-peer frame: the two-byte header
/// word followed by concatenated messages, or by a raw LZSS stream when the
/// compression marker is set.
/// </summary>
public static class MessageCodecUtility
{
    /// <summary>Parses a fully decoded datagram into a frame.</summary>
    /// <param name="decoded">Datagram with the chain and scramble removed and the digest verified.</param>
    public static UdpFrame DecodeFrame(byte[] decoded)
    {
        var rawCounter = decoded.Length >= UdpCommandConstants.HeaderSize
            ? BinaryUtility.ReadUInt16LittleEndian(decoded, 0)
            : (ushort)0;

        var compressed = (rawCounter & UdpCommandConstants.CompressionMarker) != 0;
        var contentEnd = Math.Max(UdpCommandConstants.HeaderSize, decoded.Length - UdpCommandConstants.TailSize);
        var content = decoded[UdpCommandConstants.HeaderSize..contentEnd];

        if (compressed && content.Length > 0)
        {
            // A stream that runs out of input before its marker is refused and
            // this region comes back empty: the peer's own reader fails there
            // too, and abandons the frame rather than reading half of it.
            content = LzssUtility.Decompress(content);
        }

        return new UdpFrame(
            (ushort)(rawCounter & UdpCommandConstants.CounterMask),
            compressed,
            content,
            ParseMessages(content),
            decoded);
    }

    /// <summary>Parses the concatenated records of a content region.</summary>
    /// <param name="buffer">Content region, decompressed when the frame was compressed.</param>
    public static List<UdpMessage> ParseMessages(ReadOnlySpan<byte> buffer)
    {
        var messages = new List<UdpMessage>();
        var offset = 0;

        while (offset + UdpCommandConstants.MessageHeaderSize <= buffer.Length)
        {
            var type = BinaryUtility.ReadUInt16LittleEndian(buffer, offset);
            var extended = RecordEntryFramingUtils.DeclaresExtendedLength(type);
            var sequenced = RecordEntryFramingUtils.DeclaresSequenced(type);
            var declaredLength = extended
                ? BinaryUtility.ReadUInt16LittleEndian(buffer, offset + 2)
                : buffer[offset + 2];
            var bodyLength = ReadBodyLength(type, declaredLength);

            // The fourth header byte is the record's place in its stream when the
            // identifier declares the sequenced class, and the first byte of the body
            // when it does not; a record declaring the sixteen-bit length carries its
            // ordinal one byte later, because its length takes two bytes.
            var headerSize = UdpCommandConstants.MessageHeaderSize + (extended ? 1 : 0);
            if (bodyLength < 0 || offset + headerSize + bodyLength > buffer.Length)
            {
                // A length that runs past the end means the region is not walking
                // under the framing this record claims, and everything after it
                // would be read at a false offset. Stop rather than invent.
                break;
            }

            var flags = buffer[offset + headerSize - 1];
            var bodyStart = offset + headerSize;

            messages.Add(new UdpMessage(type, (byte)Math.Min(declaredLength, byte.MaxValue), flags, buffer[bodyStart..(bodyStart + bodyLength)].ToArray()));
            offset = bodyStart + bodyLength;
        }

        return messages;
    }

    /// <summary>
    /// Reads a record's body length under the class its identifier declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The class bits decide, not the identifier's size.</b> A record whose identifier
    /// declares <see cref="RecordClassBits.Sequenced"/> spends its fourth header byte on its
    /// place in the stream, so its declared length is the body alone. A record that does not
    /// declare it has no such byte: the fourth byte is the body's first, and the declared
    /// length covers it, which is why this host's four-byte header reads one byte less than
    /// the byte says. **A sixteen-bit length does not change that rule** — it widens the
    /// length field, so the identifier and length together take one more byte before the
    /// place-or-class byte, and that byte is still the body's first when the record is not
    /// sequenced (`P2P_QueueInboundRecord`, `0x269b00`: the body starts at
    /// `2 + length width + sequenced`).
    /// </para>
    /// <para>
    /// <b>Measured on the working captures: the old threshold is wrong for eighteen
    /// identifiers.</b> Partitioning on <c>type &lt; 0x1000</c>
    /// (<see cref="UdpCommandConstants.TickRecordThreshold"/>) instead of on the class bit
    /// reads 0x889f, 0x88e5, 0x8859, 0x8863 and fourteen more as session records, when they
    /// carry no <c>0x10</c> bit and so have no ordinal: the threshold then steps one byte too
    /// far and loses the rest of the frame. Over 354 455 frames of thirty working captures,
    /// 408 distinct identifiers and 2 854 346 records, 0x68bd and 0xe702 also declare the
    /// sixteen-bit length, which the threshold rule cannot express at all.
    /// </para>
    /// </remarks>
    /// <param name="type">Record identifier.</param>
    /// <param name="declaredLength">The record's length field, already read at its own width.</param>
    /// <returns>Body length in bytes.</returns>
    public static int ReadBodyLength(ushort type, int declaredLength) =>
        RecordEntryFramingUtils.DeclaresSequenced(type)
            ? declaredLength
            : declaredLength - 1;

    /// <summary>Serializes messages into a content region payload.</summary>
    /// <remarks>
    /// The length byte follows the framing the identifier selects, the same way
    /// <see cref="ReadBodyLength"/> reads it: a tick record counts its
    /// attribute class as well as its body. Writing the body length for both
    /// would put a tick record's body one byte short of where its reader
    /// expects it, which is the mirror image of the defect §2 of
    /// <c>UDP_GAME_CAPTURE.md</c> describes on the reading side.
    /// </remarks>
    /// <param name="messages">Messages to serialize.</param>
    public static byte[] SerializeMessages(IReadOnlyList<UdpMessage> messages)
    {
        var total = 0;
        foreach (var message in messages)
        {
            total += UdpCommandConstants.MessageHeaderSize + message.Body.Length;
        }

        var output = new byte[total];
        var offset = 0;
        foreach (var message in messages)
        {
            output[offset] = (byte)(message.Type & 0xff);
            output[offset + 1] = (byte)((message.Type >> 8) & 0xff);
            output[offset + 2] = WriteBodyLength(message.Type, message.Body.Length);
            output[offset + 3] = message.Flags;
            message.Body.CopyTo(output, offset + UdpCommandConstants.MessageHeaderSize);
            offset += UdpCommandConstants.MessageHeaderSize + message.Body.Length;
        }

        return output;
    }

    /// <summary>
    /// Writes a record's length byte under the class its identifier declares: the body
    /// alone when the record is sequenced, the body plus the byte this host puts where the
    /// ordinal would be when it is not.
    /// </summary>
    /// <param name="type">Record identifier.</param>
    /// <param name="bodyLength">Length of the record's body.</param>
    /// <returns>The value the length byte carries.</returns>
    public static byte WriteBodyLength(ushort type, int bodyLength) =>
        RecordEntryFramingUtils.DeclaresSequenced(type)
            ? (byte)bodyLength
            : (byte)(bodyLength + 1);

    /// <summary>
    /// Builds the packet-log bytes for a frame: the header word followed by the
    /// content region, decompressed for compressed frames. The tail digest is
    /// omitted because it is derived checksum data rather than content.
    /// </summary>
    /// <param name="frame">Frame to render.</param>
    public static byte[] FrameToLogBytes(UdpFrame frame)
    {
        var header = new byte[UdpCommandConstants.HeaderSize];
        var counter = frame.Compressed
            ? (ushort)(frame.Counter | UdpCommandConstants.CompressionMarker)
            : frame.Counter;
        BinaryUtility.WriteUInt16LittleEndian(header, 0, counter);
        return BinaryUtility.Concatenate(header, frame.Content);
    }
}
