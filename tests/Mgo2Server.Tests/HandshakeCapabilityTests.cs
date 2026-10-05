using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// The handshake body's capability byte and its two endpoint pairs.
/// </summary>
/// <remarks>
/// These are codec facts, so they are pinned against the capture rather than
/// against a description of it. The bodies here are the ones the live dedicated
/// server was seen to send and receive, so a change that makes this server
/// behave unlike that one fails here rather than in a client that ignores it.
/// </remarks>
public sealed class HandshakeCapabilityTests
{
    /// <summary>Body the joining client of the gameplay capture announced.</summary>
    private static readonly byte[] JoinerHandshakeBody =
    [
        0x2f, 0x23, 0x01, 0x00, // peer_id 74543
        0xa6, 0x73, 0x67, 0x73, // counter_base 0x736773a6
        0xb7, 0x8a, 0x25, 0x4d, // module_magic
        0x02,                   // capability
        0x02, 0x00,             // mode
        0x02,                   // pair count
        0xd9, 0x8a, 0xd5, 0x05, 0x62, 0x16, // 217.138.213.5:5730
        0x0a, 0x02, 0x00, 0x02, 0x62, 0x16, // 10.2.0.2:5730
    ];

    /// <summary>
    /// A handshake is refused when the peer cleared the session-flag capability
    /// bit while this host's own capability requires it, and not otherwise.
    /// </summary>
    /// <remarks>
    /// The client builds this byte from its own session flags and folds the
    /// peer's back into them, so bit 2 is negotiated rather than a version. The
    /// live capture holds three flows whose joiner announced 0x06 — that bit set
    /// — and the real dedicated server answered all three. Refusing them is the
    /// regression this test exists to catch.
    /// </remarks>
    [Fact]
    public void CapabilityBitSetIsAccepted()
    {
        var body = (byte[])JoinerHandshakeBody.Clone();
        body[12] = 0x06;

        var handshake = FrameBuilderUtility.ParseHandshakeBody(body);

        Assert.NotNull(handshake);
        Assert.Equal(0x06, handshake.Capability);
    }

    /// <summary>The captured capability, 0x02, is accepted and read back as written.</summary>
    [Fact]
    public void CapturedCapabilityIsAccepted()
    {
        var handshake = FrameBuilderUtility.ParseHandshakeBody(JoinerHandshakeBody);

        Assert.NotNull(handshake);
        Assert.Equal(0x02, handshake.Capability);
    }

    /// <summary>
    /// A handshake whose module magic is wrong is still refused: the capability
    /// gate must not become a way past it.
    /// </summary>
    [Fact]
    public void WrongModuleMagicIsRefused()
    {
        var body = (byte[])JoinerHandshakeBody.Clone();
        body[8] ^= 0xff;

        Assert.Null(FrameBuilderUtility.ParseHandshakeBody(body));
    }

    /// <summary>The handshake this host builds carries the captured capability and mode.</summary>
    [Fact]
    public void BuiltHandshakeCarriesCapturedCapabilityAndMode()
    {
        var body = FrameBuilderUtility.BuildHandshakeBody(0x0000a001, 0x3c8a65d6, "99.66.131.177", 5731);

        Assert.Equal(28, body.Length);
        Assert.Equal(0x02, body[12]);
        Assert.Equal(0x01, BinaryUtility.ReadUInt16LittleEndian(body, 13));
        Assert.Equal(UdpCryptoKeyConstants.ModuleMagic, BinaryUtility.ReadUInt32LittleEndian(body, 8));
    }

    /// <summary>
    /// The two advertised pairs are public then private, as the live host sent
    /// them, rather than one address twice.
    /// </summary>
    [Fact]
    public void BuiltHandshakeAdvertisesPublicThenPrivate()
    {
        var body = FrameBuilderUtility.BuildHandshakeBody(
            0x0000a001,
            0x3c8a65d6,
            "99.66.131.177",
            5731,
            "10.104.10.28");

        var handshake = FrameBuilderUtility.ParseHandshakeBody(body);

        Assert.NotNull(handshake);
        Assert.Equal(2, handshake.Pairs.Count);
        Assert.Equal("99.66.131.177", handshake.Pairs[0].Address);
        Assert.Equal("10.104.10.28", handshake.Pairs[1].Address);
        Assert.Equal(5731, handshake.Pairs[0].Port);
        Assert.Equal(5731, handshake.Pairs[1].Port);
    }

    /// <summary>
    /// With no private address configured the public one fills both slots, which
    /// is all a host reachable at a single address can put there.
    /// </summary>
    [Fact]
    public void BuiltHandshakeFallsBackToOneAddressForBothPairs()
    {
        var body = FrameBuilderUtility.BuildHandshakeBody(0x0000a001, 0x3c8a65d6, "127.0.0.1", 5730);

        var handshake = FrameBuilderUtility.ParseHandshakeBody(body);

        Assert.NotNull(handshake);
        Assert.Equal("127.0.0.1", handshake.Pairs[0].Address);
        Assert.Equal("127.0.0.1", handshake.Pairs[1].Address);
    }

    /// <summary>
    /// A handshake is refused when it declares more pairs than the body holds,
    /// so a truncated body cannot walk past the end.
    /// </summary>
    [Fact]
    public void ExcessivePairCountIsRefused()
    {
        var body = (byte[])JoinerHandshakeBody.Clone();
        body[15] = 5;

        Assert.Null(FrameBuilderUtility.ParseHandshakeBody(body));
    }

    /// <summary>A short body is refused rather than read past its end.</summary>
    [Fact]
    public void TruncatedBodyIsRefused()
    {
        Assert.Null(FrameBuilderUtility.ParseHandshakeBody(JoinerHandshakeBody[..20]));
    }
}
