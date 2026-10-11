using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Mgo2Server.GameplayServer.Identity;

/// <summary>
/// Reads this machine's private IPv4 address from its network interfaces.
/// </summary>
/// <remarks>
/// The dedicated host advertises a private endpoint beside its public one, as
/// the captured hosts do: game1's host entry carries its public address first
/// and its LAN address second. The address is detected rather than configured
/// so a host on a new network needs no setting, and nothing is invented when
/// none can be detected -- a host with no second address says so rather than
/// sending the one it already advertises twice.
/// </remarks>
public static class LocalAddressUtils
{
    /// <summary>
    /// Reports the machine's private IPv4 address.
    /// </summary>
    /// <remarks>
    /// The interface the default route leaves by is asked first, because it is
    /// the one peers on the network reach and it is what the install scripts
    /// detect with <c>ip route get</c>. A machine with no such route falls back
    /// to the first private address on an up, non-loopback interface.
    /// </remarks>
    /// <returns>The address as dotted text, or <c>null</c> when none is found.</returns>
    /// <param name="excluding">Address to refuse, or <c>null</c> to accept any.</param>
    /// <returns>The address as dotted text, or <c>null</c> when none is found.</returns>
    public static string? TryGetPrivateAddress(string? excluding = null)
    {
        // The route is the answer when there is one, and it is not replaced by
        // another interface's address just because it was ruled out. That
        // fallback is what put a container bridge in a roster: a host whose
        // routed address is the one it already advertises has no second
        // address, and the next private address on any up interface is a
        // bridge or a virtual NIC that no peer on the network can reach.
        var routed = RoutedAddress();
        return routed is not null ? UnruledOut(routed, excluding) : FirstPrivateInterfaceAddress(excluding);
    }

    /// <summary>
    /// An address, unless the caller has ruled out exactly that one.
    /// </summary>
    /// <remarks>
    /// The exclusion is what keeps a host from answering with the address it
    /// already advertises as its public one. The captured hosts all carry two
    /// <em>different</em> endpoints -- game1's host entry holds
    /// <c>99.66.131.177</c> and then <c>10.104.10.28</c> -- so a machine whose
    /// only reachable address is the one already advertised has no private
    /// address to offer, and answering with it again puts the same endpoint in
    /// both slots of the handshake and of the roster entry. Answering with
    /// nothing lets the caller say so rather than send it twice.
    /// </remarks>
    /// <param name="address">Address found, or <c>null</c>.</param>
    /// <param name="excluding">Address the caller will not accept.</param>
    /// <returns>The address when it is usable, otherwise <c>null</c>.</returns>
    private static string? UnruledOut(string? address, string? excluding) =>
        address is not null && !string.Equals(address, excluding, StringComparison.Ordinal)
            ? address
            : null;

    /// <summary>
    /// Reports the source address the default route would use, without sending
    /// anything: a connected UDP socket resolves the route when it connects.
    /// </summary>
    /// <returns>The address as dotted text, or <c>null</c> when there is no route.</returns>
    private static string? RoutedAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(new IPEndPoint(IPAddress.Parse("1.1.1.1"), 53));
            return socket.LocalEndPoint is IPEndPoint endpoint ? endpoint.Address.ToString() : null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static string? FirstPrivateInterfaceAddress(string? excluding)
    {
        string? candidate = null;

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
            {
                var address = unicast.Address;
                if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                {
                    continue;
                }

                var text = address.ToString();
                if (text.StartsWith("169.254", StringComparison.Ordinal) ||
                    string.Equals(text, excluding, StringComparison.Ordinal))
                {
                    continue;
                }

                // A private range is what the captured hosts advertise. A
                // public- or carrier-range address on a virtual interface is
                // kept only as a last resort, so a bridged or tunneled
                // interface does not win over the LAN one.
                if (IsPrivateAddress(address))
                {
                    return text;
                }

                candidate ??= text;
            }
        }

        return candidate;
    }

    /// <summary>
    /// Whether an address falls in one of the private IPv4 ranges.
    /// </summary>
    /// <param name="address">Address to test.</param>
    /// <returns><c>true</c> for <c>10/8</c>, <c>172.16/12</c> and <c>192.168/16</c>.</returns>
    private static bool IsPrivateAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
            (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168);
    }
}
