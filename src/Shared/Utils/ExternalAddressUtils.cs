using System.Net;
using System.Net.Sockets;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// The external addresses a host is reachable at: how one is recognised as a
/// tailnet address, and which one a stored row keeps.
/// </summary>
public static class ExternalAddressUtils
{
    /// <summary>Longest textual form of an address an IPv6 literal can take.</summary>
    public const int MaximumLength = 45;

    /// <summary>First octet of the CGNAT range the tailnet addresses are carved from.</summary>
    private const int TailnetFirstOctet = 100;

    /// <summary>First second octet of that range, 100.64.0.0/10.</summary>
    private const int TailnetSecondOctetMinimum = 64;

    /// <summary>Last second octet of that range, 100.64.0.0/10.</summary>
    private const int TailnetSecondOctetMaximum = 127;

    /// <summary>
    /// Whether an address is one of the tailnet addresses the range 100.64.0.0/10
    /// covers. Such an address is the peer's own view of itself and not the one
    /// other players reach it at, so it never replaces a stored external address:
    /// a player who signs in over the tailnet keeps presenting an address only
    /// their tailnet can dial.
    /// </summary>
    /// <param name="address">Address to read, or <c>null</c> when none is set.</param>
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

    /// <summary>
    /// The address a host settings row keeps, given the one the player just
    /// presented and the one the row already holds.
    /// </summary>
    /// <remarks>
    /// A player that presents nothing, something that is not an address, or a
    /// tailnet address leaves the row as it was; anything else is the fresher
    /// view of the same host and replaces it.
    /// </remarks>
    /// <param name="presented">Address presented just now, or <c>null</c>.</param>
    /// <param name="stored">Address the row holds, or <c>null</c>.</param>
    /// <returns>The address to store.</returns>
    public static string? Resolve(string? presented, string? stored)
    {
        var trimmed = presented?.Trim();
        if (string.IsNullOrEmpty(trimmed) ||
            !IPAddress.TryParse(trimmed, out var parsed) ||
            IsTailnetAddress(trimmed))
        {
            return stored;
        }

        return parsed.ToString();
    }
}
