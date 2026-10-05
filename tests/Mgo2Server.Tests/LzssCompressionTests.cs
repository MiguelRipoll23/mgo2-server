using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Round-trips the compressor against the decompressor that reads the game's
/// own frames, including the shapes that break a sliding-window scheme: a match
/// longer than its own distance, a ring that wraps, and input with nothing in
/// it to match.
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
        Assert.True(
            compressed.Length < original.Length / 2,
            $"no gain: {compressed.Length} of {original.Length}");
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
}