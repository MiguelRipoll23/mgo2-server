using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Xunit.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// Decodes one live joiner frame from a named session, offline.
/// </summary>
/// <remarks>
/// Frames come out of the gameplay server's log with the session key beside
/// them, so a capture can be replayed through the real decode path without a
/// console attached. The frame that failed to reach the profile handler is the
/// one worth having here: it is the difference between "the join request is
/// lost" and "the join request arrives and the parser misreads it".
/// </remarks>
[Trait("Category", "Shared")]
public sealed class LatestJoinerFrameTests(ITestOutputHelper output)
{
    /// <summary>Session key the gameplay server logged for that peer.</summary>
    private const uint SessionKey = 0x1c060bf5;

    /// <summary>
    /// The joiner's 106-byte frame, first of the data phase in that session.
    /// </summary>
    private const string JoinerFrame =
        "A032454D3DB44D9A60BB4D8066C84E24E7CC58309A5976931436508391A72D246"
        + "2473D5413C99C341629CFC96854F72D13A6F8431571225CF5E2ABDD42A6CB518C"
        + "0409FD812B3764AC2017D088A9A147BC0C0E1A0CB0BE732BAC6C3AA2EDD4BD405E"
        + "D1E44C3D60537937";

    [Fact]
    public void The_frame_decodes_to_something_the_host_can_read()
    {
        var work = Convert.FromHexString(JoinerFrame);
        var counter = FrameCryptoUtility.UnscrambleHeaderOnly(work);
        var digestOk = FrameCryptoUtility.VerifyTailDigest(work, SessionKey ^ UdpCryptoKeyConstants.TailDigestKey);
        FrameCryptoUtility.RemoveChainInPlace(work, counter, SessionKey);

        var frame = MessageCodecUtility.DecodeFrame(work);

        output.WriteLine($"counter=0x{counter:x4} digest={digestOk} compressed={frame.Compressed}");
        output.WriteLine($"content ({frame.Content.Length}) = {Convert.ToHexString(frame.Content)}");
        output.WriteLine($"printable tail = {System.Text.Encoding.ASCII.GetString(frame.Content).TrimEnd('\0')}");
        output.WriteLine($"messages = {frame.Messages.Count}");
        foreach (var message in frame.Messages)
        {
            output.WriteLine($"  type=0x{message.Type:x4} len={message.Length} flags={message.Flags}");
            output.WriteLine($"    body={Convert.ToHexString(message.Body)}");
            output.WriteLine($"    text={System.Text.Encoding.ASCII.GetString(message.Body).TrimEnd('\0')}");
        }

        Assert.True(digestOk);
        Assert.NotEmpty(frame.Messages);
    }

    [Fact]
    public void The_join_request_parses_into_the_player_and_clan_that_sent_it()
    {
        // The capture is the whole evidence for the join-request layout: a
        // 149-byte body whose per-player block is longer than the roster
        // record's, so the names sit at the end rather than at 0x44.
        var work = Convert.FromHexString(JoinerFrame);
        var counter = FrameCryptoUtility.UnscrambleHeaderOnly(work);
        FrameCryptoUtility.RemoveChainInPlace(work, counter, SessionKey);
        var frame = MessageCodecUtility.DecodeFrame(work);

        var join = frame.Messages.Single(m => m.Type == UdpCommandConstants.JoinRequest);
        var profile = PlayerProfileRecordUtility.Parse(join.Body);

        Assert.NotNull(profile);
        Assert.Equal("MyPlayerName", profile.Name);
        Assert.Equal("test name", profile.ClanName);
    }
}