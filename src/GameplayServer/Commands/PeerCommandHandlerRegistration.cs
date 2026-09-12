using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Udp;

namespace Mgo2Server.GameplayServer.Commands;

/// <summary>
/// Registers every peer command handler this server dispatches.
/// </summary>
public static class PeerCommandHandlerRegistration
{
    /// <summary>Registers the handlers of this server.</summary>
    /// <param name="registry">Registry to register with.</param>
    public static void RegisterCommandHandlers(PeerCommandRegistry registry)
    {
        registry.Register<AcceptHandshakeHandler>(UdpCommandConstants.Handshake);
        registry.Register<AcknowledgeKeepAliveHandler>(UdpCommandConstants.KeepAlive);
        registry.Register<PlayerProfileHandler>(UdpCommandConstants.PlayerProfile);
    }
}
