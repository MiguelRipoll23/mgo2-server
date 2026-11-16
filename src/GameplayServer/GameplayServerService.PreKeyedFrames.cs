using System.Net;
using Mgo2Server.GameplayServer.Gameplay;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The pre-keyed half of the inbound path: the frames a peer writes before its session
/// is keyed, the handshake that provisions or restarts one, and the two tests the
/// dispatch makes about what such a frame's records mean.
/// </summary>
public sealed partial class GameplayServerService
{
    private async Task DispatchPreKeyedAsync(byte[] decoded, ushort counter, IPEndPoint remote, string remoteAddress)
    {
        var frame = MessageCodecUtility.DecodeFrame(decoded);
        var first = frame.Messages.Count > 0 ? frame.Messages[0] : null;

        // A handshake is the one frame a peer may open with, and it can arrive
        // with a session already held: the client re-sending a handshake this
        // host has not answered yet, or the same client starting its session
        // over. The counter base it carries is what tells those two apart, and a
        // peer starting over is retired here rather than served on a key its
        // client has forgotten (`GameplayServerService.SessionRestart.cs`).
        var isHandshake =
            first is not null &&
            first.Type == UdpCommandConstants.Handshake &&
            HandshakeShapeUtils.IsHandshakeBody(first.Body);
        var session = sessions.Get(remoteAddress);
        if (isHandshake && first is not null)
        {
            session = RestartPeerIfHandshakeStartsOver(first, remote, remoteAddress, session);
        }

        // An unknown peer sending a handshake gets its session provisioned
        // before the message reaches its handler. A draining host takes none: the
        // peers in the match it is still hosting are the ones it waits for, and a
        // new one would only be a joiner whose host is about to leave.
        if (session is null && isHandshake)
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
    /// Whether a record is the client's join report, which this pass does
    /// not answer.
    /// </summary>
    /// <remarks>
    /// The answer a report gets depends on whether it opens the round, and that
    /// is not known until it reaches <c>JoinReportHandler</c>: the report that
    /// opens the round is answered by the round's own marker, which is the same
    /// record, and answering it here as well puts two markers on the wire
    /// (<c>docs/protocol/DATA_PHASE_DELIVERY.md</c> §2). A report that does not
    /// open the round is answered by the handler itself, beside its publication.
    /// </remarks>
    /// <param name="message">Record to test.</param>
    /// <returns><c>true</c> when the record is a join report.</returns>
    private static bool IsJoinReport(UdpMessage message) =>
        MatchRecordGenerationUtils.IsJoinReportIdentifier(message.Type) &&
        MatchStartBodiesUtils.IsJoinReport(message.Body);

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
}
