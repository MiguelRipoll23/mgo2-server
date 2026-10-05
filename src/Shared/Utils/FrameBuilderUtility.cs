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
    /// Capability byte this host puts in its own handshake.
    /// </summary>
    /// <remarks>
    /// The client derives the handshake body from its session flags: the byte is
    /// <c>2</c>, or <c>3</c> when its own flag <c>0x100</c> is set, with bit 2
    /// (<see cref="CapabilitySessionFlagBit"/>) set when its flag <c>0x800</c>
    /// is. This host asserts none of those extra flags, so it advertises the
    /// plain <c>2</c> — the value the live dedicated server sent on the
    /// gameplay channel.
    /// </remarks>
    public const byte HostCapability = 0x02;

    /// <summary>
    /// Bit of the capability byte that mirrors the session flag <c>0x800</c>.
    /// </summary>
    public const byte CapabilitySessionFlagBit = 0x04;

    /// <summary>Byte written at offset 12 of a handshake body: the capability.</summary>
    private const int CapabilityOffset = 12;

    /// <summary>Offset of the u16 a handshake body carries after the capability.</summary>
    private const int ModeOffset = 13;

    /// <summary>Value this host puts in that u16; the live dedicated server's is 1.</summary>
    private const ushort HostMode = 1;

    /// <summary>
    /// Builds a plaintext frame wrapping messages: the header word, the
    /// serialized messages and the zeroed tail region that
    /// <see cref="FrameCryptoUtility.EncodeFrame"/> fills in.
    /// </summary>
    /// <param name="counter">Frame counter.</param>
    /// <param name="messages">Messages to wrap.</param>
    /// <param name="compressed">
    /// Whether the content region goes out as an LZSS stream with the
    /// compression marker set in the header word. The recorded host marks the
    /// roster run, the roster repeat and the post-join burst and leaves the
    /// short records that close them unmarked, so this is per run rather than
    /// a property of the peer.
    /// </param>
    public static byte[] BuildMessageFrame(
        ushort counter,
        IReadOnlyList<UdpMessage> messages,
        bool compressed = false)
    {
        var header = new byte[UdpCommandConstants.HeaderSize];
        BinaryUtility.WriteUInt16LittleEndian(
            header,
            0,
            compressed ? (ushort)(counter | UdpCommandConstants.CompressionMarker) : counter);
        var content = MessageCodecUtility.SerializeMessages(messages);
        return BinaryUtility.Concatenate(
            header,
            compressed ? LzssUtility.Compress(content) : content,
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
    /// <param name="advertiseAddress">Public address to advertise to the peer.</param>
    /// <param name="advertisePort">Port to advertise to the peer.</param>
    /// <param name="privateAddress">
    /// Second pair of the handshake. The live host advertises its public address
    /// first and its private one second; omitted, the public address is used for
    /// both, which is what a host reachable at one address has to do.
    /// </param>
    public static byte[] BuildHandshakeBody(
        uint peerIdentifier,
        uint counterBase,
        string advertiseAddress,
        int advertisePort,
        string? privateAddress = null)
    {
        var body = new byte[HandshakeBodySize];
        BinaryUtility.WriteUInt32LittleEndian(body, 0, peerIdentifier);
        BinaryUtility.WriteUInt32LittleEndian(body, 4, counterBase);
        BinaryUtility.WriteUInt32LittleEndian(body, 8, UdpCryptoKeyConstants.ModuleMagic);
        body[CapabilityOffset] = HostCapability;
        BinaryUtility.WriteUInt16LittleEndian(body, ModeOffset, HostMode);
        body[15] = HandshakePairCount;

        var publicBytes = IpAddressToBytes(advertiseAddress) ?? [127, 0, 0, 1];
        var privateBytes = IpAddressToBytes(privateAddress ?? advertiseAddress) ?? publicBytes;
        publicBytes.CopyTo(body, 16);
        BinaryUtility.WriteUInt16LittleEndian(body, 20, (ushort)advertisePort);
        privateBytes.CopyTo(body, 22);
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

        var capability = body[CapabilityOffset];

        // The client's gate is a disjunction, not a flat refusal: it refuses only
        // when the peer left this capability bit clear while the client's own
        // 0x800 flag requires it, and the builder sets that bit exactly when its
        // own 0x800 is set. This host advertises the plain 2, so it requires
        // nothing and accepts either value. Refusing every handshake that sets
        // the bit drops peers the live dedicated server answers — the capture
        // holds three flows whose joiner sent 0x06 and were answered anyway.
        if ((HostCapability & CapabilitySessionFlagBit) != 0 &&
            (capability & CapabilitySessionFlagBit) == 0)
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
            capability,
            BinaryUtility.ReadUInt16LittleEndian(body, ModeOffset),
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
/// <param name="Capability">Capability byte the sender advertises.</param>
/// <param name="Mode">Value of the u16 that follows the capability.</param>
/// <param name="Pairs">Endpoint pairs advertised by the sender.</param>
public sealed record HandshakeBody(
    uint PeerIdentifier,
    uint CounterBase,
    uint Magic,
    byte Capability,
    ushort Mode,
    IReadOnlyList<HandshakeEndpointPair> Pairs);

/// <summary>One endpoint advertised in a handshake body.</summary>
/// <param name="Address">Dotted IPv4 address.</param>
/// <param name="Port">UDP port.</param>
public sealed record HandshakeEndpointPair(string Address, ushort Port);
