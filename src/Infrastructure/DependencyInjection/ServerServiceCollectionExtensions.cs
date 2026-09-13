using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Domain.Authentication;
using Mgo2Server.Shared.Domain.Automatch;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Domain.Mail;
using Mgo2Server.Shared.Domain.News;
using Mgo2Server.Shared.Domain.Users;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Utils;
using Mgo2Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mgo2Server.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the options, the persistence layer, the protocol services and the
/// domain services both server executables need.
/// </summary>
public static class ServerServiceCollectionExtensions
{
    /// <summary>Registers the shared server services.</summary>
    /// <param name="services">Container to register with.</param>
    /// <param name="configuration">Configuration the settings are read from.</param>
    public static IServiceCollection AddServerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ServerOptions>(options =>
        {
            options.ListeningIpAddress =
                configuration.ReadText("LISTENING_IP") ?? options.ListeningIpAddress;
            options.LobbiesRefreshIntervalMinutes = configuration.ReadNumber(
                "LOBBIES_REFRESH_INTERVAL_MINUTES",
                options.LobbiesRefreshIntervalMinutes);
            options.GameplayServerPort = configuration.ReadNumber(
                "GAMEPLAY_SERVER_PORT",
                configuration.ReadNumber("UDP_PORT", options.GameplayServerPort));
            options.PublicHostAddress =
                configuration.ReadText("P2P_HOST") ?? options.PublicHostAddress;
            options.GameplayLobbyName = configuration.ReadText("GAMEPLAY_SERVER_LOBBY_NAME")
                ?? options.GameplayLobbyName;
            options.GameplayServerAccountName = configuration.ReadText("GAMEPLAY_SERVER_ACCOUNT_NAME")
                ?? options.GameplayServerAccountName;
            options.GameplayServerAccountPassword = configuration.ReadText("GAMEPLAY_SERVER_ACCOUNT_PASSWORD")
                ?? options.GameplayServerAccountPassword;
            options.GameplayServerCharacterName = configuration.ReadText("GAMEPLAY_SERVER_CHARACTER_NAME")
                ?? options.GameplayServerCharacterName;
        });

        services.Configure<LobbyOptions>(options =>
        {
            options.Name = configuration.ReadText("LOBBY_NAME") ?? options.Name;
            options.Subtype = configuration.ReadText("LOBBY_SUBTYPE") ?? options.Subtype;
            options.Port = configuration.ReadNumber("LOBBY_PORT", options.Port);
            options.BeginnerOnly = configuration.ReadFlag("LOBBY_BEGINNER_ONLY", options.BeginnerOnly);
            options.ExpansionOnly = configuration.ReadFlag("LOBBY_EXPANSION_ONLY", options.ExpansionOnly);
            options.NoHeadshot = configuration.ReadFlag("LOBBY_NO_HEADSHOT", options.NoHeadshot);
            options.ReplaysOnly = configuration.ReadFlag("LOBBY_REPLAYS_ONLY", options.ReplaysOnly);
        });

        services.AddDbContextFactory<Mgo2DatabaseContext>(options =>
            options.UseNpgsql(ResolveDatabaseConnectionString(configuration)));

        services.AddSingleton<CryptographyService>();
        services.AddSingleton<AuthenticationService>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<UserService>();
        services.AddSingleton<NewsService>();
        services.AddSingleton<LobbyService>();
        services.AddSingleton<LobbyGameTypeService>();
        services.AddSingleton<LobbyCacheRefreshService>();
        services.AddSingleton<LobbyTrackerService>();
        services.AddSingleton<CharacterService>();
        services.AddSingleton<CharacterStatisticsService>();
        services.AddSingleton<GameService>();
        services.AddSingleton<RoundReportService>();
        services.AddSingleton<ClanService>();
        services.AddSingleton<MailService>();
        services.AddSingleton<RegistrationService>();
        services.AddSingleton<AutomatchService>();

        services.AddSingleton<PacketCodecService>();
        services.AddSingleton<SessionHelper>();
        services.AddSingleton<CommandRegistry>();
        services.AddSingleton<ActiveGameSessionsService>();
        services.AddSingleton<DatabaseInitializer>();

        return services;
    }

    /// <summary>
    /// Resolves the connection string from the environment, accepting the
    /// conventional variable, the connection strings section and the fallback
    /// of a local server.
    /// </summary>
    /// <param name="configuration">Configuration to read from.</param>
    private static string ResolveDatabaseConnectionString(IConfiguration configuration) =>
        configuration.ReadText("DATABASE_CONNECTION_STRING")
        ?? configuration.ReadText("DATABASE_URL")
        ?? configuration.GetConnectionString("Mgo2Database")
        ?? "Host=localhost;Port=5432;Database=mgo2;Username=postgres";
}
