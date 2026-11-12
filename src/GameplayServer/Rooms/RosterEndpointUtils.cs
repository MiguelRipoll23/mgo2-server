namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The address half of an endpoint, which is what stays the same when a peer's
/// second channel opens on a port of its own.
/// </summary>
/// <remarks>
/// <b>A peer files under the endpoint its profile arrived from and reports its
/// hits from the gameplay channel's own.</b> The two are the same machine on a
/// different port, so the roster has to compare the address and not the whole
/// string: a lookup that compared the whole string named no shooter for every
/// hit a client wrote, and the kill went to slot zero — the host's own row.
/// </remarks>
public static class RosterEndpointUtils
{
    /// <summary>
    /// The address a formatted endpoint names, with its port removed.
    /// </summary>
    /// <param name="remoteAddress">Endpoint formatted as "address:port".</param>
    /// <returns>
    /// The address, or the whole string when it carries no port. An IPv6 endpoint
    /// keeps its brackets, so <c>[::1]:4000</c> reads as <c>[::1]</c>.
    /// </returns>
    public static string HostOf(string remoteAddress)
    {
        if (string.IsNullOrEmpty(remoteAddress))
        {
            return string.Empty;
        }

        var separator = remoteAddress.LastIndexOf(':');
        return separator <= 0 ? remoteAddress : remoteAddress[..separator];
    }
}
