using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;

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
            // No null check on purpose: a stream that ends by exhaustion is
            // already handled inside the decompressor, and swallowing the
            // result here is what used to lose the joiner's profile frame.
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
            var declaredLength = buffer[offset + 2];
            var bodyLength = ReadBodyLength(type, declaredLength);
            if (bodyLength < 0 || offset + UdpCommandConstants.MessageHeaderSize + bodyLength > buffer.Length)
            {
                // A length that runs past the end means the region is not walking
                // under the framing this record claims, and everything after it
                // would be read at a false offset. Stop rather than invent.
                break;
            }

            var flags = buffer[offset + 3];
            var bodyStart = offset + UdpCommandConstants.MessageHeaderSize;

            messages.Add(new UdpMessage(type, declaredLength, flags, buffer[bodyStart..(bodyStart + bodyLength)].ToArray()));
            offset = bodyStart + bodyLength;
        }

        return messages;
    }

    /// <summary>
    /// Reads a record's length byte under the framing its identifier selects.
    /// </summary>
    /// <remarks>
    /// Two framings share this wire and the fourth header byte means different
    /// things in each. A session record counts the body alone; a tick record
    /// counts the body plus the attribute class, which is why its body is one
    /// byte shorter than the byte says. Reading every record the session way
    /// desynchronises a frame at its first tick record - see
    /// docs/protocol/UDP_GAME_CAPTURE.md §2.
    /// </remarks>
    /// <param name="type">Record identifier.</param>
    /// <param name="lengthByte">The record's length byte.</param>
    /// <returns>Body length in bytes.</returns>
    public static int ReadBodyLength(ushort type, byte lengthByte) =>
        type < UdpCommandConstants.TickRecordThreshold ? lengthByte - 1 : lengthByte;

    /// <summary>Serializes messages into a content region payload.</summary>
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
            output[offset + 2] = (byte)message.Body.Length;
            output[offset + 3] = message.Flags;
            message.Body.CopyTo(output, offset + UdpCommandConstants.MessageHeaderSize);
            offset += UdpCommandConstants.MessageHeaderSize + message.Body.Length;
        }

        return output;
    }

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
