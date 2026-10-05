using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Udp;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;

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
        registry.Register<PeerKeepAliveHandler>(UdpCommandConstants.KeepAlive);
        registry.Register<PlayerProfileHandler>(UdpCommandConstants.PlayerProfile);
        // The tag a live joiner opens with. It carries the same player record
        // as the roster type, so it is answered the same way; registering only
        // the roster type is what left a join with no handler and no roster.
        registry.Register<PlayerProfileHandler>(UdpCommandConstants.JoinRequest);
        // The two record types a running game exchanges in volume and nobody
        // has decoded. They are registered so they stop arriving as unknown
        // commands; neither is answered, because neither is understood.
        registry.Register<InGameControlHandler>(UdpCommandConstants.RosterHead);
        registry.Register<InGameControlHandler>(UdpCommandConstants.InGameControl);

        // The gameplay channel's tick records. Both are logged rather than
        // answered, at the levels their own docs set out: every record at debug,
        // a death or a revive again at information.
        registry.Register<PlayerVitalsHandler>(PlayerVitalsRecordUtility.FirstPlayerType);
        registry.Register<PlayerVitalsHandler>(PlayerVitalsRecordUtility.SecondPlayerType);
        foreach (var positionType in PlayerPositionRecordUtility.MeasuredTypes)
        {
            registry.Register<PlayerPositionHandler>(positionType);
        }
    }

    /// <summary>
    /// Registers every peer command handler with the container.
    /// </summary>
    /// <remarks>
    /// The two lists belong together. The dispatcher resolves a handler from the
    /// container by the type the registry named, so a handler registered above and
    /// missing here is not a type with no behaviour but a message that throws on
    /// arrival and takes the rest of its frame's messages with it — which is how
    /// the in-game control records failed here while the peer sat connected.
    /// </remarks>
    /// <param name="services">Container to register with.</param>
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddSingleton<AcceptHandshakeHandler>();
        services.AddSingleton<PeerKeepAliveHandler>();
        services.AddSingleton<PlayerProfileHandler>();
        services.AddSingleton<InGameControlHandler>();
        services.AddSingleton<PlayerVitalsHandler>();
        services.AddSingleton<PlayerPositionHandler>();
        return services;
    }
}
