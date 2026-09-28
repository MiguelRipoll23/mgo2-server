using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// Builds the host link a lobby pushes to a host whose game is reachable only on
/// the tailnet the players share, and decides the two facts that gate it: whether
/// the advertised address is a tailnet one, and what the link carries.
/// <para>
/// The code is four decimal digits derived from the deployment secret and the
/// character name, so a page that holds the same secret can fold a code back to a
/// character without the server storing anything. Four digits is a small space on
/// purpose — the link only ever overwrites one character's public address, and it
/// is resolved against the handful of characters hosting a game right now, so the
/// space is bounded by how many of those exist rather than by the account table.
/// </para>
/// <para>
/// The derivation is a wire contract with an external page, so it is pinned by
/// test vectors rather than left to the reader: HMAC-SHA256 with the UTF-8 secret
/// bytes as the key and the UTF-8 name bytes as the message, the first four digest
/// bytes read big-endian, that value modulo 10000, rendered with leading zeros to
/// four digits.
/// </para>
/// </summary>
public static class HostLinkUtils
{
    /// <summary>Number of digits a link code occupies.</summary>
    public const int CodeDigits = 4;

    /// <summary>Number of distinct codes a link code can take.</summary>
    public const int CodeSpace = 10000;

    /// <summary>First octet of the CGNAT range the tailnet addresses are carved from.</summary>
    private const int TailnetFirstOctet = 100;

    /// <summary>First second octet of that range, 100.64.0.0/10.</summary>
    private const int TailnetSecondOctetMinimum = 64;

    /// <summary>Last second octet of that range, 100.64.0.0/10.</summary>
    private const int TailnetSecondOctetMaximum = 127;

    /// <summary>
    /// The line the host is shown, addressed to the page that takes the code and
    /// records the address the game is reachable at from outside the tailnet.
    /// </summary>
    /// <param name="baseUrl">Base address of that page.</param>
    /// <param name="code">Code the link carries.</param>
    public static string BuildMessage(string baseUrl, string code) =>
        $"Go to {baseUrl.TrimEnd('/')}/{code} to make your game available to join from other players outside your network";

    /// <summary>
    /// Derives the code a character's link carries. Same secret and same name
    /// always give the same code, and a name of any case is a name of its own.
    /// </summary>
    /// <param name="secret">Deployment secret the code is keyed on.</param>
    /// <param name="characterName">Name of the character the link belongs to.</param>
    public static string BuildCode(string secret, string characterName)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var message = Encoding.UTF8.GetBytes(characterName);
        var digest = HMACSHA256.HashData(key, message);
        var value = BinaryPrimitives.ReadUInt32BigEndian(digest) % CodeSpace;
        return value.ToString("D" + CodeDigits, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Whether an advertised address is one of the tailnet addresses the range
    /// 100.64.0.0/10 covers. Only an address inside it makes the link worth
    /// sending: every other address is already reachable by the players.
    /// </summary>
    /// <param name="address">Advertised address, or <c>null</c> when none is set.</param>
    public static bool IsTailnetAddress(string? address)
    {
        if (!IPAddress.TryParse(address?.Trim(), out var parsed) ||
            parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var octets = parsed.GetAddressBytes();
        return octets[0] == TailnetFirstOctet &&
               octets[1] is >= TailnetSecondOctetMinimum and <= TailnetSecondOctetMaximum;
    }
}
