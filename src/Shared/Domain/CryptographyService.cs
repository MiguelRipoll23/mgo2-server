using System.Security.Cryptography;
using System.Text;

namespace Mgo2Server.Shared.Domain;

/// <summary>
/// Hashing used for account passwords. The client sends the hash, so the server
/// must reproduce the digest the client sent exactly.
/// </summary>
public sealed class CryptographyService
{
    /// <summary>Computes the lowercase hexadecimal MD5 digest of <paramref name="text"/>.</summary>
    /// <param name="text">Text to hash, encoded as UTF-8.</param>
    public string ComputeMd5Hex(string text) =>
        Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(text)));
}
