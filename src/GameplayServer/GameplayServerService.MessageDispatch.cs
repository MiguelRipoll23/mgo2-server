using System.Net;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// Provisions peer sessions and dispatches decoded messages to their handlers.
/// </summary>
public sealed partial class GameplayServerService
{
    /// <summary>
    /// Provisions a session from a decoded handshake datagram.
    /// </summary>
    /// <param name="decoded">Decoded handshake frame.</param>
    /// <param name="remote">Endpoint the handshake came from.</param>
    /// <param name="remoteAddress">Endpoint formatted as "address:port".</param>
    private void ProvisionSession(byte[] decoded, IPEndPoint remote, string remoteAddress)
    {
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

        logger.LogDebug(
            "[{LogPrefix}] Created provisional session for {RemoteAddress}: peer=0x{PeerIdentifier:x8}, " +
            "counter base=0x{CounterBase:x8}",
            LogPrefix,
            remoteAddress,
            peerIdentifier,
            counterBase);
        TrafficLogger.LogPeerConnection(
            logger,
            LogPrefix,
            $"{remoteAddress} (peer {peerIdentifier}, answered on {remote})");
    }

    /// <param name="message">Message to hand to its handler.</param>
    /// <param name="remote">Endpoint the message arrived from.</param>
    /// <param name="session">Session the frame was decoded against.</param>
    private async Task DispatchMessageAsync(UdpMessage message, IPEndPoint remote, PeerSession session)
    {
        var remoteAddress = $"{remote.Address}:{remote.Port}";
        var handlerType = registry.ResolveHandlerType(message.Type);
        if (handlerType is null)
        {
            logger.LogWarning(
                "[{LogPrefix}] IN {RemoteAddress} message type {MessageType:x4}: {BodyLength} bytes dropped, no handler",
                LogPrefix,
                remoteAddress,
                message.Type,
                message.Body.Length);
            return;
        }

        var handler = (IPeerCommandHandler)serviceProvider.GetRequiredService(handlerType);
        logger.LogDebug(
            "[{LogPrefix}] IN {RemoteAddress} invoking {HandlerType} for 0x{MessageType:x4} " +
            "({BodyLength} body bytes, ordinal={Ordinal})",
            LogPrefix,
            remoteAddress,
            handlerType.Name,
            message.Type,
            message.Body.Length,
            message.Flags);
        var context = new PeerContext(
            session,
            message,
            remote,
            port,
            (type, body, ordinal) =>
            {
                SendMessage(session, type, body, ordinal);
                return Task.CompletedTask;
            },
            (type, body, ordinal) =>
            {
                SendToOthers(session, type, body, ordinal);
                return Task.CompletedTask;
            },
            records =>
            {
                SendMessages(session, records);
                return Task.CompletedTask;
            },
            records =>
            {
                SendMessages(session, records, compressed: true);
                return Task.CompletedTask;
            });

        await handler.HandleAsync(context);
        logger.LogDebug(
            "[{LogPrefix}] IN {RemoteAddress} completed {HandlerType} for 0x{MessageType:x4}",
            LogPrefix,
            remoteAddress,
            handlerType.Name,
            message.Type);
    }
}
