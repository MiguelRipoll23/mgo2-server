using System.Net;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

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
        TrafficLogger.LogUdpTraffic(logger, LogPrefix, remoteAddress, "IN", work);

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
        logger.LogDebug(
            "[{LogPrefix}] IN {RemoteAddress} looking up session: {SessionLookup} ({SessionCount} sessions)",
            LogPrefix,
            remoteAddress,
            session is null ? "none" : $"peer=0x{session.PeerIdentifier:x8}, dial-back={session.DialBack}",
            sessions.Count);
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
            logger.LogDebug(
                "[{LogPrefix}] IN {RemoteAddress} passed the pre-key tail-digest check at counter {Counter}",
                LogPrefix,
                remoteAddress,
                preCounter);
            FrameCryptoUtility.RemoveChainInPlace(work, preCounter, UdpCryptoKeyConstants.PreHandshakeKey);
            await DispatchPreKeyedAsync(work, preCounter, remote, remoteAddress);
            return;
        }

        if (keyedCounter is { } keyed && session is not null)
        {
            logger.LogDebug(
                "[{LogPrefix}] IN {RemoteAddress} passed the session tail-digest check at counter {Counter}",
                LogPrefix,
                remoteAddress,
                keyed);
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
            "[{LogPrefix}] IN {RemoteAddress} undecodable ({Length} bytes): no session is keyed on that endpoint and none dials back to it; sessions held: {DialBacks}",
            LogPrefix,
            remoteAddress,
            work.Length,
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
                logger.LogDebug(
                    "[{LogPrefix}] IN {RemoteAddress} handshake dropped: this host is draining",
                    LogPrefix,
                    remoteAddress);
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
            logger.LogDebug(
                "[{LogPrefix}] IN {RemoteAddress} pre-keyed frame{note}; dropped, no session for it",
                LogPrefix,
                remoteAddress,
                note);
            return;
        }

        var isHandshake = decoded.Length == 44 && first is not null && first.Type == UdpCommandConstants.Handshake;

        // The frame as it decoded, with its messages: the same shape the TCP
        // lobbies log, so a gameplay session reads like a lobby session.
        TrafficLogger.LogUdpInboundPacket(
            logger,
            LogPrefix,
            $"frame counter={counter}{(isHandshake ? " handshake" : " pre-keyed")} " +
            $"compression={(frame.Compressed ? "compressed" : "uncompressed")} from {remoteAddress}",
            decoded);

        foreach (var message in frame.Messages)
        {
            logger.LogDebug(
                "[{LogPrefix}] IN {RemoteAddress} dispatching pre-keyed message 0x{MessageType:x4} ({BodyLength} body bytes)",
                LogPrefix,
                remoteAddress,
                message.Type,
                message.Body.Length);
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
    /// Records how far the peer's frame counter has got, without transmitting.
    /// </summary>
    /// <remarks>
    /// The counter a frame travels under is not the sequence a peer's record is
    /// numbered by, and tracking it is what makes a stale or replayed frame
    /// visible before its records reach their handlers.
    ///
    /// The comparison is wrap-aware. A plain <c>&gt;</c> would report every
    /// frame as stale once the fifteen-bit counter had gone past 32 767.
    /// </remarks>
    /// <param name="session">Session to advance.</param>
    /// <param name="counter">Counter of the frame that arrived.</param>
    private static void TrackInbound(PeerSession session, ushort counter)
    {
        var sequence = FrameCounterUtility.Value(counter);
        if (FrameCounterUtility.IsNewer(sequence, session.LastInboundSequence))
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
        TrafficLogger.LogUdpInboundPacket(
            logger,
            LogPrefix,
            $"frame counter={counter} key={sessionKey:x8} messages={frame.Messages.Count} " +
            $"compression={(frame.Compressed ? "compressed" : "uncompressed")} from {session.DialBack}",
            decoded);

        var tickMessages = frame.Messages
            .Where(message => message.Type < UdpCommandConstants.TickRecordThreshold)
            .ToList();

        // Answer numbered records before handing them to a handler. Frame ACK
        // entries are excluded by the utility, and a valid profile is the
        // exception because its roster response starts with the same 0x5001
        // record-level answer.
        //
        // An earlier reading of this host's answers was that they cannot be what
        // the peer waits for, because a live join was served them and still
        // stalled. The measurement says otherwise once the answers are read
        // beside the capture's: the answers served then carried an empty body,
        // and every answer a 0x8000-class record draws in either capture carries
        // one byte. The peer went on building records — eighteen of them, its
        // sequence climbing a step each time — while the window's base stood
        // still, which is what a window whose base is never moved does.
        // `PeerAcknowledgementUtils` carries the rule and
        // `docs/protocol/P2P_CONNECT_FSM.md` §7.1c the measurement, including
        // what the object at `[0xc]` — the base the window advances from — is
        // written by.
        var answers = frame.Messages
            .Where(PeerAcknowledgementUtils.WaitsForAcknowledgement)
            .Where(message =>
                (message.Type & ~PeerAcknowledgementUtils.PayloadClassBit) != UdpCommandConstants.PlayerProfile ||
                PlayerProfileRecordParseUtils.Parse(message.Body) is not { Name.Length: > 0 })
            .Select(PeerAcknowledgementUtils.AcknowledgementOf)
            .ToList();
        if (answers.Count > 0)
        {
            logger.LogDebug(
                "[{LogPrefix}] OUT {RemoteAddress} answering {AnswerCount} reliable records: {MessageTypes}",
                LogPrefix,
                session.DialBack,
                answers.Count,
                string.Join(", ", answers.Select(message => $"0x{message.Type:x4}")));
        }
        SendMessages(session, answers);

        foreach (var message in frame.Messages)
        {
            // A frame acknowledgement closes an outbound frame and must not
            // itself receive a record-level answer.
            if (PeerAcknowledgementUtils.IsFrameAcknowledgement(message))
            {
                if (session.SeenAcknowledgements.Add(message.Type))
                {
                    logger.LogDebug(
                        "[{LogPrefix}] IN {RemoteAddress} acknowledgement naming sequence {Sequence} (flags={Flags})",
                        LogPrefix,
                        session.RemoteAddress,
                        message.Type & UdpCommandConstants.AcknowledgementIdentifierMask,
                        message.Flags);
                }

                continue;
            }

            if (message.Type < UdpCommandConstants.TickRecordThreshold &&
                registry.ResolveHandlerType(message.Type) is null)
            {
                // Unknown tick identifiers are still forwarded below, but they
                // are not session commands and should not flood warning logs.
                continue;
            }

            await DispatchMessageAsync(message, remote, session);
        }

        // Tick records are carried under the identifier range below 0x1000.
        // The capture shows these flowing through the dedicated server in both
        // directions; it does not show that this host simulates them. Forward
        // these records to the other established peers as one frame, preserving
        // their contents and the source frame's compression choice.
        if (tickMessages.Count > 0)
        {
            logger.LogDebug(
                "[{LogPrefix}] Relaying {TickCount} tick records from {RemoteAddress}; compression={Compression}",
                LogPrefix,
                tickMessages.Count,
                session.DialBack,
                frame.Compressed ? "compressed" : "uncompressed");
            RelayTickMessages(session, tickMessages, frame.Compressed);
        }

        // The frame counter is tracked as well as the records being
        // acknowledged: it is what the peer's own window is checked against, and
        // the acknowledgement above names a record sequence rather than it.
        TrackInbound(session, counter);
    }

}
