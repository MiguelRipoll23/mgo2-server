using System.Net;
using Mgo2Server.GameplayServer.Commands;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the handshake to the endpoint the host answers at, and to the one it
/// names in its own reply.
/// </summary>
/// <remarks>
/// These are two different questions and the answers must not be confused. The
/// host answers where the datagram came from, because that endpoint is
/// reachable by definition. It names its configured advertised address in the
/// reply, because that is the only address a peer can dial. The peer's own
/// advertised pair is neither: it is whatever a STUN lookup returned for it, so
/// a console behind a home router names the router's public address there, and
/// answering that from inside the same network goes nowhere.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class AcceptHandshakeHandlerTests
{
    private const string AdvertisedAddress = "100.69.153.69";
    private const int AdvertisedPort = 5730;

    /// <summary>
    /// The endpoint a datagram is observed arriving from. The deployment sets
    /// externalTrafficPolicy Local so this is the console's real address and
    /// port rather than a load balancer's connection-tracking address.
    /// </summary>
    private static readonly IPEndPoint ObservedSource = new(IPAddress.Parse("192.168.1.20"), 5730);

    /// <summary>Size of a handshake body, which is a fixed header plus two endpoint pairs.</summary>
    private const int HandshakeBodySize = 0x1c;

    private static HostIdentityService CreateHostIdentity() =>
        new(Options.Create(new ServerOptions
        {
            PublicHostAddress = AdvertisedAddress,
            GameplayServerPort = AdvertisedPort,
        }));

    private static byte[] HandshakeBodyAdvertising(string address, int port)
    {
        var body = new byte[HandshakeBodySize];
        BinaryUtility.WriteUInt32LittleEndian(body, 0, 2);
        BinaryUtility.WriteUInt32LittleEndian(body, 4, 0xa9d0a9e4);
        BinaryUtility.WriteUInt32LittleEndian(body, 8, UdpCryptoKeyConstants.ModuleMagic);
        body[12] = 0x02;
        body[13] = 0x01;
        body[15] = 1;

        var octets = IPAddress.Parse(address).GetAddressBytes();
        octets.CopyTo(body, 16);
        BinaryUtility.WriteUInt16LittleEndian(body, 20, (ushort)port);
        return body;
    }

    private static async Task<(PeerSession Session, List<byte[]> Sent)> RunAsync(byte[] body)
    {
        var session = new PeerSession
        {
            RemoteAddress = ObservedSource.ToString(),
            // Provisioning seeds this from the datagram source, and the handler
            // has to leave it there rather than replace it.
            DialBack = ObservedSource,
            CounterBase = 0x11223344,
            OutboundCounter = 0,
            SessionKey = 0,
            PeerIdentifier = 0,
            HostPeerIdentifier = 1,
            Established = false,
            LastSeenAt = 0,
            LastInboundSequence = 0,
        };

        List<byte[]> sent = [];
        var handler = new AcceptHandshakeHandler(
            CreateHostIdentity(),
            NullLogger<AcceptHandshakeHandler>.Instance);

        var context = new PeerContext(
            session,
            UdpMessage.Create(UdpCommandConstants.Handshake, body),
            ObservedSource,
            AdvertisedPort,
            (_, reply) =>
            {
                sent.Add(reply);
                return Task.CompletedTask;
            },
            (_, reply) =>
            {
                sent.Add(reply);
                return Task.CompletedTask;
            });

        await handler.HandleAsync(context);
        return (session, sent);
    }

    [Fact]
    public async Task HandleAsync_answers_where_the_datagram_came_from()
    {
        var (session, _) = await RunAsync(HandshakeBodyAdvertising("192.168.1.50", 25110));

        Assert.Equal(ObservedSource.Address, session.DialBack.Address);
        Assert.Equal(ObservedSource.Port, session.DialBack.Port);
    }

    [Fact]
    public async Task HandleAsync_answers_the_source_even_when_the_peer_advertises_its_public_address()
    {
        // What a console on a home network puts in its handshake is the address
        // its router presents to the internet, not one it is reading on. That is
        // the case this host used to handle by answering it, which put every
        // reply on a path the router never loops back. The observed source is
        // the console's real socket and is the only endpoint worth answering.
        var (session, _) = await RunAsync(HandshakeBodyAdvertising("89.129.16.203", 5730));

        Assert.Equal("192.168.1.20", session.DialBack.Address.ToString());
        Assert.Equal(5730, session.DialBack.Port);
    }

    [Fact]
    public async Task HandleAsync_answers_the_source_when_the_peer_advertises_nothing_usable()
    {
        // A placeholder pair is no reason to behave differently; the source is
        // the answer either way.
        var (session, _) = await RunAsync(HandshakeBodyAdvertising("0.0.0.0", 0));

        Assert.Equal(ObservedSource.Address, session.DialBack.Address);
        Assert.Equal(ObservedSource.Port, session.DialBack.Port);
    }

    [Fact]
    public async Task HandleAsync_advertises_the_address_this_host_is_reached_on()
    {
        // The other direction still names the configured address, and it has
        // to: the peer dials what the host says, not where the datagram came
        // from, so this is the one place the configured value is correct.
        var (_, sent) = await RunAsync(HandshakeBodyAdvertising("192.168.1.50", 25110));

        var reply = FrameBuilderUtility.ParseHandshakeBody(sent[0]);
        Assert.NotNull(reply);
        Assert.Equal(AdvertisedAddress, reply.Pairs[0].Address);
        Assert.Equal(AdvertisedPort, reply.Pairs[0].Port);
    }

    [Fact]
    public void The_advertised_address_is_the_one_published_to_joiners()
    {
        // The address a joining client is handed in the join result and the one
        // a peer is told to answer on have to be the same, or the client dials
        // one endpoint and is sent to another. Both now read this property.
        var options = new ServerOptions { PublicHostAddress = AdvertisedAddress };

        Assert.Equal(AdvertisedAddress, options.GameplayServerAdvertisedAddress);
    }

    [Fact]
    public void The_advertised_address_falls_back_to_loopback_rather_than_the_wildcard()
    {
        // The wildcard is no more dialable than any real address, so a server
        // that was given nothing to announce says loopback.
        Assert.Equal("127.0.0.1", new ServerOptions().GameplayServerAdvertisedAddress);
    }
}
