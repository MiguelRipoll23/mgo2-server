using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the peer-to-peer frame cipher and the handshake parser, the two
/// places an unauthenticated datagram is decoded. A regression here can end
/// the UDP host, so the malformed inputs are exercised directly.
/// </summary>
public sealed class FrameCryptoUtilityTests
{
    [Fact]
    public void Encode_then_decode_round_trips_a_frame()
    {
        var plain = BuildFrame(counter: 7, content: [0x11, 0x22, 0x33, 0x44]);
        var wire = FrameCryptoUtility.EncodeFrame(plain, counter: 7, key: 0x12345678);

        var decoded = wire.ToArray();
        var counter = FrameCryptoUtility.DecodeFrameInPlace(decoded, key: 0x12345678);

        Assert.Equal((ushort)7, counter);
        // The tail keeps the digest the decoder verified; the frame the
        // consumer reads is the header and content region.
        var readable = decoded.Length - UdpCommandConstants.TailSize;
        Assert.Equal(plain[..readable], decoded[..readable]);
    }

    [Fact]
    public void Unscramble_rejects_a_datagram_shorter_than_a_frame()
    {
        var datagram = new byte[UdpCommandConstants.FrameOverhead - 1];

        Assert.Throws<ArgumentException>(() => FrameCryptoUtility.UnscrambleHeaderOnly(datagram));
    }

    [Fact]
    public void Unscramble_accepts_the_smallest_valid_frame()
    {
        var plain = BuildFrame(counter: 1, content: []);
        var datagram = FrameCryptoUtility.EncodeFrame(plain, counter: 1, key: 0x12345678);

        var counter = FrameCryptoUtility.UnscrambleHeaderOnly(datagram);

        Assert.Equal((ushort)1, counter);
    }

    [Fact]
    public void Tail_digest_rejects_a_tampered_frame()
    {
        var plain = BuildFrame(counter: 3, content: [0xaa, 0xbb]);
        var wire = FrameCryptoUtility.EncodeFrame(plain, counter: 3, key: 0x0badf00d).ToArray();
        wire[^1] ^= 0xff;

        var counter = FrameCryptoUtility.UnscrambleHeaderOnly(wire);

        Assert.Equal((ushort)3, counter);
        Assert.False(FrameCryptoUtility.VerifyTailDigest(wire, UdpCryptoKeyConstants.TailDigestKey));
    }

    [Fact]
    public void ParseHandshake_rejects_a_truncated_body()
    {
        // One byte short of the fixed body a handshake must carry.
        Assert.Null(FrameBuilderUtility.ParseHandshakeBody(new byte[0x1b]));
        Assert.Null(FrameBuilderUtility.ParseHandshakeBody([]));
    }

    [Fact]
    public void ParseHandshake_reads_a_body_it_built()
    {
        var body = FrameBuilderUtility.BuildHandshakeBody(42, 9, "127.0.0.1", 5730);

        var parsed = FrameBuilderUtility.ParseHandshakeBody(body);

        Assert.NotNull(parsed);
        Assert.Equal(42u, parsed.PeerIdentifier);
        Assert.Equal(9u, parsed.CounterBase);
        Assert.Equal(2, parsed.Pairs.Count);
    }

    private static byte[] BuildFrame(ushort counter, byte[] content)
    {
        var frame = new byte[UdpCommandConstants.HeaderSize + content.Length + UdpCommandConstants.TailSize];
        BinaryUtility.WriteUInt16LittleEndian(frame, 0, counter);
        content.CopyTo(frame, UdpCommandConstants.HeaderSize);
        return frame;
    }
}

/// <summary>Exercises the TCP packet codec round-trip and its length guards.</summary>
public sealed class PacketCodecServiceTests
{
    /// <summary>A command whose payload is neither encrypted nor decrypted.</summary>
    private const ushort PlainCommand = 0x1234;

    private static readonly PacketCodecService Codec = new(NullLogger<PacketCodecService>.Instance);

    [Fact]
    public void Encode_then_decode_round_trips_a_packet()
    {
        byte[] payload = [1, 2, 3, 4];

        var bytes = Codec.EncodePacket(PlainCommand, payload, sequenceOut: 5);
        var packet = Codec.DecodePacket(bytes);

        Assert.NotNull(packet);
        Assert.Equal(PlainCommand, packet.Header.Command);
        Assert.Equal((uint)5, packet.Header.Sequence);
        Assert.Equal(payload, packet.Payload);
    }

    [Fact]
    public void Decode_returns_null_for_a_truncated_buffer()
    {
        Assert.Null(Codec.DecodePacket(new byte[PacketConstants.HeaderSize - 1]));
    }

    [Fact]
    public void Decode_returns_null_when_the_checksum_is_wrong()
    {
        Assert.Null(Codec.DecodePacket(new byte[PacketConstants.HeaderSize]));
    }

    [Fact]
    public void Encode_refuses_a_payload_over_the_protocol_limit()
    {
        var oversized = new byte[PacketConstants.MaximumPayloadLength + 1];

        Assert.Throws<InvalidOperationException>(
            () => Codec.EncodePacket(PlainCommand, oversized, sequenceOut: 1));
    }
}

/// <summary>
/// Guards the reader contract that a short or negative read length cannot
/// fault a handler, and the Latin-1 writer that drops characters it cannot
/// encode rather than widening them into a wrong byte.
/// </summary>
public sealed class PacketReaderUtilityTests
{
    [Fact]
    public void ReadBytes_clamps_a_negative_length_to_nothing()
    {
        var reader = new PacketReader([1, 2, 3, 4]);

        Assert.Empty(reader.ReadBytes(-5));
    }

    [Fact]
    public void ReadBytes_clamps_to_the_remaining_bytes()
    {
        var reader = new PacketReader([1, 2, 3, 4]);

        Assert.Equal([1, 2, 3, 4], reader.ReadBytes(99));
    }

    [Fact]
    public void ReadFixedString_clamps_to_the_remaining_bytes()
    {
        var reader = new PacketReader([(byte)'a', (byte)'b']);

        Assert.Equal("ab", reader.ReadFixedString(10));
    }

    [Fact]
    public void ReadInt16_reads_a_signed_value()
    {
        var reader = new PacketReader([0xff, 0xfe]);

        Assert.Equal((short)-2, reader.ReadInt16());
    }

    [Fact]
    public void WriteFixedString_writes_nul_for_a_character_above_latin1()
    {
        var bytes = StringUtility.WriteFixedString("a\u0100b", 4);

        Assert.Equal([(byte)'a', (byte)0, (byte)'b', (byte)0], bytes);
    }

    [Fact]
    public void WriteFixedStringInto_normalizes_a_character_above_latin1()
    {
        var destination = new byte[4];

        StringUtility.WriteFixedStringInto(destination, 0, "a\u0100b", 4);

        Assert.Equal([(byte)'a', (byte)0, (byte)'b', (byte)0], destination);
    }
}
