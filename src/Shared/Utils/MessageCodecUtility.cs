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
            content = LzssUtility.Decompress(content) ?? [];
        }

        return new UdpFrame(
            (ushort)(rawCounter & UdpCommandConstants.CounterMask),
            compressed,
            content,
            ParseMessages(content),
            decoded);
    }

    /// <summary>Parses the concatenated messages of a content region.</summary>
    /// <param name="buffer">Content region, decompressed when the frame was compressed.</param>
    public static List<UdpMessage> ParseMessages(ReadOnlySpan<byte> buffer)
    {
        var messages = new List<UdpMessage>();
        var offset = 0;

        while (offset + UdpCommandConstants.MessageHeaderSize <= buffer.Length)
        {
            var type = BinaryUtility.ReadUInt16LittleEndian(buffer, offset);
            var length = buffer[offset + 2];
            var flags = buffer[offset + 3];
            var bodyStart = offset + UdpCommandConstants.MessageHeaderSize;
            var bodyEnd = Math.Min(bodyStart + length, buffer.Length);

            messages.Add(new UdpMessage(type, length, flags, buffer[bodyStart..bodyEnd].ToArray()));
            offset = bodyStart + length;
        }

        return messages;
    }

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
