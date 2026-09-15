using System.Net;
using System.Net.Sockets;
using Mgo2Server.Stun.Options;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Stun;

/// <summary>
/// Answers the port check of the console, the NAT discovery it runs before it
/// will let a player into a lobby.
/// </summary>
/// <remarks>
/// <para>
/// The check is not a single binding request: the console asks the responder to
/// answer from a different address and port and infers its NAT type from which
/// answers arrive. RFC 3489 section 8.1 therefore asks for four sockets, the two
/// served ports of two addresses, and section 9.2 fixes which socket answers
/// which request: everything is relative to where the request arrived, and
/// CHANGED-ADDRESS always names the responder's <em>other</em> address and port,
/// never the route of the answer being written.
/// </para>
/// <para>
/// With one address only the port can change. The responder then answers a
/// change-address request from the alternate port of the same address, which is
/// the one thing it can do; a restricted-cone NAT filters on address only, so such
/// an answer reaches it and its owner is told they are a full-cone peer and will
/// try direct connections only a real full-cone peer accepts. Two addresses are
/// what makes peer to peer work; STUN_SECONDARY_ADDRESS is how one is supplied.
/// </para>
/// <para>
/// The console identifies the responder by the address and the port an answer came
/// from, so anything that rewrites the source of a reply, or that hands a request
/// over from a port of its own, makes the mapped address the console is given
/// wrong. Answering from a second address means owning both, which only works where
/// the responder has addresses of the machine to itself.
/// </para>
/// </remarks>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the responder.</param>
public sealed class StunServer(StunServerOptions options, ILogger<StunServer> logger)
{
    /// <summary>Receives requests until the token is cancelled.</summary>
    /// <param name="cancellationToken">Token that stops the responder.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var layout = StunSocketLayout.From(options);
        var sockets = OpenSockets(layout);

        logger.LogInformation(
            "Listening on {EndPoints}",
            string.Join(", ", layout.ListeningEndPoints.Select(endPoint => endPoint.ToString())));

        if (!layout.ServesTwoAddresses)
        {
            logger.LogWarning(
                "Serving one address only: a request to change the address is answered from port {AlternatePort} " +
                "of the same address, which cannot tell a full-cone NAT from a restricted-cone one. " +
                "Give the responder a second address of its own (STUN_SECONDARY_ADDRESS) for peer to peer to work",
                layout.AlternatePort);

            if (options.SecondaryAddress is not null)
            {
                logger.LogWarning(
                    "STUN_SECONDARY_ADDRESS {SecondaryAddress} is not a second address to serve from and is ignored",
                    options.SecondaryAddress);
            }
        }

        try
        {
            await Task.WhenAll(layout.ListeningEndPoints.Select(endPoint =>
                ReceiveAsync(layout, endPoint, sockets, cancellationToken)));
        }
        finally
        {
            foreach (var socket in sockets.Values)
            {
                socket.Dispose();
            }
        }
    }

    /// <summary>Opens the sockets of a layout, one per address and port served.</summary>
    /// <param name="layout">Addresses and ports to serve.</param>
    /// <exception cref="SocketException">An address or port cannot be bound.</exception>
    private static Dictionary<IPEndPoint, UdpClient> OpenSockets(StunSocketLayout layout)
    {
        var sockets = new Dictionary<IPEndPoint, UdpClient>();

        foreach (var endPoint in layout.ListeningEndPoints)
        {
            // A wildcard address cannot be bound as itself, so the port is bound
            // on every interface and the configured address is what the answers
            // name.
            var bindAddress = layout.ServesTwoAddresses ? endPoint.Address : IPAddress.Any;
            sockets[endPoint] = new UdpClient(new IPEndPoint(bindAddress, endPoint.Port));
        }

        return sockets;
    }

    /// <summary>Receives datagrams on one socket until the token is cancelled.</summary>
    /// <param name="layout">Addresses and ports being served.</param>
    /// <param name="listeningEndPoint">Address and port this socket serves.</param>
    /// <param name="sockets">Every socket of the layout, keyed by what it serves.</param>
    /// <param name="cancellationToken">Token that stops the responder.</param>
    private async Task ReceiveAsync(
        StunSocketLayout layout,
        IPEndPoint listeningEndPoint,
        IReadOnlyDictionary<IPEndPoint, UdpClient> sockets,
        CancellationToken cancellationToken)
    {
        var socket = sockets[listeningEndPoint];

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
                logger.LogError(exception, "Listener socket error on {EndPoint}", listeningEndPoint);
                continue;
            }

            await HandleRequestAsync(layout, listeningEndPoint, received, sockets, cancellationToken);
        }
    }

    /// <summary>Answers one datagram, when it is a binding request.</summary>
    /// <param name="layout">Addresses and ports being served.</param>
    /// <param name="listeningEndPoint">Address and port the datagram arrived on.</param>
    /// <param name="received">Datagram and the endpoint it came from.</param>
    /// <param name="sockets">Every socket of the layout, keyed by what it serves.</param>
    /// <param name="cancellationToken">Token that stops the responder.</param>
    private async Task HandleRequestAsync(
        StunSocketLayout layout,
        IPEndPoint listeningEndPoint,
        UdpReceiveResult received,
        IReadOnlyDictionary<IPEndPoint, UdpClient> sockets,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!StunMessageCodec.TryParseBindingRequest(received.Buffer, out var request))
            {
                logger.LogDebug(
                    "Ignored {Length} bytes from {Peer}, which is not a binding request",
                    received.Buffer.Length,
                    received.RemoteEndPoint);
                return;
            }

            var changeAddress = (request.ChangeRequestFlags & StunMessageCodec.ChangeIpFlag) != 0;
            var changePort = (request.ChangeRequestFlags & StunMessageCodec.ChangePortFlag) != 0;

            // RFC 3489 section 9.2, relative to where the request arrived. The
            // console's own probe asks for both, Test II, and is what it decides on.
            var changedAddress = layout.OtherAddressOf(listeningEndPoint);
            var replyEndPoint = new IPEndPoint(
                changeAddress ? changedAddress.Address : listeningEndPoint.Address,
                changePort ? changedAddress.Port : listeningEndPoint.Port);

            logger.LogInformation(
                "{EndPoint} <- {Peer} length {Length} change_address {ChangeAddress} change_port {ChangePort}",
                listeningEndPoint,
                received.RemoteEndPoint,
                received.Buffer.Length,
                changeAddress,
                changePort);

            var response = StunMessageCodec.BuildBindingResponse(
                request,
                received.RemoteEndPoint,
                replyEndPoint,
                changedAddress);

            await sockets[replyEndPoint].SendAsync(response, received.RemoteEndPoint, cancellationToken);

            logger.LogInformation(
                "Answered {Peer} from {ReplyEndPoint}, naming {ChangedAddress} as the other address",
                received.RemoteEndPoint,
                replyEndPoint,
                changedAddress);
        }
        catch (OperationCanceledException)
        {
            // Shutdown while answering; the loop ends on the next turn.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error handling a port-check request");
        }
    }

    /// <summary>The sockets this responder serves and what their answers name.</summary>
    /// <param name="PrimaryAddress">Address the console dials.</param>
    /// <param name="SecondaryAddress">Second address, when there is one.</param>
    /// <param name="Port">Port the console dials.</param>
    private sealed record StunSocketLayout(IPAddress PrimaryAddress, IPAddress? SecondaryAddress, int Port)
    {
        /// <summary>The port that follows the served one, RFC 3489's P2.</summary>
        public int AlternatePort => Port + 1;

        /// <summary>
        /// Whether a change-address request can be answered honestly. It cannot
        /// when the configured address is the wildcard, which is no address to
        /// name, or when no second address was configured.
        /// </summary>
        public bool ServesTwoAddresses =>
            SecondaryAddress is not null && !PrimaryAddress.Equals(IPAddress.Any);

        /// <summary>The address and port pairs this responder receives on.</summary>
        public IEnumerable<IPEndPoint> ListeningEndPoints =>
            ServesTwoAddresses
                ? [
                    new IPEndPoint(PrimaryAddress, Port),
                    new IPEndPoint(PrimaryAddress, AlternatePort),
                    new IPEndPoint(SecondaryAddress!, Port),
                    new IPEndPoint(SecondaryAddress!, AlternatePort),
                ]
                : [
                    new IPEndPoint(PrimaryAddress, Port),
                    new IPEndPoint(PrimaryAddress, AlternatePort),
                ];

        /// <summary>Reads the addresses of the options.</summary>
        /// <param name="options">Options to read.</param>
        public static StunSocketLayout From(StunServerOptions options)
        {
            var primaryAddress = IPAddress.Parse(options.PrimaryAddress);
            var secondaryAddress = options.SecondaryAddress is null
                ? null
                : IPAddress.Parse(options.SecondaryAddress);

            // A second address equal to the first is no second address, and
            // binding it twice would fail.
            if (secondaryAddress is not null && secondaryAddress.Equals(primaryAddress))
            {
                secondaryAddress = null;
            }

            return new StunSocketLayout(primaryAddress, secondaryAddress, options.Port);
        }

        /// <summary>
        /// Reads CHANGED-ADDRESS for a request: the responder's other address and
        /// port, which describes neither the socket that received the request nor
        /// the one that answers it.
        /// </summary>
        /// <param name="listeningEndPoint">Address and port the request arrived on.</param>
        public IPEndPoint OtherAddressOf(IPEndPoint listeningEndPoint)
        {
            var address = listeningEndPoint.Address;

            if (ServesTwoAddresses)
            {
                address = address.Equals(PrimaryAddress) ? SecondaryAddress! : PrimaryAddress;
            }

            var port = listeningEndPoint.Port == Port ? AlternatePort : Port;

            return new IPEndPoint(address, port);
        }
    }
}
