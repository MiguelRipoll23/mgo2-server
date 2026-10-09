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

        logger.LogInformation(
            "UDP {LocalPort}: handshake from {RemoteAddress} peer=0x{PeerIdentifier:x8} base=0x{CounterBase:x8}, answering {DialBack}",
            context.LocalPort,
            context.Remote,
            handshake.PeerIdentifier,
            handshake.CounterBase,
            dialBack);

        await context.Send(UdpCommandConstants.KeepAlive, [], 0);

        var reply = FrameBuilderUtility.BuildHandshakeBody(
            hostIdentity.PeerIdentifier,
            hostIdentity.CounterBase,
            hostIdentity.AdvertisedAddress,
            hostIdentity.AdvertisedPort);
        await context.Send(UdpCommandConstants.Handshake, reply, 0);

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
