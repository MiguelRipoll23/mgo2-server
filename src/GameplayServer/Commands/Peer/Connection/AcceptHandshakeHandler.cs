using System.Net;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Connection;

/// <summary>
/// Accepts a joiner's handshake and sends the pre-keyed keep-alive followed by
/// the handshake reply. Later traffic uses the session key.
/// </summary>
/// <remarks>
/// The live host sends the keep-alive first as outbound counter 0, then the
/// handshake reply as counter 1. Both frames are pre-keyed; the first keyed host
/// frame is the roster response.
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
        logger.LogDebug(
            "UDP {LocalPort}: parsing handshake from {RemoteAddress} ({BodyLength} body bytes)",
            context.LocalPort,
            context.Remote,
            context.Message.Body.Length);
        var handshake = FrameBuilderUtility.ParseHandshakeBody(context.Message.Body);
        if (handshake is null)
        {
            logger.LogWarning(
                "UDP {LocalPort}: a handshake from {RemoteAddress} failed validation",
                context.LocalPort,
                context.Remote);
            return;
        }

        context.Session.PeerIdentifier = handshake.PeerIdentifier;
        context.Session.CounterBase = handshake.CounterBase;
        context.Session.SessionKey = handshake.CounterBase ^ hostIdentity.CounterBase;
        context.Session.PeerAddressData = FrameBuilderUtility.EndpointData(handshake.Pairs);

        var dialBack = AdvertisedEndpointOf(handshake) ?? context.Remote;
        context.Session.DialBack = dialBack;

        // The capability byte and the mode are what tell a joiner from a host on
        // the wire: every joining client in the captures sends mode 2 and every
        // host 1, while the one live join logged here sent 1 from a joiner. That is
        // the divergence P2P_CONNECT_FSM.md §7.1b–d records: the mode is the
        // client's own and never a value this server sends, and the receiving side
        // stores it without testing it. Logging it is what makes a failed join say
        // which role the client presented, instead of leaving it to be recovered
        // from a capture afterwards.
        logger.LogDebug(
            "UDP {LocalPort}: configured peer=0x{PeerIdentifier:x8}, counter base=0x{CounterBase:x8}, " +
            "capability=0x{Capability:x2}, mode={Mode}, advertised endpoint={AdvertisedEndpoint}, " +
            "dial-back={DialBack}",
            context.LocalPort,
            handshake.PeerIdentifier,
            handshake.CounterBase,
            handshake.Capability,
            handshake.Mode,
            string.Join(", ", handshake.Pairs.Select(pair => $"{pair.Address}:{pair.Port}")),
            dialBack);

        logger.LogInformation(
            "UDP {LocalPort}: handshake from {RemoteAddress} peer=0x{PeerIdentifier:x8} base=0x{CounterBase:x8}, answering {DialBack}",
            context.LocalPort,
            context.Remote,
            handshake.PeerIdentifier,
            handshake.CounterBase,
            dialBack);

        await context.Send(UdpCommandConstants.KeepAlive, [], 0);
        logger.LogDebug(
            "UDP {LocalPort}: sent pre-keyed opening keep-alive to {RemoteAddress}",
            context.LocalPort,
            dialBack);

        var reply = FrameBuilderUtility.BuildHandshakeBody(
            hostIdentity.PeerIdentifier,
            hostIdentity.CounterBase,
            hostIdentity.AdvertisedAddress,
            hostIdentity.AdvertisedPort);
        await context.Send(UdpCommandConstants.Handshake, reply, 0);
        logger.LogDebug(
            "UDP {LocalPort}: sent pre-keyed handshake reply to {RemoteAddress}",
            context.LocalPort,
            dialBack);

        context.Session.Established = true;

        logger.LogInformation(
            "UDP {LocalPort}: session with peer=0x{PeerIdentifier:x8} established; opening exchange pre-keyed",
            context.LocalPort,
            handshake.PeerIdentifier);
    }

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
