using System.Net;
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
/// session key and the handshake reply behind it.
/// </summary>
/// <remarks>
/// The live host sends the keep-alive <b>first</b>, as outbound counter 0, and
/// the handshake reply behind it as counter 1
/// (<c>docs/mgo2-game.pcapng</c>: a 16-byte <c>0x5000</c> and a 44-byte
/// <c>0x1000</c>, both at t+1.6681). The reply leads here only because it reads
/// as the more important of the two; the capture puts the other way, and the
/// counters are observable, so the keep-alive takes counter 0.
/// </remarks>
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

        // Answers go to the endpoint the peer advertised, not to the one the
        // datagram came from. The handshake carries the peer's own pair for
        // exactly this: behind a load balancer the source is the balancer's
        // address and a port the peer's socket is not listening on, so replying
        // to it leaves the reply in a connection-tracking entry the peer never
        // reads. The source is only the fallback for a peer that advertised
        // nothing usable.
        var dialBack = AdvertisedEndpointOf(handshake) ?? context.Remote;
        context.Session.DialBack = dialBack;

        logger.LogInformation(
            "UDP {LocalPort}: handshake from {RemoteAddress} peer=0x{PeerIdentifier:x8} base=0x{CounterBase:x8}, answering {DialBack}",
            context.LocalPort,
            context.Remote,
            handshake.PeerIdentifier,
            handshake.CounterBase,
            dialBack);

        // 1. The keep-alive, which the recorded host sends first, as its
        //    outbound counter 0. The joiner reads it as the frame that
        //    establishes the session key, so it carries the key ahead of
        //    anything that needs it; the live capture puts it in the same
        //    millisecond as the handshake reply, and the order and the counters
        //    are what a client can observe and depend on.
        context.Session.Established = true;
        await context.Send(UdpCommandConstants.KeepAlive, []);

        // 2. The handshake reply, still pre-keyed because the joiner has not
        //    reached its keyed state yet. It advertises the address this host is
        //    reached on, which is the configured one for the same reason.
        var reply = FrameBuilderUtility.BuildHandshakeBody(
            hostIdentity.PeerIdentifier,
            hostIdentity.CounterBase,
            hostIdentity.AdvertisedAddress,
            hostIdentity.AdvertisedPort);
        await context.Send(UdpCommandConstants.Handshake, reply);

        logger.LogInformation(
            "UDP {LocalPort}: session with peer=0x{PeerIdentifier:x8} established",
            context.LocalPort,
            handshake.PeerIdentifier);
    }

    /// <summary>
    /// Picks the endpoint a peer's handshake says it is listening on. The first
    /// advertised pair is the public one and the second the private, so the
    /// public is preferred; a pair that names no address or no port is skipped,
    /// because a peer that advertises nothing usable is better answered at the
    /// address the datagram came from than at a placeholder.
    /// </summary>
    /// <param name="handshake">Parsed handshake body.</param>
    /// <returns>The advertised endpoint, or <c>null</c> when there is none usable.</returns>
    private static IPEndPoint? AdvertisedEndpointOf(HandshakeBody handshake)
    {
        foreach (var pair in handshake.Pairs)
        {
            if (pair.Port != 0 && IPAddress.TryParse(pair.Address, out var address))
            {
                return new IPEndPoint(address, pair.Port);
            }
        }

        return null;
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
/// Recognises the in-game control records a running game exchanges, and does
/// nothing else with them.
/// </summary>
/// <remarks>
/// <para>
/// A live session is mostly two record types nobody has decoded: a zero-length
/// one the client sends in volume with a fourth byte that climbs monotonically,
/// and a one-byte one whose body is a small counter. Both are normal traffic,
/// not surprises, so they are handled here rather than falling through to the
/// "no handler for peer message type" warning — which, at the several hundred
/// of them a round contains, buries the warnings that matter.
/// </para>
/// <para>
/// Neither is answered. What either one *means* is unresolved
/// (docs/protocol/UDP_GAME_CAPTURE.md §3), and answering a record this server
/// cannot read is how <c>0x43CA</c>/<c>0x43CB</c> and <c>0x4442</c> went wrong in
/// this project. They are logged at debug level so a session can be watched
/// without turning the log into the traffic itself.
/// </para>
/// </remarks>
public sealed class InGameControlHandler(ILogger<InGameControlHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(PeerContext context)
    {
        logger.LogDebug(
            "UDP {LocalPort}: in-game control {MessageType:x4} of {BodyLength} bytes from {RemoteAddress} " +
            "(fourth byte {Flags}); not answered, its meaning is unresolved",
            context.LocalPort,
            context.Message.Type,
            context.Message.Body.Length,
            context.Remote,
            context.Message.Flags);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Handles a player-profile record. The joiner sends its own profile once the
/// session is keyed and then re-sends it, byte for byte, until the host answers;
/// this handler answers with the whole room roster, the host's own entry first
/// under the join tag and every joining player after it in slot order under the
/// roster tag, closes the run with the record that ends a roster, sends that run
/// a second time without its head and then one bare empty head record after it,
/// the way the recorded host does, and tells the peers already in the room that
/// the roster grew.
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
        var profile = PlayerProfileRecordParseUtils.Parse(context.Message.Body);
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
            "UDP {LocalPort}: profile from {RemoteAddress}: character {CharacterId} name {Name}, roster slot {RosterIndex}",
            context.LocalPort,
            context.Remote,
            profile.CharacterId,
            profile.Name,
            member.RosterIndex);

        // The run is built once and sent twice, so the joiner and the room are
        // told the same roster and the same end to it. Each record carries the
        // type the recorded host sent it under: the host's own entry under the
        // join tag, the joining players and the close under the roster tag.
        var run = roster.BuildRosterRun();

        // The host's own entry going out under 0x9001 is the odd one, and it is
        // logged at information level because nothing else about it explains
        // itself. 0x9001 is the tag a *joiner* opens the exchange with, and the
        // recorded dedicated server answers with its own roster entry under the
        // same tag, ahead of the 0x1001 records that carry everyone else
        // (docs/protocol/UDP_GAME_CAPTURE.md §4, both roster frames). The game
        // appears to be reusing one tag for both ends of the exchange rather
        // than the two types the name suggests, so this is worth seeing in a
        // log rather than discovering from a client that ignores the roster.
        foreach (var record in run.Where(record => record.Type == RoomRosterService.HostEntryType))
        {
            logger.LogInformation(
                "UDP {LocalPort}: sending this host's own roster entry to {RemoteAddress} under " +
                "0x{MsgType:x4}, the tag a joiner opens the exchange with; the rest of the roster " +
                "travels as 0x{RosterType:x4}",
                context.LocalPort,
                context.Remote,
                record.Type,
                UdpCommandConstants.PlayerProfile);
        }

        // Answering a repeat is deliberate: the joiner re-sends its profile
        // until it sees the roster, so a reply to a repeat is what breaks the
        // exchange, not a bug. Re-registering is idempotent, so the repeated
        // profile keeps the slot it was first given.
        foreach (var record in run)
        {
            await context.Send(record.Type, record.Body);
        }

        // Then the run again, without its head, which is what the recorded host
        // sends. It is not a retransmission and nothing waits for an
        // acknowledgement: the live host puts the two on consecutive outbound
        // sequences with the tick stream between them, and the joiner never
        // acknowledged either. RoomRosterService.BuildRosterRunRepeat carries
        // the measurement.
        foreach (var record in roster.BuildRosterRunRepeat())
        {
            await context.Send(record.Type, record.Body);
        }

        // Then one more empty 0x5001 on its own, the record the live host closes
        // a roster exchange with. It repeats the type that opened the run rather
        // than closing it; what it says is unresolved, and it is sent because the
        // host sends it.
        var trailer = RoomRosterService.BuildRosterTrailer();
        await context.Send(trailer.Type, trailer.Body);

        // The peers already in the room are told as well. A player who is only
        // announced to the peer that just joined is never announced to the ones
        // that were there first, and they would go on playing without knowing
        // the room has grown. The roster is small enough to send whole, which is
        // also what the recorded host wrote: one flat roster, not a delta.
        foreach (var record in run)
        {
            await context.Broadcast(record.Type, record.Body);
        }
    }
}
