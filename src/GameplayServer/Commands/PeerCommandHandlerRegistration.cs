using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Udp;
using Mgo2Server.GameplayServer.Commands.Peer.Connection;
using Mgo2Server.GameplayServer.Commands.Peer.Room;
using Mgo2Server.GameplayServer.MatchRecords;
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
        // Join-probe records the client sends on reconnection attempts after a
        // stalled join. Recognised so they are not dropped as unknown commands;
        // not answered because the server cannot read what they mean.
        registry.Register<JoinProbeHandler>(UdpCommandConstants.JoinProbe);

        // A client's readiness report travels on the round's own payload record,
        // which alternates between two values as rounds succeed each other
        // (0x1ade, then 0x12de a 0x800 bit along). Both are registered so the
        // report is recognised rather than dropped as an unknown command; the
        // handler reads the body, because the same identifier carries the round's
        // other payloads, which belong on the relay and not to a round.
        foreach (var identifier in MatchRecordGenerationUtils.ReadinessReportIdentifiers)
        {
            registry.Register<PlayerReadyHandler>(identifier);
        }
    }

    /// <summary>
    /// Registers every peer command handler with the container.
    /// </summary>
    /// <remarks>
    /// The two lists belong together. The dispatcher resolves a handler from the
    /// container by the type the registry named, so a handler registered above and
    /// missing here is not a type with no behaviour but a message that throws on
    /// arrival and takes the rest of its frame's messages with it.
    /// </remarks>
    /// <param name="services">Container to register with.</param>
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddSingleton<AcceptHandshakeHandler>();
        services.AddSingleton<PeerKeepAliveHandler>();
        services.AddSingleton<PlayerProfileHandler>();
        services.AddSingleton<JoinProbeHandler>();
        services.AddSingleton<PlayerReadyHandler>();
        return services;
    }
}
