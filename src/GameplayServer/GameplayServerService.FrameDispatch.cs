using System.Net;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The dispatch half of the gameplay server: it classifies an inbound datagram,
/// removes its frame chain, hands the messages to their handlers and keeps the
/// cumulative acknowledgement window.
/// </summary>
public sealed partial class GameplayServerService
{
    private async Task HandleDatagramAsync(byte[] wire, IPEndPoint remote)
    {
        var remoteAddress = $"{remote.Address}:{remote.Port}";

        try
        {
            await HandleDatagramAsyncCore(wire, remote);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Unhandled error processing datagram from {RemoteAddress}",
                remoteAddress);
        }
    }

    private async Task HandleDatagramAsyncCore(byte[] wire, IPEndPoint remote)
    {
        var remoteAddress = $"{remote.Address}:{remote.Port}";
        var work = wire.ToArray();

        // The datagram as it arrived, before any unscrambling. Every later stage
        // reports what it made of the frame, and when a frame is rejected that
        // is not enough to tell a bad decode from a frame that was never the
        // shape we expected. The bytes settle that, and they are the only
        // record of the frame that no later stage can alter.
        logger.LogDebug(
            "Raw {Length} bytes from {RemoteAddress}: {RawHex}",
            work.Length,
            remoteAddress,
            Convert.ToHexString(work));

        // The digest key classifies the frame before the chain is removed:
        // pre-keyed frames verify with the bare constant, keyed frames with the
        // session key combined with it.
        // The session is filed under the endpoint the handshake arrived from,
        // while the frames that follow it arrive from the endpoint the peer
        // advertised and the handler dialed back to. Those are the same
        // endpoint for a peer out on the internet and different whenever
        // something rewrites the source in between - a load balancer, or a
        // router hairpining a reply addressed to its own public address back
        // into the LAN - and a lookup by the observed source alone drops every
        // one of those peer's frames as undecodable, because the digest cannot
        // be verified without the session key.
        var session = sessions.Get(remoteAddress) ?? sessions.FindByDialBack(remote);
        var preKeyedCounter = TryUnscrambleWith(work, UdpCryptoKeyConstants.TailDigestKey);
        ushort? keyedCounter = null;
        uint sessionKey = 0;

        if (preKeyedCounter is null && session is not null)
        {
            sessionKey = session.CounterBase ^ hostIdentity.CounterBase;
            keyedCounter = TryUnscrambleWith(work, sessionKey ^ UdpCryptoKeyConstants.TailDigestKey);
        }

        if (preKeyedCounter is { } preCounter)
        {
            FrameCryptoUtility.RemoveChainInPlace(work, preCounter, UdpCryptoKeyConstants.PreHandshakeKey);
            await DispatchPreKeyedAsync(work, preCounter, remote, remoteAddress);
            return;
        }

        if (keyedCounter is { } keyed && session is not null)
        {
            FrameCryptoUtility.RemoveChainInPlace(work, keyed, sessionKey);
            await DispatchKeyedAsync(work, keyed, sessionKey, session, remote);
            return;
        }

        // The two endpoints this host would have recognised are the one the
        // datagram came from and the one every live session dials back to, so
        // both are named. A mismatch between them is the whole diagnosis: the
        // peer is answering from somewhere the session was never filed under,
        // which is what a rewritten source does to it.
        logger.LogWarning(
            "Undecodable datagram from {RemoteAddress}: no session is keyed on that endpoint and none dials back to it; sessions held: {DialBacks}",
            remoteAddress,
            string.Join(", ", sessions.Snapshot().Select(session => session.DialBack)));
    }

    /// <summary>
    /// Unscrambles and verifies a copy of the frame, then applies the same
    /// unscrambling to the working buffer. The buffer is left
    /// header-unscrambled with its chain still on, which is the state the digest
    /// gate reads.
    /// </summary>
    /// <param name="work">Frame to unscramble in place.</param>
    /// <param name="digestKey">Key the tail digest is verified with.</param>
    /// <returns>The header counter, or <c>null</c> when the digest gate fails.</returns>
    private static ushort? TryUnscrambleWith(byte[] work, uint digestKey)
    {
        var copy = work.ToArray();
        var counter = FrameCryptoUtility.UnscrambleHeaderOnly(copy);
        if (!FrameCryptoUtility.VerifyTailDigest(copy, digestKey))
        {
            return null;
        }

        FrameCryptoUtility.UnscrambleHeaderOnly(work);
        return counter;
    }

    private async Task DispatchPreKeyedAsync(byte[] decoded, ushort counter, IPEndPoint remote, string remoteAddress)
    {
        var frame = MessageCodecUtility.DecodeFrame(decoded);
        var first = frame.Messages.Count > 0 ? frame.Messages[0] : null;

        // An unknown peer sending a handshake gets its session provisioned
        // before the message reaches its handler. A draining host takes none: the
        // peers in the match it is still hosting are the ones it waits for, and a
        // new one would only be a joiner whose host is about to leave.
        var session = sessions.Get(remoteAddress);
        if (session is null &&
            first is not null &&
            decoded.Length == 44 &&
            first.Type == UdpCommandConstants.Handshake)
        {
            if (draining)
            {
                logger.LogDebug("Dropping handshake from {RemoteAddress}: this host is draining", remoteAddress);
            }
            else
            {
                ProvisionSession(decoded, remote, remoteAddress);
                session = sessions.Get(remoteAddress);
            }
        }

        if (session is null)
        {
            var note = first is null ? string.Empty : $" type={first.Type:x4}";
            logger.LogDebug("Inbound pre-keyed frame from {RemoteAddress}{Note}; dropped", remoteAddress, note);
            return;
        }

        var isHandshake = decoded.Length == 44 && first is not null && first.Type == UdpCommandConstants.Handshake;

        foreach (var message in frame.Messages)
        {
            await DispatchMessageAsync(message, remote, session);
        }

        // Pre-keyed frames advance the cumulative window but are not
        // acknowledged; the first keyed acknowledgement covers them.
        if (!isHandshake)
        {
            TrackInbound(session, counter);
        }
    }

    /// <summary>
    /// Advances the cumulative window without transmitting. Pre-keyed frames
    /// and frames whose only content is acknowledgement entries use this, because
    /// acknowledging an acknowledgement would ping-pong forever.
    /// </summary>
    /// <param name="session">Session to advance.</param>
    /// <param name="counter">Counter of the frame that arrived.</param>
    private static void TrackInbound(PeerSession session, ushort counter)
    {
        var sequence = (ushort)(counter & UdpCommandConstants.CounterMask);
        if (sequence > session.LastInboundSequence)
        {
            session.LastInboundSequence = sequence;
        }
    }

    private async Task DispatchKeyedAsync(
        byte[] decoded,
        ushort counter,
        uint sessionKey,
        PeerSession session,
        IPEndPoint remote)
    {
        var frame = MessageCodecUtility.DecodeFrame(decoded);
        session.LastSeenAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        session.Established = true;
        logger.LogDebug("Inbound keyed frame counter={Counter} key={SessionKey:x8}", counter, sessionKey);

        var acknowledgementEntries = 0;
        foreach (var message in frame.Messages)
        {
            // An acknowledgement entry closes the matching outbound frame: the
            // sender's reliable-class identifier with a one-byte zero body. The
            // length guard matters, because the profile record shares the class.
            var isAcknowledgementEntry =
                (message.Type & 0xf000) == UdpCommandConstants.AcknowledgementClass &&
                message.Length == 1 &&
                message.Body.Length == 1 &&
                message.Body[0] == 0;

            if (isAcknowledgementEntry)
            {
                if (session.SeenAcknowledgements.Add(message.Type))
                {
                    logger.LogDebug(
                        "Acknowledgement for sequence {Sequence} received (flags={Flags})",
                        message.Type & UdpCommandConstants.AcknowledgementIdentifierMask,
                        message.Flags);
                }

                acknowledgementEntries++;
                continue;
            }

            await DispatchMessageAsync(message, remote, session);
        }

        // A frame whose only content is acknowledgement entries must not be
        // acknowledged, because acknowledgements of acknowledgements cycle.
        if (acknowledgementEntries == frame.Messages.Count)
        {
            TrackInbound(session, counter);
        }
        else
        {
            AcknowledgeInbound(session, counter);
        }
    }

    /// <summary>
    /// Acknowledges the peer's frame. Every newly observed sequence gets exactly
    /// one acknowledgement, and a duplicate is not re-acknowledged.
    /// </summary>
    /// <param name="session">Session to acknowledge.</param>
    /// <param name="counter">Counter of the frame that arrived.</param>
    private void AcknowledgeInbound(PeerSession session, ushort counter)
    {
        var sequence = (ushort)(counter & UdpCommandConstants.CounterMask);
        if (sequence <= session.LastInboundSequence)
        {
            return;
        }

        session.LastInboundSequence = sequence;
        session.OutboundCounter = (ushort)((session.OutboundCounter + 1) & 0xffff);
        var outboundCounter = session.OutboundCounter;

        var plain = FrameBuilderUtility.BuildMessageFrame(outboundCounter,
        [
            FrameBuilderUtility.MessageOf(
                (ushort)(UdpCommandConstants.AcknowledgementClass | session.LastInboundSequence),
                UdpCommandConstants.AcknowledgementBody,
                UdpCommandConstants.AcknowledgementFirstAttempt),
        ]);

        var key = session.Established ? session.SessionKey : UdpCryptoKeyConstants.PreHandshakeKey;
        var digestKey = session.Established
            ? session.SessionKey ^ UdpCryptoKeyConstants.TailDigestKey
            : UdpCryptoKeyConstants.TailDigestKey;

        Send(FrameCryptoUtility.EncodeFrame(plain, outboundCounter, key, digestKey), session.DialBack);
        logger.LogDebug(
            "Acknowledged counter={Counter} sequence={Sequence} to {DialBack}",
            outboundCounter,
            session.LastInboundSequence,
            session.DialBack);
    }

    /// <summary>
    /// Provisions a session from a decoded handshake datagram: the peer
    /// identifier and the counter base come from the handshake body, and the
    /// session key is the two counter bases combined.
    /// </summary>
    /// <param name="decoded">Decoded handshake frame.</param>
    /// <param name="remote">Endpoint the handshake came from.</param>
    /// <param name="remoteAddress">Endpoint formatted as "address:port".</param>
    private void ProvisionSession(byte[] decoded, IPEndPoint remote, string remoteAddress)
    {
        // After the two-byte header and the four-byte message header, the body
        // starts at six: the peer identifier then the counter base.
        var peerIdentifier = BinaryUtility.ReadUInt32LittleEndian(decoded, 6);
        var counterBase = BinaryUtility.ReadUInt32LittleEndian(decoded, 10);

        sessions.Put(new PeerSession
        {
            RemoteAddress = remoteAddress,
            DialBack = remote,
            CounterBase = counterBase,
            OutboundCounter = 0,
            SessionKey = counterBase ^ hostIdentity.CounterBase,
            PeerIdentifier = peerIdentifier,
            HostPeerIdentifier = hostIdentity.PeerIdentifier,
            Established = false,
            LastSeenAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LastInboundSequence = 0,
        });
    }

    /// <param name="message">Message to hand to its handler.</param>
    /// <param name="remote">Endpoint the message arrived from.</param>
    /// <param name="session">
    /// Session the frame was decoded against. It is passed in rather than looked
    /// up again by the observed source: a peer that advertised a different
    /// endpoint than its handshake arrived from - a hairpining router, or a load
    /// balancer - sends every later frame from the advertised one, so a second
    /// lookup by source alone misses the session and drops the message silently.
    /// </param>
    private async Task DispatchMessageAsync(UdpMessage message, IPEndPoint remote, PeerSession session)
    {
        var remoteAddress = $"{remote.Address}:{remote.Port}";

        var handlerType = registry.ResolveHandlerType(message.Type);
        if (handlerType is null)
        {
            // A warning and not a debug line: a type the host cannot answer is
            // a gap in the protocol table, not per-frame chatter. At Debug it
            // was invisible in the noise, and a join that stalls on an
            // unhandled type logged nothing at all while the joiner waited.
            logger.LogWarning(
                "No handler for peer message type {MessageType:x4} from {RemoteAddress}: {BodyLength} bytes dropped",
                message.Type,
                remoteAddress,
                message.Body.Length);
            return;
        }

        var handler = (IPeerCommandHandler)serviceProvider.GetRequiredService(handlerType);
        var context = new PeerContext(
            session,
            message,
            remote,
            port,
            (type, body) =>
            {
                SendMessage(session, type, body);
                return Task.CompletedTask;
            },
            (type, body) =>
            {
                SendToOthers(session, type, body);
                return Task.CompletedTask;
            });

        await handler.HandleAsync(context);
    }
}
