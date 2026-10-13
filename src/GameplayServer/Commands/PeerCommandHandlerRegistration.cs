using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Udp;
using Mgo2Server.GameplayServer.Commands.Peer.Connection;
using Mgo2Server.GameplayServer.Commands.Peer.Gameplay;
using Mgo2Server.GameplayServer.Commands.Peer.Room;
using Mgo2Server.GameplayServer.Gameplay;
using Mgo2Server.GameplayServer.MatchRecords;
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
        // Join-probe records the client sends on reconnection attempts after a
        // stalled join. Recognised so they are not dropped as unknown commands;
        // not answered because the server cannot read what they mean.
        registry.Register<JoinProbeHandler>(UdpCommandConstants.JoinProbe);

        // A client's readiness report travels on the round's own payload record,
        // which alternates between two values as rounds succeed each other
        // (0x1ade, then 0x12de a 0x800 bit along). Both are registered so the
        // report reaches MatchStartRunService rather than being dropped as an
        // unknown command; the handler reads the body, because the same identifier
        // carries the round's other payloads, which belong on the relay and not
        // to a round.
        foreach (var identifier in MatchRecordGenerationUtils.ReadinessReportIdentifiers)
        {
            registry.Register<PlayerReadyHandler>(identifier);
        }

        // The presence record, sent in the same datagram as the report and
        // carrying the player's own lobby slot. Below the tick threshold, so
        // without a handler it would be relayed as an unnamed gameplay record;
        // registering it makes the relay a decision rather than a default, and
        // its answer rides the run.
        foreach (var identifier in MatchRecordGenerationUtils.PlayerPresenceIdentifiers)
        {
            registry.Register<PlayerPresenceHandler>(identifier);
        }

        // The players' vitals, the two identifiers that carry one player's health
        // and stamina each. They are tick records the relay would otherwise pass
        // on unread, and reading them is what gives the host a value to know: the
        // handler keeps the last one and still relays the record.
        registry.Register<PlayerVitalsHandler>(PlayerVitalsRecordUtility.FirstPlayerType);
        registry.Register<PlayerVitalsHandler>(PlayerVitalsRecordUtility.SecondPlayerType);

        // The operations stream, which carries the round's clock and state block
        // and, under opcode 0x09, the kill feed. Both rounds' sets are registered
        // and both class-bit forms, because a round plays under the set its own
        // report arrived on. The handler reads the body: the identifier cannot
        // tell a kill feed from the clock beside it.
        foreach (var identifier in MatchRecordGenerationUtils.OperationsIdentifiers)
        {
            registry.Register<KillFeedHandler>(identifier);
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
        services.AddSingleton<PlayerPresenceHandler>();
        services.AddSingleton<PlayerVitalsHandler>();
        services.AddSingleton<KillFeedHandler>();
        return services;
    }
}
