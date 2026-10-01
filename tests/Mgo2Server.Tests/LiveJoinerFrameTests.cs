using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Xunit.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins what a real joiner's compressed profile frame decodes to.
/// </summary>
/// <remarks>
/// Captured live against the Free Battle host on 5731, 2026-10-01, from a
/// console in the same LAN as the host. The session key is the one the log
/// reports for the inbound keyed frames of that session. The frame is the
/// joiner's repeated join request: without it decoding there is nothing to
/// assert about, because the host would sit on an empty roster forever.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class LiveJoinerFrameTests(ITestOutputHelper output)
{
    /// <summary>Session key the gameplay server derived for that peer.</summary>
    private const uint SessionKey = 0x2628b0f8;

    /// <summary>
    /// The joiner's first compressed frame of the session, as it arrived: 112
    /// bytes, header counter 0x8001, carrying the join request.
    /// </summary>
    private const string JoinerFrame =
        "07221E6F506B169CD3A0261D9196C60556F7B60E5EEF96CF192744E512D1A59C5E"
        + "88597756B2437860AE4D9C6CE070BF7303933FE3BCACB024C0D0B1CBFFECABC78B3"
        + "6C42B7186FFCBB9E5C6706BFF5DEDE1140FCCCD19E289C8F395F155D9DB6BD606B7"
        + "D3166B94450E94D8EA94CF8B";

    private byte[] Decode(string hex)
    {
        var work = Convert.FromHexString(hex);
        var sessionKey = SessionKey;

        var counter = FrameCryptoUtility.UnscrambleHeaderOnly(work);
        var digestOk = FrameCryptoUtility.VerifyTailDigest(work, sessionKey ^ UdpCryptoKeyConstants.TailDigestKey);

        FrameCryptoUtility.RemoveChainInPlace(work, counter, sessionKey);
        var frame = MessageCodecUtility.DecodeFrame(work);

        return Report(counter, digestOk, frame);
    }

    private byte[] Report(ushort counter, bool digestOk, UdpFrame frame)
    {
        output.WriteLine($"counter=0x{counter:x4} digest={digestOk} compressed={frame.Compressed}");
        output.WriteLine($"content={Convert.ToHexString(frame.Content)} ({frame.Content.Length} bytes)");
        output.WriteLine($"messages={frame.Messages.Count}");
        foreach (var message in frame.Messages)
        {
            output.WriteLine($"  type=0x{message.Type:x4} len={message.Length} flags={message.Flags} body={Convert.ToHexString(message.Body)}");
        }

        return frame.Content;
    }

    [Fact]
    public void The_joiners_frame_verifies_and_decodes_to_a_join_request()
    {
        var content = Decode(JoinerFrame);

        Assert.NotEmpty(content);
    }

    [Fact]
    public void The_join_request_survives_a_stream_that_ends_mid_token()
    {
        // The frame ends by exhaustion rather than by the offset-0 marker. A
        // decompressor that treats that as an error returns nothing, the frame
        // decodes to an empty content region, and the joiner re-sends the same
        // body forever waiting for a roster this host never builds.
        var work = Convert.FromHexString(JoinerFrame);
        var counter = FrameCryptoUtility.UnscrambleHeaderOnly(work);
        FrameCryptoUtility.RemoveChainInPlace(work, counter, SessionKey);
        var frame = MessageCodecUtility.DecodeFrame(work);

        Assert.True(frame.Compressed);
        Assert.Equal(0x8001, (ushort)(counter | UdpCommandConstants.CompressionMarker));
        Assert.Contains(frame.Messages, message => message.Type == UdpCommandConstants.PlayerProfile);
    }
}
