using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// The frame counter in the peer-to-peer header, which is fifteen bits wide with
/// a compression flag above it.
/// </summary>
/// <remarks>
/// These pin arithmetic that a live round cannot exercise: a round's counter
/// stops at 18665 and never wraps, so the wrap and the bit-15 collision below
/// are both invisible unless they are stated here. The header layout they assume
/// is measured over 19018 datagrams in <c>docs/mgo2-game.pcapng</c>; see
/// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §2.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class FrameCounterUtilityTests
{
    [Fact]
    public void ValueStripsTheCompressionMarker()
    {
        // A compressed frame from the capture: header 0x8005 is sequence 5.
        Assert.Equal(5, FrameCounterUtility.Value(0x8005));
        Assert.Equal(2, FrameCounterUtility.Value(0x8002));
        Assert.Equal(4, FrameCounterUtility.Value(0x0004));
    }

    [Fact]
    public void ValueKeepsEveryBitBelowTheCompressionMarker()
    {
        // Bits 12, 13 and 14 are counter bits, not class or flag bits. Masking
        // them away is what made the header read as a uniformly random field.
        Assert.Equal(0x1000, FrameCounterUtility.Value(0x1000));
        Assert.Equal(0x3fff, FrameCounterUtility.Value(0x3fff));
        Assert.Equal(0x7fff, FrameCounterUtility.Value(0x7fff));
    }

    [Fact]
    public void NextNeverSetsTheCompressionMarker()
    {
        // Wrapping at sixteen bits would put the counter at 0x8000, which the
        // peer reads as "this frame is LZSS" on a frame that is not.
        Assert.Equal(0x7ffe, FrameCounterUtility.Next(0x7ffd));
        Assert.Equal(0x7fff, FrameCounterUtility.Next(0x7ffe));
        Assert.Equal(0x0000, FrameCounterUtility.Next(0x7fff));

        for (var counter = 0; counter <= 0x7fff; counter++)
        {
            Assert.Equal(0, FrameCounterUtility.Next((ushort)counter) & UdpCommandConstants.CompressionMarker);
        }
    }

    [Fact]
    public void IsNewerFollowsTheCounterForwardsAcrossTheWrap()
    {
        Assert.True(FrameCounterUtility.IsNewer(2, 1));
        Assert.True(FrameCounterUtility.IsNewer(0x7fff, 0x7ffe));

        // The case a plain `>` gets wrong: past 0x7fff the next value is 0, which
        // is not greater than what came before, so every later frame would read
        // as stale and nothing after the wrap would ever be acknowledged.
        Assert.True(FrameCounterUtility.IsNewer(0x0000, 0x7fff));
        Assert.True(FrameCounterUtility.IsNewer(0x0001, 0x7fff));
        Assert.True(FrameCounterUtility.IsNewer(0x0100, 0x7fff));
    }

    [Fact]
    public void IsNewerRejectsARepeatAndAnOlderSequence()
    {
        Assert.False(FrameCounterUtility.IsNewer(1, 1));
        Assert.False(FrameCounterUtility.IsNewer(0x7fff, 0x7fff));
        Assert.False(FrameCounterUtility.IsNewer(1, 2));
        Assert.False(FrameCounterUtility.IsNewer(0x7ffe, 0x0001));

        // 0x4000 frames ahead is as far away as a fifteen-bit counter can
        // distinguish, so it reads as the far past rather than the future.
        Assert.False(FrameCounterUtility.IsNewer(0x4000, 0x0000));
    }

    [Fact]
    public void AnAcknowledgementTypeCannotCarryMoreThanTwelveBits()
    {
        // What the server used to build: the reliable class ORed with a
        // fifteen-bit sequence. Above 4095 the sequence's own high bits collide
        // with the class, and the acknowledgement stops being an
        // acknowledgement -- 16384 comes out as 0x5000, the keep-alive, and
        // 16385 as 0x5001, the roster head.
        const ushort reliableClass = UdpCommandConstants.AcknowledgementClass;
        var unmasked = (ushort)(reliableClass | 0x4001);
        Assert.Equal(UdpCommandConstants.RosterHead, unmasked);
        Assert.Equal(UdpCommandConstants.KeepAlive, (ushort)(reliableClass | 0x4000));

        // Masked to what the type can carry, it stays inside the ack range.
        var masked = (ushort)(reliableClass |
            (0x4001 & UdpCommandConstants.AcknowledgementIdentifierMask));
        Assert.Equal(0x1001, masked);
        Assert.Equal(
            UdpCommandConstants.AcknowledgementClass,
            masked & 0xf000);
    }

    [Fact]
    public void ASequenceAndItsWrappedCounterNameTheSameAcknowledgement()
    {
        // The capture's only acknowledgements all read 0x1001, which names
        // sequences 1, 4097, 8193, 12289 and 16385 alike.
        var names = new HashSet<ushort>();
        for (var sequence = 1; sequence <= 18665; sequence += 4096)
        {
            names.Add((ushort)(UdpCommandConstants.AcknowledgementClass |
                (sequence & UdpCommandConstants.AcknowledgementIdentifierMask)));
        }

        Assert.Equal(0x1001, Assert.Single(names));
    }
}