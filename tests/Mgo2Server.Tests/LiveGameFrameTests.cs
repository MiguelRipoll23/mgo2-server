using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Decodes live frames from a dedicated-server game captured in
/// <c>docs/mgo2-game.pcapng</c>, offline.
/// </summary>
/// <remarks>
/// The capture is not committed; the frame bytes below are lifted from it, so
/// these tests run without it. One side is a joining player, the other the
/// dedicated server. Unlike the frames in <see cref="LatestJoinerFrameTests"/>,
/// these come from a round that actually ran, so they carry the in-game tick
/// records - and with them the framing this file exists to pin.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class LiveGameFrameTests
{
    /// <summary>
    /// Session key of that game: the two handshake counter bases,
    /// <c>0x736773a6</c> (joiner) and <c>0x3c8a65d6</c> (server), exclusive-ored.
    /// </summary>
    private const uint SessionKey = 0x4fed1670;

    /// <summary>
    /// A 246-byte frame the dedicated server sent mid-round, compressed, header
    /// <c>0x8005</c>. It opens with a session record and then carries sixty-odd
    /// tick records, so it is the frame that fails if the two framings are read
    /// as one.
    /// </summary>
    private const string ServerTickFrame =
        "e9670a5a77ad13877fec8246bd0edbb17d343ebe7d05203182c9c0029a191e689b741c0f9df5e0ee60d1a310f22a3bb3e2322f31005"
        + "3cf77494349724c67587d4d6f665f5c6fe0173e7f6018d762fc6972fff7f529f763fef40f6f5ea71eff9849a0bd01d9bf654e87bed96d"
        + "a7e0f574371af3d9f70b90399ae57682a9bdce3ecef46e1f81442a560252b2fb5b213ece5ab551c6a23a62e245c17ad2474bc0dda"
        + "84efc56a2d4f7954c35460567ef3597f47771645ab60171dd3974405008f588a77bd3cc5579a614ee9f148eb460e46ff03f1bb1"
        + "f1a0d5ec06ade01cfe6eb1dde294ebcb4569118cc215dd8c91a2611a3089adcf0b9c";

    /// <summary>
    /// A 75-byte frame the joiner sent, uncompressed, header <c>0x0024</c>. It
    /// opens with two session records and then runs five tick records to the
    /// last byte, so it is the frame that fails if either framing is applied
    /// throughout: the first three records would swallow the fourth under the
    /// session reading, and the tick reading would under-count the first.
    /// </summary>
    private const string JoinerTickFrame =
        "4ab011d69e64cdbc9e76e0498e84da499e881a4a7c98244ab266598a589952034f9864ec4f956d1454a96d1854a97c1153ace02666"
        + "51d3d697a4e92ea8ce193bbc6784c2b341e3";

    /// <summary>
    /// Decodes a wire datagram into its content region, the way the server does.
    /// </summary>
    private static (ushort Counter, UdpFrame Frame) Decode(string wire)
    {
        var work = Convert.FromHexString(wire);
        var counter = FrameCryptoUtility.UnscrambleHeaderOnly(work);
        var digestOk = FrameCryptoUtility.VerifyTailDigest(
            work,
            SessionKey ^ UdpCryptoKeyConstants.TailDigestKey);
        FrameCryptoUtility.RemoveChainInPlace(work, counter, SessionKey);

        Assert.True(digestOk, "the tail digest of a captured frame must verify");
        return (counter, MessageCodecUtility.DecodeFrame(work));
    }

    /// <summary>
    /// The server's frame opens with session records and then runs tick records
    /// to the last byte. The single-convention reading cannot do this: reading
    /// every length as a body length walks 291 of the capture's 19018 frames to
    /// their end, this being one of the 18728 that need the identifier read
    /// first. 290 frames still do not walk and are recorded as unresolved in
    /// docs/protocol/UDP_GAME_CAPTURE.md §2.
    /// </summary>
    [Fact]
    public void A_server_frame_walks_to_its_last_byte()
    {
        var (counter, frame) = Decode(ServerTickFrame);

        Assert.Equal((ushort)0x8005, counter);
        Assert.True(frame.Compressed);

        // Every record is accounted for: the sum of the headers and bodies is
        // the whole content region, with nothing left over and nothing read
        // twice. This is the assertion that fails under the old parser.
        var consumed = frame.Messages.Sum(message => UdpCommandConstants.MessageHeaderSize + message.Body.Length);
        Assert.Equal(frame.Content.Length, consumed);

        // It really is a mixed frame, or the assertion above would prove nothing.
        Assert.Contains(frame.Messages, message => message.Type >= UdpCommandConstants.TickRecordThreshold);
        Assert.Contains(frame.Messages, message => message.Type < UdpCommandConstants.TickRecordThreshold);
    }

    /// <summary>
    /// The joiner's frame is mostly tick records with session records interleaved,
    /// which is the harder case: the parser has to switch framing record by record
    /// to land on the last byte. It opens and closes with session records
    /// (<c>0x5000</c> and <c>0x9001</c>) around a run of ticks.
    /// </summary>
    [Fact]
    public void A_joiner_frame_switches_framing_record_by_record_and_lands_exactly()
    {
        var (counter, frame) = Decode(JoinerTickFrame);

        Assert.Equal((ushort)0x0024, counter);
        Assert.False(frame.Compressed);

        var consumed = frame.Messages.Sum(message => UdpCommandConstants.MessageHeaderSize + message.Body.Length);
        Assert.Equal(frame.Content.Length, consumed);

        // Two session records, then five tick records, and the last of those
        // runs to the final byte.
        Assert.Equal(7, frame.Messages.Count);
        Assert.Equal(0x5a54, frame.Messages[0].Type);
        Assert.Equal(0x090d, frame.Messages[2].Type);
        Assert.Equal(0x090f, frame.Messages[^1].Type);
    }

    /// <summary>
    /// A tick record's declared length is one more than its body, because the
    /// byte counts the attribute class as well. The record keeps the declared
    /// byte it arrived with and the body it actually has, so a consumer reading
    /// <see cref="UdpMessage.Length"/> is not silently handed a body length.
    /// </summary>
    [Fact]
    public void A_tick_record_keeps_the_length_byte_it_arrived_with()
    {
        var (_, frame) = Decode(JoinerTickFrame);

        var tick = frame.Messages.First(message => message.Type < UdpCommandConstants.TickRecordThreshold);

        Assert.Equal(tick.Length - 1, tick.Body.Length);
    }

    /// <summary>
    /// The framing rule itself, on the two ends of the threshold. A tick record's
    /// length byte counts its attribute class as well as its body; a session
    /// record's counts the body alone.
    /// </summary>
    [Fact]
    public void The_length_byte_is_read_by_the_record_identifier()
    {
        // Below the threshold: body length is one less than the byte.
        Assert.Equal(15, MessageCodecUtility.ReadBodyLength(0x0081, 16));
        Assert.Equal(0, MessageCodecUtility.ReadBodyLength(0x0261, 1));

        // At and above it: the byte is the body length.
        Assert.Equal(16, MessageCodecUtility.ReadBodyLength(0x1001, 16));
        Assert.Equal(0, MessageCodecUtility.ReadBodyLength(0x9001, 0));
    }

    /// <summary>
    /// A record whose length runs past the end of the region ends the walk
    /// rather than being read at a false offset. Before this, the parser clamped
    /// the body to whatever was left and carried on, which is how a
    /// mis-framed region produced a plausible record with the wrong bytes.
    /// </summary>
    [Fact]
    public void A_length_that_runs_past_the_region_ends_the_walk()
    {
        // A tick record claiming a body longer than the region holds.
        byte[] content = [0x61, 0x0a, 0x40, 0x01, 0x02];

        var messages = MessageCodecUtility.ParseMessages(content);

        Assert.Empty(messages);
    }
}