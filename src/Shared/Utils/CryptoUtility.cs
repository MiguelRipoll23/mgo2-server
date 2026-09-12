using System.Buffers.Binary;
using System.Security.Cryptography;
using Mgo2Server.Shared.Constants;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Cipher primitives of the TCP protocol: full-packet XOR obfuscation, the
/// HMAC-MD5 packet checksum, the session-field derivation and the custom
/// eight-round Blowfish variant the protocol uses.
/// </summary>
public static class CryptoUtility
{
    /// <summary>Size of one Blowfish block in bytes.</summary>
    private const int BlowfishBlockSize = PacketConstants.BlowfishBlockSize;

    /// <summary>Byte offset of the first sub-key entry of a Blowfish key table.</summary>
    private const int BlowfishInitialSubKeyOffset = 0x44;

    /// <summary>Byte offset of the first substitution box of a Blowfish key table.</summary>
    private const int BlowfishFirstBoxOffset = 0x48;

    /// <summary>Distance in bytes between two substitution boxes.</summary>
    private const int BlowfishBoxStride = 0x400;

    /// <summary>Number of rounds the Blowfish variant runs.</summary>
    private const int BlowfishRounds = 8;

    /// <summary>Length of the derived session field in bytes.</summary>
    private const int SessionFieldLength = 16;

    /// <summary>Length of the login token, in characters and bytes.</summary>
    private const int SessionFieldTokenLength = 16;

    /// <summary>Applies the full-packet XOR key to <paramref name="buffer"/>, in place.</summary>
    /// <param name="buffer">Buffer to obfuscate.</param>
    public static void ApplyExclusiveOr(Span<byte> buffer) =>
        ApplyExclusiveOr(buffer, CryptoKeyConstants.XorKeyBytes);

    /// <summary>Applies a XOR key to <paramref name="buffer"/>, in place.</summary>
    /// <param name="buffer">Buffer to obfuscate.</param>
    /// <param name="key">Key bytes, cycled across the buffer.</param>
    public static void ApplyExclusiveOr(Span<byte> buffer, ReadOnlySpan<byte> key)
    {
        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] ^= key[index & (key.Length - 1)];
        }
    }

    /// <summary>
    /// Computes the HMAC-MD5 checksum of the packet header prefix concatenated
    /// with the payload.
    /// </summary>
    /// <param name="headerBytes">Header bytes the checksum covers.</param>
    /// <param name="payload">Packet payload the checksum covers.</param>
    public static byte[] ComputeHmacMd5(ReadOnlySpan<byte> headerBytes, ReadOnlySpan<byte> payload)
    {
        var message = BinaryUtility.Concatenate(headerBytes, payload);
        return HMACMD5.HashData(CryptoKeyConstants.HmacMd5Key, message);
    }

    /// <summary>Encrypts a packet payload with the packet key table.</summary>
    /// <param name="payload">Payload, modified in place. Its length must be a multiple of eight.</param>
    public static void EncryptPacketPayload(Span<byte> payload) =>
        BlowfishEncrypt(payload, BlowfishPacketKeyTable.Bytes);

    /// <summary>Decrypts a packet payload with the packet key table.</summary>
    /// <param name="payload">Payload, modified in place. Its length must be a multiple of eight.</param>
    public static void DecryptPacketPayload(Span<byte> payload) =>
        BlowfishDecrypt(payload, BlowfishPacketKeyTable.Bytes);

    /// <summary>Encrypts an authentication payload with the authentication key table.</summary>
    /// <param name="payload">Payload, modified in place. Its length must be a multiple of eight.</param>
    public static void EncryptAuthenticationPayload(Span<byte> payload) =>
        BlowfishEncrypt(payload, BlowfishAuthKeyTable.Bytes);

    /// <summary>
    /// Derives the sixteen-byte session field the client presents in its
    /// check-session packet. The client keeps the issued login token as its
    /// sixteen characters, derives this value and presents it, so the server
    /// applies the same transform to the token it issued and stores the result
    /// for a plain lookup. Each eight-byte block is decrypted with the
    /// authentication key table and then exclusive-ORed with the previous
    /// plaintext block, starting from the initialization vector.
    /// </summary>
    /// <param name="token">Login token issued to the client.</param>
    public static byte[] DeriveSessionField(string token)
    {
        var plain = new byte[SessionFieldTokenLength];
        for (var index = 0; index < SessionFieldTokenLength; index++)
        {
            plain[index] = (byte)token[index];
        }

        var output = new byte[SessionFieldLength];
        var previous = CryptoKeyConstants.SessionFieldInitializationVector.ToArray();

        for (var offset = 0; offset < plain.Length; offset += BlowfishBlockSize)
        {
            var block = plain.AsSpan(offset, BlowfishBlockSize);
            var transformed = block.ToArray();
            BlowfishDecrypt(transformed, BlowfishAuthKeyTable.Bytes);

            for (var index = 0; index < BlowfishBlockSize; index++)
            {
                output[offset + index] = (byte)(transformed[index] ^ previous[index]);
            }

            // The chain carries the plaintext block forward, not the block just written.
            previous = block.ToArray();
        }

        return output;
    }

    /// <summary>Derives the session field in the hexadecimal form stored on the session row.</summary>
    /// <param name="token">Login token issued to the client.</param>
    public static string StoreSessionField(string token) =>
        Convert.ToHexStringLower(DeriveSessionField(token));

    /// <summary>Renders a field taken off the wire into the stored hexadecimal form.</summary>
    /// <param name="field">Field bytes received from the client.</param>
    public static string StoredSessionFieldFromWire(ReadOnlySpan<byte> field) =>
        Convert.ToHexStringLower(field[..SessionFieldLength]);

    /// <summary>Encrypts <paramref name="data"/> with the custom eight-round Blowfish variant.</summary>
    /// <param name="data">Data, modified in place. Its length must be a multiple of eight.</param>
    /// <param name="keyTable">Byte table holding the sub-keys and substitution boxes.</param>
    public static void BlowfishEncrypt(Span<byte> data, ReadOnlySpan<byte> keyTable)
    {
        var blocks = data.Length / BlowfishBlockSize;
        for (var blockIndex = 0; blockIndex < blocks; blockIndex++)
        {
            var offset = blockIndex * BlowfishBlockSize;
            var first = ReadInt32BigEndian(data, offset + 4);
            var second = ReadInt32BigEndian(data, offset);

            second ^= ReadInt32BigEndian(keyTable, 0x0);

            for (var round = BlowfishRounds - 1; round >= 0; round--)
            {
                first ^= ReadInt32BigEndian(keyTable, 0x3c - (round * 8));
                first ^= ComputeBlowfishRound(keyTable, second);

                second ^= ReadInt32BigEndian(keyTable, 0x40 - (round * 8));
                second ^= ComputeBlowfishRound(keyTable, first);
            }

            first ^= ReadInt32BigEndian(keyTable, BlowfishInitialSubKeyOffset);

            WriteInt32BigEndian(data, offset, first);
            WriteInt32BigEndian(data, offset + 4, second);
        }
    }

    /// <summary>Decrypts <paramref name="data"/> with the custom eight-round Blowfish variant.</summary>
    /// <param name="data">Data, modified in place. Its length must be a multiple of eight.</param>
    /// <param name="keyTable">Byte table holding the sub-keys and substitution boxes.</param>
    public static void BlowfishDecrypt(Span<byte> data, ReadOnlySpan<byte> keyTable)
    {
        var blocks = data.Length / BlowfishBlockSize;
        for (var blockIndex = 0; blockIndex < blocks; blockIndex++)
        {
            var offset = blockIndex * BlowfishBlockSize;
            var first = ReadInt32BigEndian(data, offset + 4);
            var second = ReadInt32BigEndian(data, offset);

            second ^= ReadInt32BigEndian(keyTable, BlowfishInitialSubKeyOffset);

            for (var round = 0; round < BlowfishRounds; round++)
            {
                first ^= ReadInt32BigEndian(keyTable, 0x40 - (round * 8));
                first ^= ComputeBlowfishRound(keyTable, second);

                second ^= ReadInt32BigEndian(keyTable, 0x3c - (round * 8));
                second ^= ComputeBlowfishRound(keyTable, first);
            }

            first ^= ReadInt32BigEndian(keyTable, 0x0);

            WriteInt32BigEndian(data, offset, first);
            WriteInt32BigEndian(data, offset + 4, second);
        }
    }

    /// <summary>Pads <paramref name="data"/> to Blowfish block alignment.</summary>
    /// <param name="data">Data to pad.</param>
    public static byte[] PadToBlowfishBlock(ReadOnlySpan<byte> data)
    {
        var remainder = data.Length % BlowfishBlockSize;
        if (remainder == 0)
        {
            return data.ToArray();
        }

        var padded = new byte[data.Length + (BlowfishBlockSize - remainder)];
        data.CopyTo(padded);
        return padded;
    }

    /// <summary>
    /// One Blowfish round: four substitution-box lookups combined into a
    /// single 32-bit result.
    /// </summary>
    /// <param name="keyTable">Key table holding the substitution boxes.</param>
    /// <param name="value">Round input.</param>
    private static int ComputeBlowfishRound(ReadOnlySpan<byte> keyTable, int value)
    {
        var indexA = (value >>> 22) & 0x3fc;
        var indexB = (value >>> 14) & 0x3fc;
        var indexC = (value >>> 6) & 0x3fc;
        var indexD = ((value << 2) | (value >>> 30)) & 0x3fc;

        var first = ReadInt32BigEndian(keyTable, indexA + BlowfishFirstBoxOffset);
        var second = ReadInt32BigEndian(keyTable, indexB + BlowfishFirstBoxOffset + BlowfishBoxStride);
        var third = ReadInt32BigEndian(keyTable, indexC + BlowfishFirstBoxOffset + (2 * BlowfishBoxStride));
        var fourth = ReadInt32BigEndian(keyTable, indexD + BlowfishFirstBoxOffset + (3 * BlowfishBoxStride));

        return unchecked(((first + second) ^ third) + fourth);
    }

    /// <summary>Reads a big-endian signed 32-bit value.</summary>
    /// <param name="buffer">Source buffer.</param>
    /// <param name="offset">Byte offset of the value.</param>
    private static int ReadInt32BigEndian(ReadOnlySpan<byte> buffer, int offset) =>
        BinaryPrimitives.ReadInt32BigEndian(buffer[offset..]);

    /// <summary>Writes a big-endian signed 32-bit value.</summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="offset">Byte offset of the value.</param>
    /// <param name="value">Value to write.</param>
    private static void WriteInt32BigEndian(Span<byte> buffer, int offset, int value) =>
        BinaryPrimitives.WriteInt32BigEndian(buffer[offset..], value);
}
