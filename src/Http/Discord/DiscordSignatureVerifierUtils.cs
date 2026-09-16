using System.Text;
using NSec.Cryptography;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Verifies the signature Discord puts on every interaction it delivers. The
/// interactions endpoint is reachable by anyone, so nothing is answered before
/// the request is proved to come from Discord.
/// </summary>
/// <remarks>
/// Discord signs the timestamp header and the raw body with Ed25519, and the
/// public key of the application is the one shown in its developer portal.
/// </remarks>
public static class DiscordSignatureVerifierUtils
{
    /// <summary>Length of an Ed25519 public key in bytes.</summary>
    public const int PublicKeyLength = 32;

    /// <summary>Length of an Ed25519 signature in bytes.</summary>
    public const int SignatureLength = 64;

    /// <summary>Verifies the signature of an interaction request.</summary>
    /// <param name="body">Raw body of the request, exactly as it arrived.</param>
    /// <param name="signature">Hex-encoded value of the X-Signature-Ed25519 header.</param>
    /// <param name="timestamp">Value of the X-Signature-Timestamp header.</param>
    /// <param name="publicKey">Hex-encoded public key of the application.</param>
    /// <returns>Whether the request carries a valid signature of the body.</returns>
    public static bool Verify(byte[] body, string? signature, string? timestamp, string? publicKey)
    {
        if (string.IsNullOrEmpty(signature) ||
            string.IsNullOrEmpty(timestamp) ||
            string.IsNullOrEmpty(publicKey) ||
            !TryDecodeHex(publicKey, PublicKeyLength, out var keyBytes) ||
            !TryDecodeHex(signature, SignatureLength, out var signatureBytes))
        {
            return false;
        }

        var timestampBytes = Encoding.UTF8.GetBytes(timestamp);
        var message = new byte[timestampBytes.Length + body.Length];
        timestampBytes.CopyTo(message, 0);
        body.CopyTo(message, timestampBytes.Length);

        var algorithm = SignatureAlgorithm.Ed25519;
        if (!PublicKey.TryImport(algorithm, keyBytes, KeyBlobFormat.RawPublicKey, out var key) || key is null)
        {
            return false;
        }

        return algorithm.Verify(key, message, signatureBytes);
    }

    /// <summary>Decodes a hex string of an expected length.</summary>
    /// <param name="value">Hex string to decode.</param>
    /// <param name="expectedLength">Number of bytes the value must hold.</param>
    /// <param name="decoded">Bytes the value was decoded into.</param>
    private static bool TryDecodeHex(string value, int expectedLength, out byte[] decoded)
    {
        decoded = [];

        if (value.Length != expectedLength * 2)
        {
            return false;
        }

        try
        {
            decoded = Convert.FromHexString(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
