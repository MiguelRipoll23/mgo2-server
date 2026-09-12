using System.Net;
using System.Net.Sockets;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The dispatch half of the Gameplay server: it classifies an inbound datagram,
/// removes its frame chain, hands the messages to their handlers and keeps the
/// cumulative acknowledgement window.
/// </summary>
public sealed partial class GameplayServerService
{
    private async Task HandleDatagramAsync(byte[] wire, IPEndPoint remote)
    {
        var remoteAddress = $"{remote.Address}:{remote.Port}";
        var work = wire.ToArray();

        // The digest key classifies the frame before the chain is removed:
        // pre-keyed frames verify with the bare constant, keyed frames with the
        // session key combined with it.
        var session = sessions.Get(remoteAddress);
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

        logger.LogWarning("Undecodable datagram from {RemoteAddress}", remoteAddress);
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
        // before the message reaches its handler.
        if (sessions.Get(remoteAddress) is null &&
            first is not null &&
            decoded.Length == 44 &&
            first.Type == UdpCommandConstants.Handshake)
        {
            ProvisionSession(decoded, remote, remoteAddress);
        }

        var session = sessions.Get(remoteAddress);
        if (session is null)
        {
            var note = first is null ? string.Empty : $" type={first.Type:x4}";
            logger.LogDebug("Inbound pre-keyed frame from {RemoteAddress}{Note}; dropped", remoteAddress, note);
            return;
        }

        var isHandshake = decoded.Length == 44 && first is not null && first.Type == UdpCommandConstants.Handshake;

        foreach (var message in frame.Messages)
        {
            await DispatchMessageAsync(message, remote);
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

            await DispatchMessageAsync(message, remote);
        }

        // A frame whose only content is acknowledgement entries must not be
        // acknowledged, because acknowledgements of acknowledgements cycle.
        if (acknowledgementEntries == frame.Messages.Count)
        {
            TrackInbound(session, counter);
        }
        else
        {
            AcknowledgeInbound(session, counter, remote);
        }
    }

    /// <summary>
    /// Acknowledges the peer's frame. Every newly observed sequence gets exactly
    /// one acknowledgement, and a duplicate is not re-acknowledged.
    /// </summary>
    /// <param name="session">Session to acknowledge.</param>
    /// <param name="counter">Counter of the frame that arrived.</param>
    /// <param name="remote">Endpoint the acknowledgement is written to.</param>
    private void AcknowledgeInbound(PeerSession session, ushort counter, IPEndPoint remote)
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

        Send(FrameCryptoUtility.EncodeFrame(plain, outboundCounter, key, digestKey), remote);
        logger.LogDebug(
            "Acknowledged counter={Counter} sequence={Sequence}",
            outboundCounter,
            session.LastInboundSequence);
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

    private async Task DispatchMessageAsync(UdpMessage message, IPEndPoint remote)
    {
        var remoteAddress = $"{remote.Address}:{remote.Port}";
        var session = sessions.Get(remoteAddress);
        if (session is null)
        {
            return;
        }

        var handlerType = registry.ResolveHandlerType(message.Type);
        if (handlerType is null)
        {
            logger.LogDebug("No handler for peer message type {MessageType:x4}", message.Type);
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
                SendMessage(session, type, body, remote);
                return Task.CompletedTask;
            });

        await handler.HandleAsync(context);
    }

    /// <summary>Writes a message to a peer through the session's shared counter.</summary>
    /// <param name="session">Session to write through.</param>
    /// <param name="messageType">Type of the message.</param>
    /// <param name="body">Body of the message.</param>
    /// <param name="remote">Endpoint the message is written to.</param>
    private void SendMessage(PeerSession session, ushort messageType, byte[] body, IPEndPoint remote)
    {
        var counter = session.OutboundCounter;
        session.OutboundCounter = (ushort)((counter + 1) & 0xffff);

        var plain = FrameBuilderUtility.BuildMessageFrame(counter, [FrameBuilderUtility.MessageOf(messageType, body)]);
        var key = session.Established ? session.SessionKey : UdpCryptoKeyConstants.PreHandshakeKey;
        var digestKey = session.Established
            ? session.SessionKey ^ UdpCryptoKeyConstants.TailDigestKey
            : UdpCryptoKeyConstants.TailDigestKey;

        Send(FrameCryptoUtility.EncodeFrame(plain, counter, key, digestKey), remote);
        logger.LogDebug(
            "Outbound counter={Counter} type={MessageType:x4}{PreKeyed}",
            counter,
            messageType,
            session.Established ? string.Empty : " pre");
    }

    private void Send(byte[] data, IPEndPoint remote)
    {
        try
        {
            socket?.Send(data, data.Length, remote);
        }
        catch (SocketException exception)
        {
            logger.LogWarning("Send to {RemoteAddress} failed: {Message}", remote, exception.Message);
        }
    }
}
