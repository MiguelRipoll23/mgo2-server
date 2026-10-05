using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the two wire rules the recorded host writes and this server has to
/// match: a compressed run carries the header's compression marker and rebuilds
/// from an LZSS region, and an uncompressed run carries neither. The recorded
/// host writes the roster run, the roster repeat and the post-join burst
/// compressed and the two one-byte records that close the exchange plain, so a
/// frame built without the marker is not the same frame as one built with it.
/// </summary>
public sealed class CompressedFrameTests
{
    private static IReadOnlyList<UdpMessage> RosterRunShape() =>
    [
        FrameBuilderUtility.MessageOf(0x5001, ReadOnlySpan<byte>.Empty),
        FrameBuilderUtility.MessageOf(0x9001, new byte[75]),
        FrameBuilderUtility.MessageOf(0x1001, new byte[87]),
        FrameBuilderUtility.MessageOf(0x1001, new byte[7]),
    ];

    [Fact]
    public void ACompressedRunSetsTheMarkerAndRebuildsFromLzss()
    {
        const ushort counter = 0x0123;
        var frame = FrameBuilderUtility.BuildMessageFrame(counter, RosterRunShape(), compressed: true);

        var header = BinaryUtility.ReadUInt16LittleEndian(frame, 0);

        Assert.Equal(counter | UdpCommandConstants.CompressionMarker, header);
        Assert.NotEqual(0, header & UdpCommandConstants.CompressionMarker);

        var rebuilt = LzssUtility.Decompress(
            frame[UdpCommandConstants.HeaderSize..^UdpCommandConstants.TailSize]);

        var plain = MessageCodecUtility.SerializeMessages(RosterRunShape());
        Assert.Equal(plain, rebuilt);
    }

    [Fact]
    public void AnUncompressedRunLeavesTheMarkerClear()
    {
        const ushort counter = 0x0123;
        var frame = FrameBuilderUtility.BuildMessageFrame(counter, RosterRunShape());

        var header = BinaryUtility.ReadUInt16LittleEndian(frame, 0);

        Assert.Equal(counter, header);
        Assert.Equal(0, header & UdpCommandConstants.CompressionMarker);
    }

    /// <summary>
    /// The two paths have to differ only in the marker and the payload, never
    /// in what the frame says: a peer that decompresses the marker set must
    /// land on the same messages whether or not the sender compressed.
    /// </summary>
    [Fact]
    public void BothPathsRebuildToTheSameMessages()
    {
        var messages = RosterRunShape();
        var plain = MessageCodecUtility.SerializeMessages(messages);
        var compressedFrame = FrameBuilderUtility.BuildMessageFrame(0x0002, messages, compressed: true);
        var plainFrame = FrameBuilderUtility.BuildMessageFrame(0x0002, messages);

        var fromCompressed = LzssUtility.Decompress(
            compressedFrame[UdpCommandConstants.HeaderSize..^UdpCommandConstants.TailSize]);
        var fromPlain = plainFrame[UdpCommandConstants.HeaderSize..^UdpCommandConstants.TailSize];

        Assert.Equal(plain, fromCompressed);
        Assert.Equal(plain, fromPlain);
        Assert.True(compressedFrame.Length < plainFrame.Length);
    }

    /// <summary>
    /// A compressed frame is still shorter than the ceiling the peer applies
    /// when it rebuilds, or the peer would silently truncate the run and the
    /// joiner would wait for a roster that never finishes arriving.
    /// </summary>
    [Fact]
    public void TheRosterRunStaysInsideTheDecompressorsOutputCeiling()
    {
        var plain = MessageCodecUtility.SerializeMessages(RosterRunShape());

        Assert.True(plain.Length <= UdpCommandConstants.LzssMaximumOutput);
    }
}