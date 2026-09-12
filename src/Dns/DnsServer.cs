using System.Net;
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

    /// <summary>Receives queries until the token is cancelled.</summary>
    /// <param name="cancellationToken">Token that stops the name server.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var socket = new UdpClient(new IPEndPoint(
            IPAddress.Parse(options.ListeningIpAddress),
            options.Port));

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
        var query = DnsMessageCodec.ParseQuery(received.Buffer);
        if (query is null)
        {
            return;
        }

        if (IsLocalDomain(query.Domain) &&
            query.QueryType is DnsMessageCodec.AddressRecordType or DnsMessageCodec.AnyRecordType)
        {
            logger.LogInformation(
                "Overriding {Domain} locally to {Address}",
                query.Domain,
                options.ResolvedIpAddress);

            var response = DnsMessageCodec.BuildAddressResponse(received.Buffer, options.ResolvedIpAddress);
            await socket.SendAsync(response, received.RemoteEndPoint, cancellationToken);
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

    /// <summary>
    /// Answers true only when the domain exactly matches a configured entry.
    /// Subdomains are not matched implicitly; they have to be listed.
    /// </summary>
    /// <param name="domain">Queried domain.</param>
    private bool IsLocalDomain(string domain) => localDomains.Contains(domain);

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
