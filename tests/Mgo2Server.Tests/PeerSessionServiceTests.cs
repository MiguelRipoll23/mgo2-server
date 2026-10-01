using System.Net;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Udp;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins that a peer stays reachable once its frames start arriving from the
/// endpoint it advertised rather than the one its handshake came from.
/// </summary>
/// <remarks>
/// The dispatch half of the gameplay server looks a session up by the source of
/// the datagram, and the handshake handler answers at the pair the peer
/// advertised because that is the only endpoint the peer's socket is reading.
/// The two agree for a peer out on the internet, where both name the peer's
/// NATed address, and part company wherever something rewrites the source in
/// between. Without the fallback the peer's frames are all dropped as
/// undecodable, which is what a hairpining router provokes on a LAN where the
/// console and the host share one public address.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class PeerSessionServiceTests
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The endpoint a console's handshake is observed arriving from.</summary>
    private static readonly IPEndPoint Observed = new(IPAddress.Parse("192.168.1.20"), 5730);

    /// <summary>The pair that console advertises, which is the house's public address.</summary>
    private static readonly IPEndPoint Advertised = new(IPAddress.Parse("89.129.16.203"), 5730);

    private static PeerSession Handshaken(PeerSessionService sessions)
    {
        var session = new PeerSession
        {
            RemoteAddress = Observed.ToString(),
            DialBack = Observed,
            CounterBase = 0x293edf93,
            OutboundCounter = 0,
            SessionKey = 0,
            PeerIdentifier = 2,
            HostPeerIdentifier = 1,
            Established = false,
            LastSeenAt = 0,
            LastInboundSequence = 0,
        };

        sessions.Put(session);
        return session;
    }

    /// <summary>
    /// The lookup the dispatch half performs, kept here so the test exercises
    /// the same two steps in the same order rather than a paraphrase of them.
    /// </summary>
    private static PeerSession? Lookup(PeerSessionService sessions, IPEndPoint from) =>
        sessions.Get($"{from.Address}:{from.Port}") ?? sessions.FindByDialBack(from);

    [Fact]
    public void A_peer_is_found_by_the_endpoint_its_handshake_came_from()
    {
        var sessions = new PeerSessionService(IdleTimeout);
        var session = Handshaken(sessions);

        Assert.Same(session, Lookup(sessions, Observed));
    }

    [Fact]
    public void A_peer_is_still_found_once_it_dials_back_to_the_endpoint_it_advertised()
    {
        var sessions = new PeerSessionService(IdleTimeout);
        var session = Handshaken(sessions);

        // What the handshake handler does with every peer: answer where the
        // peer says it is listening rather than where the datagram came from.
        session.DialBack = Advertised;
        session.Established = true;

        Assert.Same(session, Lookup(sessions, Advertised));
    }

    [Fact]
    public void An_endpoint_no_peer_advertised_finds_nothing()
    {
        var sessions = new PeerSessionService(IdleTimeout);
        Handshaken(sessions);

        Assert.Null(Lookup(sessions, new IPEndPoint(IPAddress.Parse("203.0.113.7"), 5730)));
    }

    [Fact]
    public void The_fallback_does_not_displace_the_session_filed_under_its_source()
    {
        var sessions = new PeerSessionService(IdleTimeout);
        var session = Handshaken(sessions);
        session.DialBack = Advertised;

        // The session stays keyed by where it was provisioned, so the reaper
        // still ages it out and the count is still one peer rather than two
        // entries for the same conversation.
        Assert.Single(sessions.Snapshot());
        Assert.Equal(1, sessions.Count);
    }
}