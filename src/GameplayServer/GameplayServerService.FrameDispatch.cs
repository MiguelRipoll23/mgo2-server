using System.Net;
using Mgo2Server.GameplayServer.Gameplay;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Udp;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The dispatch half of the gameplay server: it classifies an inbound datagram,
/// removes its frame chain, hands the messages to their handlers and keeps the
/// cumulative acknowledgement window.
/// </summary>
public sealed partial class GameplayServerService
{
    /// <summary>
    /// The record queue each peer's inbound records are read out of.
    /// </summary>
    /// <remarks>
    /// <b>The image receives into a queue and dispatches out of it.</b>
    /// <c>P2P_QueueInboundRecord</c> (<c>0x269b00</c>) writes each record into the channel's
    /// entry buffer and the tick walks it with <c>P2P_InboundQueueBegin</c>/
    /// <c>Current</c>/<c>Next</c>, so a frame's records are handled one at a time. This host
    /// reads a frame's records into the same queue first and drains it below, which keeps the
    /// arrival order and makes a full queue visible instead of dropping the record.
    /// </remarks>
    private readonly InboundRecordQueueService inboundRecords = new();

    /// <summary>
    /// The gate a queued record passes before its handler sees it.
    /// </summary>
    /// <remarks>
    /// <b>It is <c>read_queued_p2p_message</c>'s comparison.</b> A sequenced record is handed
    /// out only when its fourth byte is the one its stream waits for, and the stream then
    /// advances; any other byte is dropped and the stream does not move, which is why the peer
    /// writes the value again. Applying it is what makes this host read its streams the way the
    /// image does rather than accepting whatever arrives.
    /// </remarks>
    private readonly InboundRecordGateService inboundGate = new();

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
        logger.LogTrace(
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
            logger.LogTrace(
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
            logger.LogTrace(
                "[{LogPrefix}] IN {RemoteAddress} passed the session tail-digest check at counter {Counter}",
                LogPrefix,
                remoteAddress,
                keyed);
            FrameCryptoUtility.RemoveChainInPlace(work, keyed, sessionKey);
            await DispatchKeyedAsync(work, keyed, sessionKey, session, remote);
            return;
        }

        // Neither endpoint lookup names the peer that answers from somewhere new,
        // and the frame is not lost: its key still does (EndpointRecovery).
        if (keyedCounter is null &&
            await TryDispatchFromMovedEndpointAsync(work, remote, remoteAddress, session))
        {
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

        // **The frame's answers free the window before anything this frame makes the host
        // write.** Decoding is where the image marks the sent entry (`decode_p2p_datagram`,
        // `0x2666c8`); the answer pass below writes records of its own, and a free taken after
        // it re-sends entries the same frame is about to acknowledge.
        retransmit.NoteAnswers(session, frame.Messages);

        // The answer pass, with the three records it deliberately does not
        // answer and the measurement behind each (`GameplayServerService.Answers.cs`).
        AnswerReliableRecords(frame, session);

        // Receive before send, which is the order the game's own tick keeps
        // (`P2P_PhaseTick_SendOperations` calls `receive_p2p_datagrams()` first): every
        // record this peer has left unanswered is written again here, so a stream that
        // lost a value recovers on the next frame rather than on its next write.
        // Only the streams this frame names are considered, which is the walk the image's
        // flush makes over the peer's own incoming datagram (`P2P_FlushChannelQueues`,
        // 0x285b58): a stream the frame says nothing about keeps its window untouched.
        var unanswered = retransmit.NoteReceived(session, frame.Messages);
        if (unanswered.Count > 0)
        {
            SendMessages(session, unanswered);
        }

        // **The frame's records go into the channel's queue, and are read back out of it.**
        // The image's receive path does exactly this (`P2P_QueueInboundRecord`, `0x269b00`)
        // and its tick drains the entries one per turn, so a record is handled where it sits
        // in arrival order rather than in the order a frame happened to list it.
        var accepted = 0;
        var ended = false;
        while (accepted < frame.Messages.Count && !ended)
        {
            var next = frame.Messages[accepted];
            if (!inboundRecords.TryQueue(
                    session.RemoteAddress,
                    new QueuedRecord(next.Type, next.Flags, next.Body)))
            {
                // The queue is the image's own back-pressure: it holds what its capacity
                // holds and refuses past it. A record that does not fit is refused rather
                // than allocated for, and the peer re-sends it because it went unanswered.
                logger.LogWarning(
                    "[{LogPrefix}] IN {RemoteAddress} inbound record queue is full; {Remaining} record(s) of this frame are refused",
                    LogPrefix,
                    session.RemoteAddress,
                    frame.Messages.Count - accepted);
                break;
            }

            accepted++;

            // **The tick drains what it has queued, one record at a time.** The image's
            // receive path queues a record and its tick reads the queue before the next
            // one arrives, so its buffer holds a record and never a whole burst: queueing
            // a frame first and draining it afterwards filled the 0x578 buffer and refused
            // 74 and 160 records of the client's join burst (2026-11-16), which are records
            // it then had to write again to be heard at all.
            while (inboundRecords.TryDequeue(session.RemoteAddress, out var queued))
            {
                // The reader's own gate, before any handler runs: a sequenced record whose byte is
                // not the one this stream is waiting for is dropped where it sits.
                if (!inboundGate.TryTake(session.RemoteAddress, queued.Type, queued.Flags))
                {
                    logger.LogDebug(
                        "[{LogPrefix}] IN {RemoteAddress} refused 0x{MessageType:x4} ordinal 0x{Ordinal:x2}: the stream is waiting for a different byte",
                        LogPrefix,
                        session.RemoteAddress,
                        queued.Type,
                        queued.Flags);
                    continue;
                }

                var message = new UdpMessage(
                    queued.Type,
                    (byte)(queued.Body.Length + 1),
                    queued.Flags,
                    queued.Body);

                // The client's own leave notice, and the last thing it writes. It is
                // below the tick threshold, so without this it would be relayed as an
                // unnamed gameplay record; it closes the session here instead, which is
                // what lets the player join again at once
                // (UdpCommandConstants.LeaveGame).
                if (IsLeaveNotice(message))
                {
                    LeavePeer(session);
                    ended = true;
                    break;
                }

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

                // A record-level answer is the peer saying it has one of ours, and
                // needs no reply. It used to reach the dispatcher as an unhandled
                // record, so the one acknowledgement that says a roster was
                // accepted was logged as a dropped message.
                if (PeerAcknowledgementUtils.IsRecordAcknowledgement(message))
                {
                    var recordType = (ushort)(message.Type & ~PeerAcknowledgementUtils.AcknowledgementBit);
                    var firstAnswer = session.NoteAcknowledgement(recordType, message.Flags);
                    if (firstAnswer)
                    {
                        logger.LogDebug(
                            "[{LogPrefix}] IN {RemoteAddress} acknowledged record 0x{RecordType:x4} ordinal {Ordinal}",
                            LogPrefix,
                            session.RemoteAddress,
                            recordType,
                            message.Flags);
                    }

                    // The window was freed by this answer before the frame's re-sends were
                    // taken (`StreamRetransmitService.NoteReceived`), which is the image's
                    // order: an answer marks, the flush then re-appends only what is left.
                    // A timed answer carries the peer's own duration in its one body byte; the
                    // margin left over is what its re-send cadence is paced by.
                    if (timedExchange.NoteAnswer(session, message) is { } margin)
                    {
                        pacing.PaceFromRemainder(session, margin);
                    }

                    continue;
                }

                // A client's own position is a tick record with no handler of its own:
                // it is relayed below and, until now, forgotten. Keeping it is what lets
                // the room's bots be written where the player actually is
                // (PlayerPositionService).
                var firstPosition = false;
                if (message.Flags == PlayerPosition.AttributeClass &&
                    message.Body.Length == PlayerPosition.BodyLength)
                {
                    firstPosition = !positions.TryGet(session.RemoteAddress, out _);
                    positions.Note(session.RemoteAddress, PlayerPosition.FromBody(message.Body));
                }

                if (message.Type < UdpCommandConstants.TickRecordThreshold &&
                    RecordDispatcherService.Resolve(registry, message.Type) is null)
                {
                    // Records in the gameplay range are the live game's tick
                    // traffic — a player's position, vitals and the host's own
                    // stream. A dedicated host has no handler of its own for them
                    // and relays them to the room's other players unchanged, which
                    // is how one client sees another move; dropping them is what
                    // left two joined clients unable to see each other. The relay
                    // keeps the type, body and fourth byte, and forwards the
                    // identifiers no handler names as well, because the capture has
                    // more tick types than this server decodes. Nothing is answered
                    // in return, and a peer that has not finished its handshake is
                    // not a recipient yet.

                    // A client's hit and the actor its CQC hold names are the two tick
                    // records this host acts on rather than relays; the relay below writes
                    // them on to the room either way (`GameplayServerService.TickCombat.cs`).
                    NoteCombatTickRecord(message, session.RemoteAddress);

                    if (session.Established)
                    {
                        logger.LogTrace(
                            "[{LogPrefix}] IN {RemoteAddress} relayed tick record 0x{MessageType:x4} " +
                            "({BodyLength} bytes, ordinal={Ordinal}, body={Body}) to the other established peers",
                            LogPrefix,
                            session.RemoteAddress,
                            message.Type,
                            message.Body.Length,
                            message.Flags,
                            Convert.ToHexString(message.Body).ToLowerInvariant());
                        SendToOthers(session, message.Type, message.Body, message.Flags, ofThisHost: false);

                        // A player's first position is the evidence that the player
                        // has spawned; its entry follows the relay
                        // (`pcapng2` t+265.622, t+312.506).
                        await AnnounceSpawnIfFirstAsync(session, firstPosition);
                    }

                    continue;
                }

                await DispatchMessageAsync(message, remote, session);
            }
        }

        // The frame counter is tracked as well as the records being
        // acknowledged: it is what the peer's own window is checked against, and
        // the acknowledgement above names a record sequence rather than it.
        TrackInbound(session, counter);
    }

}
