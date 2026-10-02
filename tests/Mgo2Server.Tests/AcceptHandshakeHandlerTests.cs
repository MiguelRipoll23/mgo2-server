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
/// Pins the handshake to the endpoints each side actually advertises.
/// </summary>
/// <remarks>
/// A handshake carries the peer's own address pair, and behind a load balancer
/// the address a datagram arrives from is the balancer's own — an address the
/// peer cannot dial and a port its socket is not reading. Both ends of the
/// exchange therefore have to come from what was advertised, not from what was
/// observed on the wire.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class AcceptHandshakeHandlerTests
{
    private const string AdvertisedAddress = "100.69.153.69";
    private const int AdvertisedPort = 5730;

    /// <summary>Address a datagram appears to come from once a balancer has rewritten it.</summary>
    private static readonly IPEndPoint Balancer = new(IPAddress.Parse("10.42.0.1"), 41234);

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
            RemoteAddress = Balancer.ToString(),
            // Provisioning sets this to the datagram source, which is what the
            // handshake handler then has to replace with the advertised pair.
            DialBack = Balancer,
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
            Balancer,
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
    public async Task HandleAsync_answers_at_the_endpoint_the_peer_advertised()
    {
        var (session, _) = await RunAsync(HandshakeBodyAdvertising("192.168.1.50", 25110));

        Assert.Equal("192.168.1.50", session.DialBack.Address.ToString());
        Assert.Equal(25110, session.DialBack.Port);
    }

    [Fact]
    public async Task HandleAsync_keeps_the_datagram_source_when_the_peer_advertises_nothing_usable()
    {
        // A peer that advertises a placeholder has nothing better to be told,
        // so the address the datagram came from is all there is.
        var (session, _) = await RunAsync(HandshakeBodyAdvertising("0.0.0.0", 0));

        Assert.Equal(Balancer.Address, session.DialBack.Address);
        Assert.Equal(Balancer.Port, session.DialBack.Port);
    }

    [Fact]
    public async Task HandleAsync_advertises_the_address_this_host_is_reached_on()
    {
        var (_, sent) = await RunAsync(HandshakeBodyAdvertising("192.168.1.50", 25110));

        // The keep-alive leads, as it does on the wire, so the handshake body is the
        // second thing this handler sends and the first is empty.
        Assert.Empty(sent[0]);
        var reply = FrameBuilderUtility.ParseHandshakeBody(sent[1]);
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
        // The wildcard is no more dialable than the balancer's address, so a
        // server that was given nothing to announce says loopback.
        Assert.Equal("127.0.0.1", new ServerOptions().GameplayServerAdvertisedAddress);
    }
}
