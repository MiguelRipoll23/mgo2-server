using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Plaintext frame builders for the peer-to-peer channel; wire framing is added
/// afterwards by <see cref="FrameCryptoUtility.EncodeFrame"/>.
/// </summary>
public static class FrameBuilderUtility
{
    /// <summary>Size in bytes of a handshake message body.</summary>
    private const int HandshakeBodySize = 0x1c;

    /// <summary>Number of endpoint pairs a handshake advertises.</summary>
    private const byte HandshakePairCount = 2;

    /// <summary>
    /// Builds a plaintext frame wrapping messages: the header word, the
    /// serialized messages and the zeroed tail region that
    /// <see cref="FrameCryptoUtility.EncodeFrame"/> fills in.
    /// </summary>
    /// <param name="counter">Frame counter.</param>
    /// <param name="messages">Messages to wrap.</param>
    public static byte[] BuildMessageFrame(ushort counter, IReadOnlyList<UdpMessage> messages)
    {
        var header = new byte[UdpCommandConstants.HeaderSize];
        BinaryUtility.WriteUInt16LittleEndian(header, 0, counter);
        return BinaryUtility.Concatenate(
            header,
            MessageCodecUtility.SerializeMessages(messages),
            new byte[UdpCommandConstants.TailSize]);
    }

    /// <summary>Creates a message whose declared length matches its body.</summary>
    /// <param name="type">Message type.</param>
    /// <param name="body">Body bytes.</param>
    /// <param name="flags">Per-message flags byte.</param>
    public static UdpMessage MessageOf(ushort type, ReadOnlySpan<byte> body, byte flags = 0) =>
        UdpMessage.Create(type, body, flags);

    /// <summary>Creates the empty keep-alive message.</summary>
    public static UdpMessage KeepAliveMessage() =>
        UdpMessage.Create(UdpCommandConstants.KeepAlive, ReadOnlySpan<byte>.Empty);

    /// <summary>
    /// Builds the handshake message body. The peer identifier is the host's
    /// character identifier, because the joining client's drain gate checks the
    /// reply's peer identifier against the peer descriptor stored in its dial
    /// session rather than echoing its own identifier. The same endpoint is
    /// advertised in both pair slots.
    /// </summary>
    /// <param name="peerIdentifier">Identifier of the sending host.</param>
    /// <param name="counterBase">Counter base the sender will use.</param>
    /// <param name="advertiseAddress">Address to advertise to the peer.</param>
    /// <param name="advertisePort">Port to advertise to the peer.</param>
    public static byte[] BuildHandshakeBody(
        uint peerIdentifier,
        uint counterBase,
        string advertiseAddress,
        int advertisePort)
    {
        var body = new byte[HandshakeBodySize];
        BinaryUtility.WriteUInt32LittleEndian(body, 0, peerIdentifier);
        BinaryUtility.WriteUInt32LittleEndian(body, 4, counterBase);
        BinaryUtility.WriteUInt32LittleEndian(body, 8, UdpCryptoKeyConstants.ModuleMagic);
        body[12] = 0x02;
        body[13] = 0x01;
        body[14] = 0x00;
        body[15] = HandshakePairCount;

        var address = IpAddressToBytes(advertiseAddress) ?? [127, 0, 0, 1];
        address.CopyTo(body, 16);
        BinaryUtility.WriteUInt16LittleEndian(body, 20, (ushort)advertisePort);
        address.CopyTo(body, 22);
        BinaryUtility.WriteUInt16LittleEndian(body, 26, (ushort)advertisePort);
        return body;
    }

    /// <summary>Parses a handshake message body.</summary>
    /// <param name="body">Message body to parse.</param>
    /// <returns>The parsed handshake, or <c>null</c> when the body is not a valid handshake.</returns>
    public static HandshakeBody? ParseHandshakeBody(ReadOnlySpan<byte> body)
    {
        if (body.Length < HandshakeBodySize)
        {
            return null;
        }

        var magic = BinaryUtility.ReadUInt32LittleEndian(body, 8);
        if (magic != UdpCryptoKeyConstants.ModuleMagic)
        {
            return null;
        }

        var flags = body[12];
        if ((flags & 0x04) != 0)
        {
            return null;
        }

        var pairCount = body[15];
        if (pairCount > 2)
        {
            return null;
        }

        var pairs = new List<HandshakeEndpointPair>(pairCount);
        for (var index = 0; index < pairCount; index++)
        {
            var offset = 16 + (index * 6);
            pairs.Add(new HandshakeEndpointPair(
                $"{body[offset]}.{body[offset + 1]}.{body[offset + 2]}.{body[offset + 3]}",
                BinaryUtility.ReadUInt16LittleEndian(body, offset + 4)));
        }

        return new HandshakeBody(
            BinaryUtility.ReadUInt32LittleEndian(body, 0),
            BinaryUtility.ReadUInt32LittleEndian(body, 4),
            magic,
            flags,
            BinaryUtility.ReadUInt16LittleEndian(body, 13),
            pairs);
    }

    /// <summary>Converts a dotted IPv4 address into its four bytes.</summary>
    /// <param name="address">Dotted address text.</param>
    /// <returns>The four address bytes, or <c>null</c> when the text is not a valid IPv4 address.</returns>
    public static byte[]? IpAddressToBytes(string address)
    {
        var parts = address.Split('.');
        if (parts.Length != 4)
        {
            return null;
        }

        var bytes = new byte[4];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!byte.TryParse(parts[index], out bytes[index]))
            {
                return null;
            }
        }

        return bytes;
    }
}

/// <summary>Host identity announced in a handshake message body.</summary>
/// <param name="PeerIdentifier">Identifier of the sending host.</param>
/// <param name="CounterBase">Counter base the sender announced.</param>
/// <param name="Magic">Module magic carried by the handshake.</param>
/// <param name="Flags">Handshake flags byte.</param>
/// <param name="Unknown">Trailing unknown field.</param>
/// <param name="Pairs">Endpoint pairs advertised by the sender.</param>
public sealed record HandshakeBody(
    uint PeerIdentifier,
    uint CounterBase,
    uint Magic,
    byte Flags,
    ushort Unknown,
    IReadOnlyList<HandshakeEndpointPair> Pairs);

/// <summary>One endpoint advertised in a handshake body.</summary>
/// <param name="Address">Dotted IPv4 address.</param>
/// <param name="Port">UDP port.</param>
public sealed record HandshakeEndpointPair(string Address, ushort Port);
