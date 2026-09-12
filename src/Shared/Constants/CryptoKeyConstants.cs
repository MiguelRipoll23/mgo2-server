// The key material is fixed by the Metal Gear Online 2 protocol. The values must
// stay byte-for-byte as they are; changing one silently produces a wrong field
// instead of an error.

namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Cryptographic key material used by the Metal Gear Online 2 wire protocol.
/// </summary>
public static class CryptoKeyConstants
{
    /// <summary>Full-packet XOR key (0x5A7085AF).</summary>
    public const uint XorKey = 0x5a7085af;

    /// <summary>Four-byte XOR key applied to every byte of the packet on the wire.</summary>
    public static ReadOnlySpan<byte> XorKeyBytes =>
    [
        0x5a, 0x70, 0x85, 0xaf,
    ];

    /// <summary>
    /// First eight bytes of the check-session derived context. Pairs with
    /// <see cref="BlowfishAuthKeyTable"/>: both must switch together, because
    /// mixing one build's initialization vector with another build's key
    /// schedule silently produces a wrong field instead of an error.
    /// </summary>
    public static ReadOnlySpan<byte> SessionFieldInitializationVector =>
    [
        0x35, 0xd5, 0xc3, 0x8e, 0xd0, 0x11, 0x0e, 0xa8,
    ];

    /// <summary>HMAC-MD5 key for the packet checksum (header bytes 0-7 plus payload).</summary>
    public static ReadOnlySpan<byte> HmacMd5Key =>
    [
        0x5a, 0x37, 0x2f, 0x62, 0x69, 0x4a, 0x34, 0x36, 0x54, 0x7a, 0x47, 0x46,
        0x2d, 0x38, 0x79, 0x78,
    ];
}
