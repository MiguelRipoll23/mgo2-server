namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Packs IPv4 endpoints into the byte forms the peer-to-peer channel carries:
/// six bytes per pair, and the twelve-byte block a roster echoes.
/// </summary>
public static class EndpointDataUtils
{
    /// <summary>Number of endpoint pairs in the roster block.</summary>
    private const int RosterPairCount = 2;

    /// <summary>
    /// Packs the two IPv4 endpoint pairs as the twelve bytes carried at roster
    /// body offsets <c>0x13</c>–<c>0x1e</c>. An incomplete or invalid pair set
    /// produces no block rather than a fabricated endpoint.
    /// </summary>
    /// <param name="pairs">Public and private endpoint pairs from a handshake.</param>
    /// <returns>Twelve bytes when both pairs are valid; otherwise an empty array.</returns>
    public static byte[] EndpointData(IReadOnlyList<HandshakeEndpointPair> pairs)
    {
        if (pairs.Count < RosterPairCount)
        {
            return [];
        }

        var first = EndpointPairBytes(pairs[0].Address, pairs[0].Port);
        var second = EndpointPairBytes(pairs[1].Address, pairs[1].Port);
        return first.Length == 6 && second.Length == 6
            ? [.. first, .. second]
            : [];
    }

    /// <summary>
    /// Builds the six bytes of an IPv4 endpoint pair: four address bytes in
    /// network order followed by a little-endian port.
    /// </summary>
    /// <param name="address">IPv4 address from a handshake endpoint pair.</param>
    /// <param name="port">Port from a handshake endpoint pair.</param>
    /// <returns>Six endpoint bytes, or an empty array for an invalid endpoint.</returns>
    public static byte[] EndpointPairBytes(string address, int port)
    {
        var addressBytes = IpAddressToBytes(address);
        if (addressBytes is null || port is < 0 or > ushort.MaxValue)
        {
            return [];
        }

        return
        [
            .. addressBytes,
            (byte)(port & 0xff),
            (byte)(port >> 8),
        ];
    }

    /// <summary>Converts a dotted IPv4 address into its four bytes.</summary>
    /// <param name="address">Dotted address text.</param>
    /// <returns>The four address bytes, or <c>null</c> when the text is not a valid IPv4 address.</returns>
    public static byte[]? IpAddressToBytes(string address)
    {
        var parts = address.Split('.');
        if (parts.Length != 4)
        {
            return null;
        }

        var bytes = new byte[4];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!byte.TryParse(parts[index], out bytes[index]))
            {
                return null;
            }
        }

        return bytes;
    }
}
