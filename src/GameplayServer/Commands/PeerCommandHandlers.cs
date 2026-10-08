using System.Net;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.GameplayServer.Stream;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer.Commands;

/// <summary>
/// Accepts a joiner's handshake, sends the keep-alive and the handshake reply
/// behind it, both pre-keyed, then one session-keyed keep-alive and the flag
/// that signs everything after it — the roster run first — with the session key.
/// </summary>
/// <remarks>
/// The live host sends the keep-alive <b>first</b>, as outbound counter 0, and
/// the handshake reply behind it as counter 1
/// (<c>docs/mgo2-game.pcapng</c>: a 16-byte <c>0x5000</c> and a 44-byte
/// <c>0x1000</c>, both at t+1.6681). The reply reads as the more important of
/// the two, so it is easy to send first; the capture puts the other way round,
/// and the counters are observable, so the keep-alive takes counter 0.
/// <para>
/// <b>Both</b> of those frames travel pre-keyed, and the flag that decides it
/// cannot be set before them. Searching the capture's digest over every
/// derivation of the two counter bases and the two constants, the 16-byte
/// keep-alive and the 44-byte reply verify with the bare constant
/// <c>0x2b58de69</c> and with <c>K ^ 0x2b58de69</c> at neither; the joiner's
/// first session-keyed frame, 92 bytes at counter 32769, verifies only with
/// <c>K ^ 0x2b58de69</c>. So the whole state-&lt;8 → pre-key rule
/// (<c>UDP_P2P_PROTOCOL.md</c> §6) covers the opening exchange as well: the
/// joiner cannot read a session-keyed frame until it has accepted a handshake
/// reply, and it accepts that reply only after reading it. Marking the session
/// established before the reply sent both frames keyed, the joiner discarded
/// them at the digest gate before parsing a field, and re-dialled every ~1.9 s
/// until it gave up — 17 handshakes answered with 17 identical failures, each
/// logged here as a session established.
/// </para>
/// <para>
/// <b>One session-keyed frame goes out after the reply, and a live join is
/// why.</b> The joiner's connect FSM (<c>FUN_00aa1140</c>,
/// <c>docs/protocol/P2P_CONNECT_FSM.md</c>) leaves its dial state when the
/// module session reports the data phase, state 8, which needs flags bits
/// <c>0x1</c> and <c>0x2</c>. Bit <c>0x1</c> comes from the decoder's second
/// digest chance — a frame whose tail digest verifies with
/// <c>K ^ 0x2b58de69</c> (<c>UDP_P2P_PROTOCOL.md</c> §11.4) — and accepting the
/// reply does not set it, which reading the reference capture alone
/// (<c>P2P_CONNECT_FSM.md</c> §7.1) is what said otherwise. A live client
/// settles it: answered with the two recorded frames and nothing else, the
/// session sits in its reply-accepted state and state 2 counts <em>both</em> of
/// its deadlines out — 6000 units, a tick that still reports that state and
/// resets the countdown to 4500, then the failure path. That is 35.0 s after the
/// dial, and it is the join that produced <c>0B09</c>. The frame costs the
/// capture's counters one place, so the roster run leaves at counter 3 rather
/// than 2; a joiner's sequence window is <c>last+1 .. last+0x20</c> (§11.5), so
/// only consecutiveness has to hold.
/// </para>
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
        context.Session.PeerAddressData = FrameBuilderUtility.EndpointData(handshake.Pairs);

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

        // 1. The keep-alive first, as outbound counter 0. The recorded host
        //    leads with it, and the counters are observable, so a client keying
        //    anything off them sees the order: a 16-byte 0x5000 at hdr 0x0000
        //    and the 44-byte reply behind it at hdr 0x0001, both at t+1.6681
        //    (docs/protocol/UDP_GAME_CAPTURE.md §4).
        await context.Send(UdpCommandConstants.KeepAlive, [], 0);

        // 2. The handshake reply behind it, as counter 1, and pre-keyed for the
        //    same reason as the keep-alive: the session is not established yet,
        //    and marking it so here is what made the joiner discard these
        //    frames. The state<8 -> pre-key rule is absolute, and nothing may
        //    carry the session key ahead of the reply that earns it.
        var reply = FrameBuilderUtility.BuildHandshakeBody(
            hostIdentity.PeerIdentifier,
            hostIdentity.CounterBase,
            hostIdentity.AdvertisedAddress,
            hostIdentity.AdvertisedPort);
        await context.Send(UdpCommandConstants.Handshake, reply, 0);

        // Only now, and with nothing sent in between: the flag is what makes
        // this frame keyed, and one keyed frame is what a live joiner needs to
        // reach its data phase (P2P_CONNECT_FSM.md §7).
        context.Session.Established = true;
        await context.Send(UdpCommandConstants.KeepAlive, [], 0);

        logger.LogInformation(
            "UDP {LocalPort}: session with peer=0x{PeerIdentifier:x8} established; opening exchange pre-keyed, then the keyed keep-alive",
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

/// <summary>
/// Reads a peer's keyed keep-alive and answers nothing.
/// </summary>
/// <remarks>
/// The one keyed keep-alive this host writes is the frame
/// <see cref="AcceptHandshakeHandler"/> puts behind its reply, and it exists to
/// carry a live joiner into its data phase. This handler is the other direction.
/// The capture's opening <c>0x5000</c> is the pre-keyed one that leads the
/// handshake reply (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4), and a joiner's
/// profile frame carries one beside its profile, and the live host answers the
/// profile with the roster and not the keep-alive. Mirroring an inbound one back
/// spends an outbound sequence the capture never spends at that point and shifts
/// every later counter away from it. So it is logged and dropped rather than
/// answered.
/// </remarks>
/// <param name="logger">Logger of this handler.</param>
public sealed class PeerKeepAliveHandler(ILogger<PeerKeepAliveHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(PeerContext context)
    {
        logger.LogDebug(
            "UDP {LocalPort}: keyed keep-alive from {RemoteAddress}; not answered, the recorded host sends none",
            context.LocalPort,
            context.Remote);

        return Task.CompletedTask;
    }
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
/// Neither is answered here. What either one *means* is unresolved
    /// (docs/protocol/UDP_GAME_CAPTURE.md §3), and answering a record this server
    /// cannot read is how <c>0x43CA</c>/<c>0x43CB</c> and <c>0x4442</c> went wrong in
    /// this project. They are logged at debug level so a session can be watched
    /// without turning the log into the traffic itself.
    /// </para>
/// <para>
    /// The one-byte record is mostly the other direction: the live host writes
    /// 176 of the 181 in a round and the client writes 5. This handler is what
    /// receives the 5; what the host writes is built by
    /// <see cref="PostJoinBurstService"/>.
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
/// <param name="burstScheduler">Scheduler of the one-shot burst that follows the roster exchange.</param>
/// <param name="hostStream">Stream the recorded host keeps writing after the burst.</param>
/// <param name="options">
/// Options of this instance. The one this handler reads is whether the traffic
/// that follows the roster is written at all, which is the switch that tells a
/// join stalled on that traffic apart from one stalled on the roster.
/// </param>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerProfileHandler(
    RoomRosterService roster,
    PostJoinBurstSchedulerService burstScheduler,
    HostStreamService hostStream,
    IOptions<ServerOptions> options,
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

        var member = roster.Register(
            context.Remote,
            profile,
            context.Session.PeerIdentifier,
            context.Session.PeerAddressData);

        if (context.Session.PeerAddressData.Length != PlayerProfileRecordUtility.AddressDataLength)
        {
            logger.LogWarning(
                "UDP {LocalPort}: profile from {RemoteAddress} has no valid pair of handshake endpoints; " +
                "its roster address fields cannot be echoed faithfully",
                context.LocalPort,
                context.Remote);
        }

        logger.LogInformation(
            "UDP {LocalPort}: profile from {RemoteAddress}: character {CharacterId} name {Name}, roster slot {RosterIndex}",
            context.LocalPort,
            context.Remote,
            member.CharacterId,
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
        //
        // The whole run goes out as one datagram. The recorded host packs the
        // head, its own entry, every player's entry and the closing record into
        // a single frame, and one record per datagram spends an outbound
        // sequence on each and hands the joiner a roster it never saw
        // assembled.
        await RosterRunSendUtils.SendAsync(context, run, compressed: true);

        // Then the run again, without its head, which is what the recorded host
        // sends. It is not a retransmission and nothing waits for an
        // acknowledgement: the live host puts the two on consecutive outbound
        // sequences with the tick stream between them, and the joiner never
        // acknowledged either. RoomRosterService.BuildRosterRunRepeat carries
        // the measurement.
        await RosterRunSendUtils.SendAsync(context, roster.BuildRosterRunRepeat(), compressed: true);

        // Then one more empty 0x5001 on its own, the record the live host closes
        // a roster exchange with. It repeats the type that opened the run rather
        // than closing it; what it says is unresolved, and it is sent because the
        // host sends it.
        var trailer = RoomRosterService.BuildRosterTrailer();
        await context.Send(trailer.Type, trailer.Body, trailer.Ordinal);

        // Then the burst, which the recorded host does not send with the roster
        // but a measured pause after it. It is scheduled rather than awaited, so
        // the dispatch loop keeps reading every peer while the pause runs.
        //
        // Then the stream the recorded host keeps writing, which is what the
        // channel is made of once the join is over: the sparse control and state
        // records, the match-start run and the steady beat under them. It is
        // started once per peer and runs on its own, so the dispatch loop keeps
        // reading every peer while it plays out.
        //
        // Both are held behind one switch, because the join above is not: with
        // it off a peer still gets the handshake, the roster run, its repeat and
        // the trailer, and the only thing that changes is what comes after.
        if (options.Value.GameplayServerPostJoinTraffic)
        {
            burstScheduler.Schedule(context);
            hostStream.Start(context);
        }
        else
        {
            logger.LogInformation(
                "UDP {LocalPort}: post-join traffic is off; sending nothing after the roster to {RemoteAddress}",
                context.LocalPort,
                context.Remote);
        }

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
