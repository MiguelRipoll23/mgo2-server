using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Mgo2Server.Dns.Options;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Dns;

/// <summary>
/// Answers the domains the server owns locally and forwards every other query
/// to the configured upstream name server.
/// </summary>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the name server.</param>
public sealed class DnsServer(DnsServerOptions options, ILogger<DnsServer> logger)
{
    private readonly HashSet<string> localDomains =
        [.. options.LocalResolvedDomains.Select(domain => domain.ToLowerInvariant())];

    /// <summary>
    /// Addresses assigned to this machine, so a query carrying one of them is
    /// recognised as local.
    /// </summary>
    private readonly HashSet<IPAddress> localAddresses = CollectLocalAddresses();

    /// <summary>Receives queries until the token is cancelled.</summary>
    /// <param name="cancellationToken">Token that stops the name server.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // Binding every interface is what lets one socket answer both the
        // loopback queries of a client on this machine and the queries of the
        // clients on the network.
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Any, options.Port));

        logger.LogInformation("Listening on port {Port}", options.Port);

        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await socket.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException exception)
            {
                logger.LogError(exception, "Listener socket error");
                continue;
            }

            await HandleQueryAsync(socket, received, cancellationToken);
        }
    }

    private async Task HandleQueryAsync(
        UdpClient socket,
        UdpReceiveResult received,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = DnsMessageCodec.ParseQuery(received.Buffer);
            if (query is null)
            {
                return;
            }

            if (IsLocalDomain(query.Domain) &&
                query.QueryType is DnsMessageCodec.AddressRecordType
                    or DnsMessageCodec.IPv6RecordType
                    or DnsMessageCodec.AnyRecordType)
            {
                // A client on this machine reaches the server through loopback,
                // so it is answered with the loopback address; every other
                // client has to be given the address this machine is reachable
                // on, which is the public address it was configured with.
                var resolvedAddress = IsLocalRequest(received.RemoteEndPoint.Address)
                    ? options.LocalResolvedIpAddress
                    : options.ResolvedIpAddress;

                // The question may be for an IPv6 address record even though
                // the deployment only has an IPv4 address to give; answering
                // it with the IPv6 twin of the resolved address keeps the
                // client from being handed the domain's upstream records.
                var queryType = query.QueryType is DnsMessageCodec.AnyRecordType
                    ? DnsMessageCodec.AddressRecordType
                    : query.QueryType;
                logger.LogInformation(
                    "Overriding {Domain} locally to {Address}",
                    query.Domain,
                    resolvedAddress);
                var response = DnsMessageCodec.BuildAddressResponse(
                    received.Buffer,
                    resolvedAddress,
                    queryType);
                if (response.Length > 0)
                {
                    await socket.SendAsync(response, received.RemoteEndPoint, cancellationToken);
                }

                return;
            }

            logger.LogInformation(
                "Resolving {Domain} via the alternative name server {Address}:{Port}",
                query.Domain,
                options.AlternativeNameServer,
                options.AlternativeNameServerPort);

            var forwarded = await ForwardAsync(received.Buffer, cancellationToken);
            if (forwarded is not null)
            {
                await socket.SendAsync(forwarded, received.RemoteEndPoint, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error handling DNS query");
        }
    }

    /// <summary>
    /// Answers true only when the domain exactly matches a configured entry.
    /// Subdomains are not matched implicitly; they have to be listed.
    /// </summary>
    /// <param name="domain">Queried domain.</param>
    private bool IsLocalDomain(string domain) => localDomains.Contains(domain);

    /// <summary>
    /// Answers true when the query came from this machine. Its source is then
    /// one of the addresses assigned to the machine, whether the client is the
    /// host itself or a container sharing its network.
    /// </summary>
    /// <param name="address">Source address of the query.</param>
    private bool IsLocalRequest(IPAddress address)
    {
        var candidate = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        return localAddresses.Contains(candidate) || IPAddress.IsLoopback(candidate);
    }

    /// <summary>Collects the addresses assigned to this machine.</summary>
    private static HashSet<IPAddress> CollectLocalAddresses()
    {
        var addresses = new HashSet<IPAddress> { IPAddress.Loopback, IPAddress.IPv6Loopback };

        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        addresses.Add(unicast.Address);
                    }
                }
                catch (NetworkInformationException)
                {
                    // The interface went down between enumeration and read.
                }
            }
        }
        catch (NetworkInformationException)
        {
            // No interface information is available; loopback matching still works.
        }

        return addresses;
    }

    private async Task<byte[]?> ForwardAsync(byte[] query, CancellationToken cancellationToken)
    {
        try
        {
            using var upstream = new UdpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(options.ForwardTimeoutMilliseconds));

            await upstream.SendAsync(
                query,
                new IPEndPoint(IPAddress.Parse(options.AlternativeNameServer), options.AlternativeNameServerPort),
                timeout.Token);

            var response = await upstream.ReceiveAsync(timeout.Token);
            return response.Buffer;
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            logger.LogError(exception, "Upstream query failed");
            return null;
        }
    }
}
