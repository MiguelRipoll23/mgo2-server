using Mgo2Server.GameplayServer.Rooms;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the burst payloads to the bytes the recorded host wrote in
/// <c>docs/mgo2-game.pcapng</c> at t+4.9929, outbound counter 5.
/// </summary>
/// <remarks>
/// The burst used to be built as the leading tag, the slot, and zeros for the
/// rest of the length, and it left out the two fifty-four-byte records in slot
/// <c>0x15</c> that the capture shows after the joiner's payload. Both are
/// differences from the recorded host, and both are what these tests hold.
/// </remarks>
public sealed class PostJoinBurstServiceTests
{
    [Fact]
    public void TheHostPayloadIsTheRecordedOne()
    {
        var body = PostJoinBurstService.HostPayload();

        Assert.Equal(161, body.Length);
        Assert.Equal(PostJoinBurstService.HostPayloadLength, body.Length);
        Assert.Equal(0x0b, body[0]);
        Assert.Equal(PostJoinBurstService.HostPayloadSlot, body[1]);
        // The capture's host payload is not a zero-filled body: it opens on
        // 0b 00 ff 27, which a tag-plus-slot build could never produce.
        Assert.Equal(0xff, body[2]);
        Assert.Equal(0x27, body[3]);
    }

    [Fact]
    public void ThePlayerPayloadIsTheRecordedOne()
    {
        var body = PostJoinBurstService.PlayerPayload();

        Assert.Equal(50, body.Length);
        Assert.Equal(PostJoinBurstService.PlayerPayloadLength, body.Length);
        Assert.Equal(0x0b, body[0]);
        Assert.Equal(PostJoinBurstService.PlayerPayloadSlot, body[1]);
        Assert.Equal(0x02, body[2]);
    }

    /// <summary>
    /// The capture writes the fifty-four-byte record twice, byte for byte
    /// identical, and it is in slot <c>0x15</c> rather than the joiner's
    /// <c>0x01</c>.
    /// </summary>
    [Fact]
    public void TheUnknownPayloadIsTheRecordedOne()
    {
        var first = PostJoinBurstService.UnknownPayload();
        var second = PostJoinBurstService.UnknownPayload();

        Assert.Equal(54, first.Length);
        Assert.Equal(PostJoinBurstService.UnknownPayloadLength, first.Length);
        Assert.Equal(0x0b, first[0]);
        Assert.Equal(PostJoinBurstService.UnknownPayloadSlot, first[1]);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// The three payloads in one burst share a tag and differ in slot and
    /// length, which is what tells the peer they are three records.
    /// </summary>
    [Fact]
    public void TheThreePayloadsAreDistinguishable()
    {
        var payloads = new[]
        {
            PostJoinBurstService.PlayerPayload(),
            PostJoinBurstService.UnknownPayload(),
            PostJoinBurstService.UnknownPayload(),
        };

        Assert.All(payloads, payload => Assert.Equal(0x0b, payload[0]));
        Assert.Equal(0x01, payloads[0][1]);
        Assert.Equal(0x15, payloads[1][1]);
        Assert.Equal(0x15, payloads[2][1]);
        Assert.NotEqual(payloads[0], payloads[1]);
    }

    [Fact]
    public void TheStateBlobIsTheRecordedOne()
    {
        var blob = PostJoinBurstService.BuildStateBlob();

        Assert.Equal(22, blob.Length);
        Assert.Equal(0x40, blob[18]);
        Assert.Equal(0x00, blob[19]);
        Assert.Equal(0x00, blob[20]);
        Assert.Equal(0x32, blob[21]);
        Assert.Equal(new byte[18], blob[..18]);
    }

    /// <summary>
    /// The shape builder stays available for the tests that only care about the
    /// tag and the slot, but nothing in the send path uses it: a zero-filled
    /// body is not one the peer has been shown to read.
    /// </summary>
    [Fact]
    public void TheShapeBuilderStillWritesTheTagAndSlot()
    {
        var body = PostJoinBurstService.BuildPayload(PostJoinBurstService.UnknownPayloadSlot, 54);

        Assert.Equal(54, body.Length);
        Assert.Equal(0x0b, body[0]);
        Assert.Equal(0x15, body[1]);
        Assert.Equal(new byte[52], body[2..]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PostJoinBurstService.BuildPayload(0x00, 1));
    }
}