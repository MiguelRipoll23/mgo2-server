using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands;

/// <summary>
/// Accepts a joiner's handshake, then sends the keep-alive that establishes the
/// session key.
/// </summary>
/// <param name="hostIdentity">Identity this host presents to its peers.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class AcceptHandshakeHandler(
    HostIdentityService hostIdentity,
    ILogger<AcceptHandshakeHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        var handshake = FrameBuilderUtility.ParseHandshakeBody(context.Message.Body);
        if (handshake is null)
        {
            logger.LogWarning(
                "UDP {LocalPort}: a handshake from {RemoteAddress} failed validation",
                context.LocalPort,
                context.Remote);
            return;
        }

        // The provisioning pass ran before the body was parsed, so the identity
        // it carried is written back here together with the session key.
        context.Session.PeerIdentifier = handshake.PeerIdentifier;
        context.Session.CounterBase = handshake.CounterBase;
        context.Session.SessionKey = handshake.CounterBase ^ hostIdentity.CounterBase;

        // Answers go to the address the datagram came from, and never to the
        // pair the peer advertised in its own handshake. That pair is whatever
        // a STUN lookup returned for the peer, so a console on a home network
        // names its router's public address there -- and from inside that same
        // network the router does not loop the answer back, so every reply
        // vanishes without an error. The observed source is reachable by
        // definition: a peer on the LAN shows its LAN address and one dialling
        // in from outside shows the public address the NAT gave it. One path
        // serves both, and it is what this host did before it started obeying
        // the advertised pair.
        //
        // The deployment owes the source for this to be the peer's real
        // endpoint: externalTrafficPolicy Local plus the source-NAT rule in
        // deploy/README.md. Where the source is a load balancer's conntrack
        // address instead, this is the wrong address -- but so was every
        // alternative, and the peer's own pair is never one of them.
        var dialBack = context.Remote;
        context.Session.DialBack = dialBack;

        logger.LogInformation(
            "UDP {LocalPort}: handshake from {RemoteAddress} peer=0x{PeerIdentifier:x8} base=0x{CounterBase:x8}, answering {DialBack}",
            context.LocalPort,
            context.Remote,
            handshake.PeerIdentifier,
            handshake.CounterBase,
            dialBack);

        // 1. The handshake reply, still pre-keyed because the joiner has not
        //    reached its keyed state yet. It advertises the address this host is
        //    reached on, which is the configured one for the same reason.
        var reply = FrameBuilderUtility.BuildHandshakeBody(
            hostIdentity.PeerIdentifier,
            hostIdentity.CounterBase,
            hostIdentity.AdvertisedAddress,
            hostIdentity.AdvertisedPort);
        await context.Send(UdpCommandConstants.Handshake, reply);

        // 2. The key-establishing keep-alive.
        context.Session.Established = true;
        await context.Send(UdpCommandConstants.KeepAlive, []);
        logger.LogInformation(
            "UDP {LocalPort}: session with peer=0x{PeerIdentifier:x8} established",
            context.LocalPort,
            handshake.PeerIdentifier);
    }

}

/// <summary>Mirrors a peer's keep-alive straight back.</summary>
public sealed class AcknowledgeKeepAliveHandler : IPeerCommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(PeerContext context) =>
        context.Send(UdpCommandConstants.KeepAlive, context.Message.Body);
}

/// <summary>
/// Handles a player-profile record. The joiner sends its own profile once the
/// session is keyed and then re-sends it, byte for byte, until the host answers;
/// this handler answers with the whole room roster, the host's own entry first
/// and every joining player after it in slot order, and tells the peers already
/// in the room that the roster grew.
/// </summary>
/// <remarks>
/// The type is shared. A joining client also sends one-byte <c>0x1001</c>
/// messages — 23 of them against 11 profiles in the capture in
/// <c>docs/protocol/UDP_SERVER_LOG.txt</c> — which are not profiles and are not
/// answered, because answering each one with the whole roster to the sender and
/// to every peer in the room turns a quiet host into a flood.
/// </remarks>
/// <param name="roster">Roster of the room this host is playing.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerProfileHandler(
    RoomRosterService roster,
    ILogger<PlayerProfileHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        var profile = PlayerProfileRecordUtility.Parse(context.Message.Body);
        if (profile is not { Name.Length: > 0 })
        {
            logger.LogDebug(
                "UDP {LocalPort}: {MessageType:x4} of {BodyLength} bytes from {RemoteAddress} carries no profile; not answered",
                context.LocalPort,
                context.Message.Type,
                context.Message.Body.Length,
                context.Remote);
            return;
        }

        var member = roster.Register(context.Remote, profile);

        logger.LogInformation(
            "UDP {LocalPort}: profile from {RemoteAddress}: character {CharacterIdentifier} name {Name}, roster slot {RosterIndex}",
            context.LocalPort,
            context.Remote,
            profile.CharacterIdentifier,
            profile.Name,
            member.RosterIndex);

        // Answering a repeat is deliberate: the joiner re-sends its profile
        // until it sees the roster, so a reply to a repeat is what breaks the
        // exchange, not a bug. Re-registering is idempotent, so the repeated
        // profile keeps the slot it was first given.
        foreach (var record in roster.BuildRecords())
        {
            await context.Send(UdpCommandConstants.PlayerProfile, record);
        }

        // The peers already in the room are told as well. A player who is only
        // announced to the peer that just joined is never announced to the ones
        // that were there first, and they would go on playing without knowing
        // the room has grown. The roster is small enough to send whole, which is
        // also what the recorded host wrote: one flat roster, not a delta.
        foreach (var record in roster.BuildRecords())
        {
            await context.Broadcast(UdpCommandConstants.PlayerProfile, record);
        }
    }
}
