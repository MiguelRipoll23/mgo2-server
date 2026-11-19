using System.Net;
using System.Net.Sockets;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Udp;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The outbound half of the gameplay server: it writes a message to one peer,
/// or to every peer but the one it came from, each through its own session
/// counter and dial-back endpoint.
/// </summary>
public sealed partial class GameplayServerService
{
    /// <summary>
    /// Writes a message to every established peer of the room except the one it
    /// came from. A session that is established but still has no roster entry is
    /// skipped: it has contacted the host but has not joined, and a record sent to
    /// it before its own roster answer is a record it has no player table to place.
    /// </summary>
    /// <param name="origin">Session the message came from, which is left out.</param>
    /// <param name="messageType">Type of the message.</param>
    /// <param name="body">Body of the message.</param>
    /// <param name="ordinal">Fourth byte of the record.</param>
    /// <param name="ofThisHost">Whether the record is this host's own rather than one relayed from a peer.</param>
    private void SendToOthers(
        PeerSession origin,
        ushort messageType,
        byte[] body,
        byte ordinal,
        bool ofThisHost = true)
    {
        var recipients = sessions.Snapshot()
            .Where(session => session.Established
                && !ReferenceEquals(session, origin)
                && (roster.Find(session.RemoteAddress) is not null
                    || roster.Find(session.DialBack.ToString()) is not null))
            .ToList();
        logger.LogTrace(
            "[{LogPrefix}] Broadcasting 0x{MessageType:x4} ({BodyLength} body bytes, ordinal={Ordinal}) " +
            "from {Origin} to {RecipientCount} established peers",
            LogPrefix,
            messageType,
            body.Length,
            ordinal,
            origin.DialBack,
            recipients.Count);

        foreach (var session in recipients)
        {
            SendMessage(session, messageType, body, ordinal, ofThisHost);
        }
    }

    /// <summary>
    /// Writes a host message to the peers a caller names, each through its own
    /// session counter. The origin is left out even when its endpoint is named.
    /// </summary>
    /// <param name="origin">Session the message came from, which is left out.</param>
    /// <param name="recipients">Roster endpoints that should receive the record.</param>
    /// <param name="messageType">Type of the message.</param>
    /// <param name="body">Body of the message.</param>
    /// <param name="ordinal">Fourth byte of the record.</param>
    private void SendToPeers(
        PeerSession origin,
        IReadOnlyCollection<string> recipients,
        ushort messageType,
        byte[] body,
        byte ordinal)
    {
        if (recipients.Count == 0)
        {
            return;
        }

        var targets = sessions.Snapshot()
            .Where(session => session.Established
                && !ReferenceEquals(session, origin)
                && (recipients.Contains(session.RemoteAddress)
                    || recipients.Contains(session.DialBack.ToString()))
                && (roster.Find(session.RemoteAddress) is not null
                    || roster.Find(session.DialBack.ToString()) is not null))
            .ToList();
        logger.LogDebug(
            "[{LogPrefix}] Sending 0x{MessageType:x4} ({BodyLength} body bytes, ordinal={Ordinal}) " +
            "from {Origin} to {RecipientCount} named peer(s)",
            LogPrefix,
            messageType,
            body.Length,
            ordinal,
            origin.DialBack,
            targets.Count);

        foreach (var session in targets)
        {
            SendMessage(session, messageType, body, ordinal);
        }
    }

    /// <summary>
    /// Writes a message to a peer through the session's shared counter, to the
    /// endpoint the session dials back rather than to the address a datagram
    /// happened to arrive from. The two differ whenever the host sits behind a
    /// load balancer, and only the first is a socket the peer is reading.
    /// </summary>
    /// <remarks>
    /// The counter is fifteen bits and the header's bit 15 is the compression
    /// marker, so the increment wraps at <c>0x7fff</c> rather than at
    /// <c>0xffff</c>. Wrapping at sixteen bits would let the counter reach
    /// <c>0x8000</c> and mark a plaintext frame as compressed, which the peer
    /// would then try to decompress.
    /// </remarks>
    /// <param name="session">Session to write through.</param>
    /// <param name="messageType">Type of the message.</param>
    /// <param name="body">Body of the message.</param>
    /// <param name="ordinal">Fourth byte of the record.</param>
    /// <param name="ofThisHost">Whether the record is this host's own rather than one relayed from a peer.</param>
    private void SendMessage(
        PeerSession session,
        ushort messageType,
        byte[] body,
        byte ordinal = 0,
        bool ofThisHost = true)
    {
        SendMessages(session, [FrameBuilderUtility.MessageOf(messageType, body, ordinal)], ofThisHost: ofThisHost);
    }

    /// <summary>
    /// Writes several messages to a peer, as few datagrams as they fit in on
    /// one outbound counter each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The recorded host keeps a roster run together rather than writing it one
    /// record per datagram: sending those one per frame spends an outbound
    /// sequence each and reaches the peer as a scatter of frames rather than the
    /// exchange it reads.
    /// </para>
    /// <para>
    /// <b>Together, but only as far as a datagram reaches.</b> The records are appended to
    /// the datagram under construction while the next one's aligned size still fits, and the
    /// rest start the next frame, in order, each on its own counter — the image's own rule
    /// (<c>append_udp_datagram_to_send_buffer</c>, <c>0x2696d8</c>, reproduced by
    /// <see cref="OutboundDatagramBufferService"/>). The whole run is written under the
    /// session's one gate, so no other write can land between the frames of a run.
    /// </para>
    /// </remarks>
    /// <param name="session">Session to write through.</param>
    /// <param name="messages">Messages to write, in order.</param>
    /// <param name="ofThisHost">
    /// Whether this host built the records. Everything it writes is its own but
    /// the relay, which is a peer's record passed on — a distinction a caller
    /// keeps when it wants the log to say which of the two it wrote.
    /// </param>
    private void SendMessages(
        PeerSession session,
        IReadOnlyList<UdpMessage> messages,
        bool ofThisHost = true)
    {
        if (messages.Count == 0)
        {
            return;
        }

        // A record carrying the timed class opens an exchange whose answer says how far
        // behind the peer is, and that margin is what paces this peer's re-sends.
        timedExchange.NoteWritten(session, messages);

        lock (session.OutboundGate)
        {
            // **The fourth byte is stamped here, under the one gate the session writes
            // through.** `P2P_SendRecord` (0x26b778) reads the stream's byte from the
            // record's channel entry, stores it in the record it is about to queue, and
            // increments it in the same call — the number and the wire order are one
            // sequence because both happen inside that call. Taking the number where a run
            // is built instead lets two publishers number one stream in the order they
            // happened to build and write it in another, which is the hole the client
            // refuses everything behind (`docs/GHIDRA.md`, `stream-sequencing.md` D3).
            messages = NumberOwnRecords(session, messages, ofThisHost);

            // A record the peer has not answered is a hole in its stream, and the peer takes
            // a stream's records only in order: the window's re-sends are put in front of
            // this write so the frame carries them in the order the peer reads them. The
            // window sees stamped records, so the byte it holds is the byte the peer answers.
            messages = retransmit.NoteWriting(session, messages);

            // The image's own packing: each record appended while its aligned size still fits,
            // the rest starting the next datagram (`append_udp_datagram_to_send_buffer`).
            foreach (var group in OutboundDatagramBufferService.Split(messages))
            {
                SendFrame(session, group);
            }
        }
    }

    /// <summary>
    /// Stamps this host's numbered records with the fourth byte their stream will carry,
    /// in the order they are about to be written.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b>Only this host's own records are numbered.</b> An answer already carries the byte
    /// of the record it answers and must echo it, and a relayed record is the peer's, not
    /// this host's — its stream is the peer's own and renumbering it would hand the room a
    /// byte the peer never sent.
    /// </p>
    /// <p>
    /// <b>The roster exchange is the stream stamped here.</b> Its records come from several
    /// publishers — the join's run, the scoreboard, the ticket count, the team arrays — and
    /// its counter is read before this gate, so a record can be numbered in build order and
    /// written in another. That is the run the client refuses with everything behind it, and
    /// the reason the number belongs to the send path. The other streams are numbered by
    /// <c>MatchRecordSequenceService</c> when their frames are built, and are left as they
    /// are.
    /// </p>
    /// <p>
    /// <b>A record below the numbered range or already carrying the answer bit is left
    /// alone.</b> The tick is numbered by its class rather than by a counter, the handshake
    /// has no place in a stream, and the frame acknowledgement is not a record. The image
    /// gates on the same bit and descriptor (`P2P_SendRecord`, 0x26b778).
    /// </p>
    /// </remarks>
    /// <param name="session">Session the records are written through.</param>
    /// <param name="messages">Records of the write, in order.</param>
    /// <param name="ofThisHost">Whether this host built the records.</param>
    /// <returns>The records with their fourth bytes stamped, or the same list when none needed one.</returns>
    private static IReadOnlyList<UdpMessage> NumberOwnRecords(
        PeerSession session,
        IReadOnlyList<UdpMessage> messages,
        bool ofThisHost)
    {
        if (!ofThisHost)
        {
            return messages;
        }

        List<UdpMessage>? numbered = null;
        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];
            if (message.FixedOrdinal
                || MatchRecords.MatchRecordSequenceService.StreamOf(message.Type)
                    != MatchRecords.MatchRecordSequenceService.StreamOf(
                        UdpCommandConstants.PlayerProfile)
                || !PeerAcknowledgementUtils.WaitsForAcknowledgement(message))
            {
                continue;
            }

            numbered ??= [.. messages];
            numbered[index] = message with
            {
                Flags = session.TakeStreamOrdinal(
                    MatchRecords.MatchRecordSequenceService.StreamOf(message.Type)),
            };
        }

        return numbered ?? messages;
    }

    /// <summary>
    /// Writes one group of messages as one datagram on the session's next
    /// outbound counter.
    /// </summary>
    /// <param name="session">Session to write through.</param>
    /// <param name="messages">Messages of the frame, in order.</param>
    private void SendFrame(PeerSession session, IReadOnlyList<UdpMessage> messages)
    {
        var counter = session.OutboundCounter;
            session.OutboundCounter = FrameCounterUtility.Next(counter);

            var plain = FrameBuilderUtility.BuildMessageFrame(counter, messages);
            var key = session.Established ? session.SessionKey : UdpCryptoKeyConstants.PreHandshakeKey;
            var digestKey = session.Established
                ? session.SessionKey ^ UdpCryptoKeyConstants.TailDigestKey
                : UdpCryptoKeyConstants.TailDigestKey;

            var wire = FrameCryptoUtility.EncodeFrame(plain, counter, key, digestKey);

            // Each record's instant is kept so the peer's answer to it can measure a
            // round trip (`PeerSession.RoundTripMilliseconds`); a client answers one
            // record per answer, which is what pairs them.
            foreach (var message in messages)
            {
                session.NoteSent(message.Type, message.Flags);
            }

            // Every record of every frame, at the level a run is read at. Without this the
            // only writes visible at Debug are the roster stream's, so a join that stalls
            // cannot be told from a join whose records are simply not logged.
            logger.LogDebug(
                "[{LogPrefix}] OUT {RemoteAddress} frame {Counter} carries {Count} record(s): {Records}",
                LogPrefix,
                session.DialBack,
                counter,
                messages.Count,
                string.Join(
                    ", ",
                    messages.Select(message =>
                        $"0x{message.Type:x4}/{message.Flags:x2} ({message.Body.Length}B)")));

            // The roster stream's writes are logged with their identifiers, because every
            // scoreboard, ticket and array record rides it: a value the peer never answers
            // can only be found by putting what was written beside what was acknowledged.
            // A stream that has spent a value it never wrote refuses everything until its
            // counter comes round to the missing byte, which is minutes at this rate.
            foreach (var message in messages)
            {
                if (MatchRecords.MatchRecordSequenceService.StreamOf(message.Type) == 0x001)
                {
                    logger.LogDebug(
                        "[{LogPrefix}] OUT {RemoteAddress} roster write 0x{MessageType:x4} ordinal 0x{Ordinal:x2}",
                        LogPrefix,
                        session.DialBack,
                        message.Type,
                        message.Flags);
                }
            }
            logger.LogTrace(
                "[{LogPrefix}] OUT {RemoteAddress} building frame counter={Counter}, compression=uncompressed, " +
                "keyed={Keyed}, records=[{Records}]",
                LogPrefix,
                session.DialBack,
                counter,
                session.Established,
                string.Join(", ", messages.Select(message =>
                    $"0x{message.Type:x4}/{message.Body.Length}B/ordinal={message.Flags} "
                    + $"body={Convert.ToHexString(message.Body).ToLowerInvariant()}")));
            Send(wire, session.DialBack);
            TrafficLogger.LogUdpOutboundPacket(
                logger,
                LogPrefix,
                $"frame counter={counter} messages={messages.Count}" +
                $"{(session.Established ? string.Empty : " pre-keyed")} " +
                $"compression=uncompressed to {session.DialBack}",
                wire);
    }

    private void Send(byte[] data, IPEndPoint remote)
    {
        try
        {
            socket?.Send(data, data.Length, remote);
        }
        catch (SocketException exception)
        {
            logger.LogWarning(
                "[{LogPrefix}] OUT {RemoteAddress} send failed: {Message}",
                LogPrefix,
                remote,
                exception.Message);
        }
    }
}
