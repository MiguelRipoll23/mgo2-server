using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Mgo2Server.AccountLobbyServer.Commands;

/// <summary>Binds the command identifiers of the account server to the handlers that serve them.</summary>
public static class AccountCommandHandlerRegistration
{
    /// <summary>Registers every account command handler.</summary>
    /// <param name="registry">Registry to register with.</param>
    public static void RegisterCommandHandlers(CommandRegistry registry)
    {
        registry.Register<CheckSessionHandler>(ServerType.Account, CommandConstants.CheckSession);
        registry.Register<GetCharacterListHandler>(ServerType.Account, CommandConstants.GetCharacterList);
        registry.Register<CreateCharacterHandler>(ServerType.Account, CommandConstants.CreateCharacter);
        registry.Register<SelectCharacterHandler>(ServerType.Account, CommandConstants.SelectCharacter);
        registry.Register<DeleteCharacterHandler>(ServerType.Account, CommandConstants.DeleteCharacter);
        registry.Register<CheckCharacterNameHandler>(ServerType.Account, CommandConstants.CheckCharacterName);
    }

    /// <summary>Registers every account command handler with the container.</summary>
    /// <param name="services">Container to register with.</param>
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        services.AddTransient<CheckSessionHandler>();
        services.AddTransient<GetCharacterListHandler>();
        services.AddTransient<CreateCharacterHandler>();
        services.AddTransient<SelectCharacterHandler>();
        services.AddTransient<DeleteCharacterHandler>();
        services.AddTransient<CheckCharacterNameHandler>();
        return services;
    }
}
