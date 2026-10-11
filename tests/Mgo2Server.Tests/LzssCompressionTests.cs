using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Round-trips the compressor against the decompressor that reads the game's
/// own frames, including the shapes that break a sliding-window scheme: a match
/// longer than its own distance, a ring that wraps, and input with nothing in
/// it to match — and the truncated stream the decompressor has to refuse.
/// </summary>
public sealed class LzssCompressionTests
{
    [Fact]
    public void RoundTripsEmptyInput()
    {
        var compressed = LzssUtility.Compress([]);

        Assert.Equal([], LzssUtility.Decompress(compressed));
    }

    [Fact]
    public void RoundTripsASingleByte()
    {
        var compressed = LzssUtility.Compress([0x42]);

        Assert.Equal([0x42], LzssUtility.Decompress(compressed));
    }

    [Fact]
    public void RoundTripsIncompressibleData()
    {
        var original = new byte[512];
        var state = 12345u;
        for (var index = 0; index < original.Length; index++)
        {
            state = (state * 1103515245u) + 12345u;
            original[index] = (byte)(state >> 16);
        }

        var compressed = LzssUtility.Compress(original);

        Assert.Equal(original, LzssUtility.Decompress(compressed));
    }

    /// <summary>
    /// A run of one byte repeated compresses to a single back-reference that
    /// then copies itself, so the length runs far past the distance and every
    /// step after the first reads the slot the step before wrote.
    /// </summary>
    [Fact]
    public void RoundTripsAMatchLongerThanItsDistance()
    {
        var original = new byte[600];
        Array.Fill(original, (byte)0xAB);

        var compressed = LzssUtility.Compress(original);

        Assert.Equal(original, LzssUtility.Decompress(compressed));
        Assert.True(
            compressed.Length < original.Length / 4,
            $"600 identical bytes took {compressed.Length} bytes");
    }

    /// <summary>
    /// The ring is 512 bytes, so a match cannot reach further back than that;
    /// input longer than the ring forces the window to wrap mid-stream. The
    /// length stays under the decompressor's 2048-byte output ceiling, which is
    /// the limit the peer applies and not something the compressor may exceed.
    /// </summary>
    [Fact]
    public void RoundTripsAcrossTheRingWrap()
    {
        var original = new byte[2000];
        for (var index = 0; index < original.Length; index++)
        {
            original[index] = (byte)(index % 251);
        }

        var compressed = LzssUtility.Compress(original);

        Assert.Equal(original, LzssUtility.Decompress(compressed));
    }

    /// <summary>
    /// A back-reference one ring further back than the distance it can name.
    /// The offset is nine bits of ring position, and the decoder reads a zero
    /// offset as the end-of-stream marker, so a match whose position wraps to
    /// zero truncates the peer's stream there. Content shorter than the 512-byte
    /// ring cannot express it; this payload is longer, with the repeat placed so
    /// that the wrap lands on the match.
    /// </summary>
    [Fact]
    public void RoundTripsAMatchWhoseOffsetWrapsToZero()
    {
        var original = new byte[600];
        var state = 4242u;
        for (var index = 0; index < original.Length; index++)
        {
            state = (state * 1103515245u) + 12345u;
            original[index] = (byte)(state >> 16);
        }

        // At output position 512 the write index is 513, so a match at distance
        // 1 is emitted as offset 513 - 1 = 512, which nine bits carry as zero.
        original[512] = original[511];
        original[513] = original[511];

        var compressed = LzssUtility.Compress(original);

        Assert.Equal(original, LzssUtility.Decompress(compressed));
    }

    /// <summary>
    /// A repetition far enough back that only a back-reference can reach it.
    /// The gap is 400 bytes because the ring is 512: a match further back than
    /// that is not representable at all, which is a property of the format
    /// rather than something the encoder can improve on.
    /// </summary>
    [Fact]
    public void RoundTripsDistantRepetition()
    {
        var original = new byte[800];
        var state = 99991u;
        for (var index = 0; index < 300; index++)
        {
            state = (state * 1103515245u) + 12345u;
            original[index] = (byte)(state >> 16);
        }

        Array.Copy(original, 0, original, 400, 300);

        var compressed = LzssUtility.Compress(original);

        Assert.Equal(original, LzssUtility.Decompress(compressed));

        // The same payload with that copy replaced by bytes nothing can
        // reference, so what the stream saves over it is the 400-byte-back
        // reference and nothing else. Two streams are compared rather than one
        // against a ratio of its input because the end-of-stream marker is a
        // fixed cost both of them pay, and it is large enough to move a ratio
        // bound this tight by the byte it is short of.
        var unreachable = (byte[])original.Clone();
        var other = 7717u;
        for (var index = 400; index < 700; index++)
        {
            other = (other * 1103515245u) + 12345u;
            unreachable[index] = (byte)(other >> 16);
        }

        var unreachableStream = LzssUtility.Compress(unreachable);

        Assert.True(
            compressed.Length + 250 < unreachableStream.Length,
            $"the 400-byte-back copy saved only {unreachableStream.Length - compressed.Length} bytes");
    }

    /// <summary>
    /// The frame content the roster run and the post-join burst actually carry:
    /// a short type/length header in front of mostly zero-filled bodies, which
    /// is the shape the compressor has to earn its keep on.
    /// </summary>
    [Fact]
    public void RoundTripsTheRosterRunShape()
    {
        var header = new List<byte> { 0xd0, 0x01, 0x01, 0x00 };
        header.AddRange(new byte[75]);
        header.AddRange(new byte[] { 0x01, 0x01, 0x57, 0x00 });
        header.AddRange(new byte[87]);
        var original = header.ToArray();

        var compressed = LzssUtility.Compress(original);

        Assert.Equal(original, LzssUtility.Decompress(compressed));
        Assert.True(
            compressed.Length < original.Length,
            $"roster run shape gained bytes: {compressed.Length} of {original.Length}");
    }

    /// <summary>
    /// A stream that stops before its marker is refused rather than accepted for
    /// what it had decoded so far. The marker is the reader's only clean exit,
    /// and the peer treats a stream that runs out as a failed decode, so
    /// returning the bytes in hand would hand the caller a roster that was never
    /// all sent — which is how a compressor that omitted the marker kept a
    /// round-trip test passing.
    /// </summary>
    [Fact]
    public void RefusesAStreamThatEndsBeforeTheMarker()
    {
        var original = new byte[600];
        var state = 20261011u;
        for (var index = 0; index < original.Length; index++)
        {
            state = (state * 1103515245u) + 12345u;
            original[index] = (byte)(state >> 16);
        }

        var compressed = LzssUtility.Compress(original);

        // The marker is the last thing the encoder writes, so cutting the tail
        // off leaves the tokens before it intact and removes part of the marker
        // itself: what is left is a stream that stops mid-stream, which is the
        // shape the peer refuses.
        foreach (var cut in (int[])[1, 2, 3])
        {
            Assert.Equal([], LzssUtility.Decompress(compressed[..^cut]));
        }

        Assert.Equal(original, LzssUtility.Decompress(compressed));
    }
}