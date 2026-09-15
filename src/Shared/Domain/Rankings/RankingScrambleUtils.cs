namespace Mgo2Server.Shared.Domain.Rankings;

/// <summary>
/// The XOR obfuscation the client applies to a ranking HTTP response body before
/// parsing it.
/// <para>
/// The Rankings screens do not use the lobby protocol at all: they post to
/// <c>rank/mgogetrank.html</c> and parse the reply as a binary blob. Before a
/// single field is read the whole body is run through this transform, so a server
/// that answers in cleartext produces garbage in every column with no error
/// reported. XOR is its own inverse, so the server scrambles with the same
/// routine the client descrambles with.
/// </para>
/// <para>
/// Byte <c>i</c> is XORed with <c>key[((i / 4) % 5) + (i % 4)]</c>, which indexes
/// the whole eight-byte key and repeats with a period of twenty bytes.
/// </para>
/// </summary>
public static class RankingScrambleUtils
{
    /// <summary>
    /// Key read out of the client's scramble routine. Eight bytes, all of which
    /// the keystream reaches.
    /// </summary>
    private static readonly byte[] Key = [0x8B, 0x75, 0x2C, 0x90, 0x3A, 0x5E, 0x4D, 0xF1];

    /// <summary>Blocks of four bytes; the key offset advances every block.</summary>
    private const int BlockSize = 4;

    /// <summary>Number of blocks after which the key offset wraps.</summary>
    private const int BlocksPerCycle = 5;

    /// <summary>Applies the transform in place. Symmetric: applying it twice is the identity.</summary>
    /// <param name="body">Body to scramble.</param>
    public static void Apply(Span<byte> body)
    {
        for (var offset = 0; offset < body.Length; offset++)
        {
            body[offset] ^= Key[KeyIndex(offset)];
        }
    }

    /// <summary>Key byte position for a wire offset, kept separate so it can be asserted directly.</summary>
    /// <param name="offset">Offset of the byte in the body.</param>
    public static int KeyIndex(int offset) =>
        ((offset / BlockSize) % BlocksPerCycle) + (offset % BlockSize);
}
