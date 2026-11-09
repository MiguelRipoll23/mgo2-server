namespace Mgo2Server.Shared.Utils;

/// <summary>
/// The clan emblem block the client decodes, as the TCP emblem replies and uploads
/// carry it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The block is 768 bytes and only its first 565 are read.</b> The client's
/// decoder (<c>0xA9B3E8</c>) reads a four-byte <c>EMBD</c> magic, a flag byte, a
/// palette of sixteen colours, and then the pixels; the rest is padding, and both
/// emblems on the live server have all 203 of those bytes zero. That is what
/// confirms the image stops where <see cref="ReadBytes"/> says it does, and it is
/// also why a block is checked for its length and its magic alone: nothing reads
/// past <see cref="ReadBytes"/>, so refusing a block over its padding would only
/// reject an emblem the client draws.
/// </para>
/// <para>
/// <b>The pixels are 4-bit palette indices, two to a byte, 32 × 32 of them.</b>
/// Which half of a byte is the pixel on the left is not settled by the bytes — the
/// two readings are exact horizontal mirrors — so this type reads and writes the
/// nibbles in the order the block stores them and claims no order of its own. The
/// emblem's colours are its palette's; index zero is whatever the clan drew as its
/// background and is not a transparency, because guessing that would punch a hole
/// in whichever clan used it as a colour.
/// </para>
/// <para>
/// <b>This block never travels on the peer-to-peer channel.</b> It is fetched by
/// clan id over TCP (<c>0x4b48</c>/<c>0x4b4a</c>/<c>0x4b4c</c>, answered by
/// <c>ClanEmblemHandlers</c>); what a roster entry carries is the clan id that
/// selects it (<see cref="PlayerProfileRecordLayout.ClanIdentifierOffset"/>).
/// </para>
/// </remarks>
public static class ClanEmblemBlockUtils
{
    /// <summary>Bytes one emblem occupies on the wire, padding included.</summary>
    public const int BlockSize = 768;

    /// <summary>Length of the magic at the front of the block.</summary>
    public const int MagicLength = 4;

    /// <summary>Offset of the flag byte behind the magic, which the client reads as a sign.</summary>
    public const int FlagOffset = 4;

    /// <summary>Offset of the sixteen-colour palette.</summary>
    public const int PaletteOffset = 5;

    /// <summary>Number of colours a block's palette holds.</summary>
    public const int PaletteEntries = 16;

    /// <summary>Bytes one palette entry occupies: red, green and blue.</summary>
    public const int PaletteEntryLength = 3;

    /// <summary>Offset of the packed pixel indices.</summary>
    public const int PixelsOffset = PaletteOffset + (PaletteEntries * PaletteEntryLength);

    /// <summary>Width of the emblem, in pixels.</summary>
    public const int Width = 32;

    /// <summary>Height of the emblem, in pixels.</summary>
    public const int Height = 32;

    /// <summary>Pixels packed into one byte: two four-bit indices.</summary>
    public const int PixelsPerByte = 2;

    /// <summary>Bytes the packed pixels occupy.</summary>
    public const int PixelsLength = Width * Height / PixelsPerByte;

    /// <summary>Bytes of the block the client reads, padding excluded.</summary>
    public const int ReadBytes = PixelsOffset + PixelsLength;

    /// <summary>The four bytes that open a block the client will decode.</summary>
    /// <returns>The magic, in wire order.</returns>
    public static ReadOnlySpan<byte> Magic => "EMBD"u8;

    /// <summary>
    /// Whether a stored blob is an emblem block: the length and the magic, which is
    /// all the client's own reader checks.
    /// </summary>
    /// <param name="block">Bytes a clan's emblem column holds.</param>
    /// <returns><c>true</c> when the client would decode the blob as an emblem.</returns>
    public static bool IsBlock(ReadOnlySpan<byte> block) =>
        block.Length >= BlockSize && block[..MagicLength].SequenceEqual(Magic);

    /// <summary>
    /// A block the client decodes as an empty emblem: the magic, a zero palette and
    /// every pixel on colour zero.
    /// </summary>
    /// <returns>The 768-byte block.</returns>
    public static byte[] Blank()
    {
        var block = new byte[BlockSize];
        Magic.CopyTo(block);
        return block;
    }
}
