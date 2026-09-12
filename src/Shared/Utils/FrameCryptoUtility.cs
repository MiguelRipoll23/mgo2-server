using System.Security.Cryptography;
using Mgo2Server.Shared.Constants;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Wire cipher of the peer-to-peer channel: a linear-congruential-generator
/// positioned header scramble, a little-endian 32-bit XOR chain with plaintext
/// feedback, and a self-verifying tail digest.
/// </summary>
public static class FrameCryptoUtility
{
    /// <summary>Scrambles a plaintext datagram into wire bytes.</summary>
    /// <param name="plain">
    /// Plaintext frame, including the zeroed tail region: the XOR chain and the
    /// digest both run over the full frame length.
    /// </param>
    /// <param name="counter">Frame counter.</param>
    /// <param name="key">Chain key.</param>
    /// <param name="digestKey">Tail digest key.</param>
    public static byte[] EncodeFrame(
        ReadOnlySpan<byte> plain,
        ushort counter,
        uint key,
        uint digestKey = UdpCryptoKeyConstants.TailDigestKey)
    {
        var output = plain.ToArray();
        ApplyExclusiveOrChain(output, counter, key, encrypt: true);
        ComputeTailDigest(output, digestKey);

        var (firstPosition, secondPosition, firstSwap, secondSwap) = ScramblePositions(output.Length);
        output[0] ^= output[firstPosition];
        output[1] ^= output[secondPosition];
        (output[0], output[firstSwap]) = (output[firstSwap], output[0]);
        (output[1], output[secondSwap]) = (output[secondSwap], output[1]);
        return output;
    }

    /// <summary>
    /// Removes the header scramble, verifies the tail digest and removes the
    /// XOR chain on a wire datagram.
    /// </summary>
    /// <param name="wire">Wire datagram, modified in place.</param>
    /// <param name="key">Chain key.</param>
    /// <param name="digestKey">Tail digest key.</param>
    /// <returns>The frame counter, or <c>null</c> when the digest gate fails.</returns>
    public static ushort? DecodeFrameInPlace(
        Span<byte> wire,
        uint key,
        uint digestKey = UdpCryptoKeyConstants.TailDigestKey)
    {
        var counter = UnscrambleHeaderOnly(wire);
        if (!VerifyTailDigest(wire, digestKey))
        {
            return null;
        }

        ApplyExclusiveOrChain(wire, counter, key, encrypt: false);
        return counter;
    }

    /// <summary>
    /// Undoes the header scramble only, leaving the XOR chain in place.
    /// </summary>
    /// <param name="raw">Wire datagram, modified in place.</param>
    /// <returns>The frame counter read from the unscrambled header.</returns>
    public static ushort UnscrambleHeaderOnly(Span<byte> raw)
    {
        var (firstPosition, secondPosition, firstSwap, secondSwap) = ScramblePositions(raw.Length);

        (raw[1], raw[secondSwap]) = (raw[secondSwap], raw[1]);
        (raw[0], raw[firstSwap]) = (raw[firstSwap], raw[0]);
        raw[1] ^= raw[secondPosition];
        raw[0] ^= raw[firstPosition];

        return BinaryUtility.ReadUInt16LittleEndian(raw, 0);
    }

    /// <summary>Removes the XOR chain in place on a header-unscrambled datagram.</summary>
    /// <param name="buffer">Datagram, modified in place.</param>
    /// <param name="counter">Frame counter.</param>
    /// <param name="key">Chain key.</param>
    public static void RemoveChainInPlace(Span<byte> buffer, ushort counter, uint key) =>
        ApplyExclusiveOrChain(buffer, counter, key, encrypt: false);

    /// <summary>
    /// Verifies the tail digest on a header-unscrambled, pre-chain datagram.
    /// The digest gate runs before the XOR chain, so pre-keyed frames verify
    /// with the bare constant and session-keyed frames with the session key
    /// exclusive-ORed with that constant.
    /// </summary>
    /// <param name="unscrambled">Header-unscrambled datagram.</param>
    /// <param name="digestKey">Digest key.</param>
    public static bool VerifyTailDigest(ReadOnlySpan<byte> unscrambled, uint digestKey)
    {
        var length = unscrambled.Length;
        if (length < UdpCommandConstants.TailSize + 1)
        {
            return false;
        }

        var tail = unscrambled[(length - UdpCommandConstants.TailSize)..];
        var expected = BuildTailDigest(unscrambled, length, digestKey);

        for (var index = 0; index < UdpCommandConstants.TailSize; index++)
        {
            if (tail[index] != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Computes and writes the self-verifying tail digest in place.</summary>
    /// <param name="decoded">Datagram carrying a zeroed tail region, modified in place.</param>
    /// <param name="digestKey">Digest key.</param>
    private static void ComputeTailDigest(Span<byte> decoded, uint digestKey)
    {
        var expected = BuildTailDigest(decoded, decoded.Length, digestKey);
        var tailOffset = decoded.Length - UdpCommandConstants.TailSize;
        for (var index = 0; index < UdpCommandConstants.TailSize; index++)
        {
            decoded[tailOffset + index] = expected[index];
        }
    }

    /// <summary>Builds the ten tail bytes expected for a datagram.</summary>
    /// <param name="datagram">Header-unscrambled datagram.</param>
    /// <param name="length">Total datagram length.</param>
    /// <param name="digestKey">Digest key.</param>
    private static byte[] BuildTailDigest(ReadOnlySpan<byte> datagram, int length, uint digestKey)
    {
        Span<byte> keyBytes = stackalloc byte[4];
        BinaryUtility.WriteUInt32LittleEndian(keyBytes, 0, digestKey);

        var firstDigest = MD5.HashData(BinaryUtility.Concatenate(keyBytes, datagram[..(length - UdpCommandConstants.TailSize)]));
        var secondDigest = MD5.HashData(BinaryUtility.Concatenate(keyBytes, firstDigest));

        var tail = new byte[UdpCommandConstants.TailSize];
        for (var index = 0; index < UdpCommandConstants.TailSize; index++)
        {
            tail[index] = (byte)(secondDigest[index] ^
                (index < 6 ? secondDigest[UdpCommandConstants.TailSize + index] : 0));
        }

        return tail;
    }

    /// <summary>
    /// XOR chain over little-endian 32-bit words starting at byte two and
    /// covering the datagram minus its header and tail, with plaintext
    /// feedback: the next seed is the current seed plus the previous word.
    /// </summary>
    /// <param name="buffer">Datagram, modified in place.</param>
    /// <param name="counter">Frame counter.</param>
    /// <param name="key">Chain key.</param>
    /// <param name="encrypt">True when encrypting, which feeds the plaintext word back into the chain.</param>
    private static void ApplyExclusiveOrChain(Span<byte> buffer, ushort counter, uint key, bool encrypt)
    {
        var seed = ComputeChainSeed(counter, key);
        uint previous = 0;
        var position = UdpCommandConstants.HeaderSize;
        var remaining = buffer.Length - UdpCommandConstants.HeaderSize - UdpCommandConstants.TailSize;

        while (remaining > 0)
        {
            seed = unchecked(seed + previous);
            var count = Math.Min(4, remaining);

            uint word = 0;
            for (var index = 0; index < count; index++)
            {
                word |= (uint)buffer[position + index] << (8 * index);
            }

            var result = word ^ seed;
            for (var index = 0; index < count; index++)
            {
                buffer[position + index] = (byte)((result >> (8 * index)) & 0xff);
            }

            previous = encrypt ? word : result;
            position += count;
            remaining -= count;
        }
    }

    /// <summary>Computes the initial XOR chain seed for a frame.</summary>
    /// <param name="counter">Frame counter.</param>
    /// <param name="key">Chain key.</param>
    private static uint ComputeChainSeed(ushort counter, uint key) =>
        unchecked(((uint)(counter & UdpCommandConstants.CounterMask) * UdpCryptoKeyConstants.LinearCongruentialMultiplier + 1) ^ key);

    /// <summary>Derives the four scramble positions from a datagram length.</summary>
    /// <param name="length">Datagram length.</param>
    private static (int FirstPosition, int SecondPosition, int FirstSwap, int SecondSwap) ScramblePositions(int length)
    {
        var generator = (uint)length;
        var first = NextRandom(generator);
        var second = NextRandom(first);
        var third = NextRandom(second);
        var fourth = NextRandom(third);

        var span = (uint)(length - 2);
        return (
            (int)(((first >> 16) % span) + 2),
            (int)(((second >> 16) % span) + 2),
            (int)((third >> 16) % (uint)length),
            (int)((fourth >> 16) % (uint)length));
    }

    /// <summary>Advances the linear congruential generator one step.</summary>
    /// <param name="value">Current state.</param>
    private static uint NextRandom(uint value) =>
        unchecked((value * UdpCryptoKeyConstants.LinearCongruentialMultiplier) + 1);
}
