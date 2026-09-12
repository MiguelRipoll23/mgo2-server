using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Mgo2Server.GateLobbyServer.Commands;

/// <summary>Binds the command identifiers of the gate to the handlers that serve them.</summary>
public static class GateCommandHandlerRegistration
{
    /// <summary>Registers every gate command handler.</summary>
    /// <param name="registry">Registry to register with.</param>
    public static void RegisterCommandHandlers(CommandRegistry registry)
    {
        registry.Register<GetLobbyListHandler>(ServerType.Gate, CommandConstants.GetLobbyList);
        registry.Register<GetNewsHandler>(ServerType.Gate, CommandConstants.GetNews);
    }

    /// <summary>Registers every gate command handler with the container.</summary>
    /// <param name="services">Container to register with.</param>
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddTransient<GetLobbyListHandler>();
        services.AddTransient<GetNewsHandler>();
        return services;
    }
}
