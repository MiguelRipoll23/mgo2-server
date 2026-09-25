using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Domain.Authentication;
using Mgo2Server.Shared.Domain.Automatch;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Domain.Instructors;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Domain.Mail;
using Mgo2Server.Shared.Domain.News;
using Mgo2Server.Shared.Domain.Presence;
using Mgo2Server.Shared.Domain.Rankings;
using Mgo2Server.Shared.Domain.Users;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Telemetry;
using Mgo2Server.Shared.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

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
            options.AdvertisedAddress =
                configuration.ReadText("ADVERTISED_ADDRESS") ?? options.AdvertisedAddress;
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
            options.BeginnersOnly = configuration.ReadFlag("LOBBY_BEGINNER_ONLY", options.BeginnersOnly);
            options.ExpansionRequired = configuration.ReadFlag("LOBBY_EXPANSION_ONLY", options.ExpansionRequired);
            options.NoHeadshot = configuration.ReadFlag("LOBBY_NO_HEADSHOT", options.NoHeadshot);
            options.ReplaysOnly = configuration.ReadFlag("LOBBY_REPLAYS_ONLY", options.ReplaysOnly);
        });

        services.Configure<CoordinationOptions>(options =>
        {
            options.ServerUrl = configuration.ReadText("INTERNAL_GRPC_URL") ?? options.ServerUrl;
            options.ReconnectInterval = TimeSpan.FromSeconds(configuration.ReadNumber(
                "INTERNAL_GRPC_RECONNECT_SECONDS",
                (int)options.ReconnectInterval.TotalSeconds));
        });

        services.Configure<AutomatchOptions>(options =>
        {
            options.Enabled = configuration.ReadFlag("AUTOMATCH_ENABLED", options.Enabled);
            options.Windows = configuration.ReadText("AUTOMATCH_WINDOWS") ?? options.Windows;
            options.TimeZone = configuration.ReadText("AUTOMATCH_TIMEZONE") ?? options.TimeZone;
            options.TickSeconds = configuration.ReadNumber("AUTOMATCH_TICK_SECONDS", options.TickSeconds);
            options.MinimumPlayers = configuration.ReadNumber("AUTOMATCH_MIN_PLAYERS", options.MinimumPlayers);
            options.MinimumPlayersAtStart = configuration.ReadNumber(
                "AUTOMATCH_MIN_PLAYERS_START",
                options.MinimumPlayersAtStart);
            options.MinimumPlayersStepSeconds = configuration.ReadNumber(
                "AUTOMATCH_MIN_PLAYERS_STEP_SECONDS",
                options.MinimumPlayersStepSeconds);
            options.BandAtStart = configuration.ReadNumber("AUTOMATCH_BAND_START", options.BandAtStart);
            options.BandStepSeconds = configuration.ReadNumber(
                "AUTOMATCH_BAND_STEP_SECONDS",
                options.BandStepSeconds);
            options.BandMaximum = configuration.ReadNumber("AUTOMATCH_BAND_MAX", options.BandMaximum);
            options.ModeRelaxSeconds = configuration.ReadNumber(
                "AUTOMATCH_MODE_RELAX_SECONDS",
                options.ModeRelaxSeconds);
        });

        services.Configure<EventOptions>(options =>
        {
            options.Title = configuration.ReadText("EVENT_TITLE") ?? options.Title;
            options.Description = configuration.ReadText("EVENT_DESCRIPTION") ?? options.Description;
            options.TimeZone = configuration.ReadText("EVENT_SCHEDULE_TIMEZONE") ?? options.TimeZone;
            options.StartMinute = configuration.ReadNumber(
                "EVENT_SCHEDULE_START_MINUTE",
                options.StartMinute);
            options.EndMinute = configuration.ReadNumber(
                "EVENT_SCHEDULE_END_MINUTE",
                options.EndMinute);
            options.ParticipationReward = configuration.ReadNumber(
                "EVENT_PARTICIPATION_REWARD",
                options.ParticipationReward);
            options.WinRewards = configuration.ReadText("EVENT_WIN_REWARDS") ?? options.WinRewards;
            options.InformationRuleFlags = configuration.ReadNumber(
                "EVENT_INFORMATION_RULE_FLAGS",
                options.InformationRuleFlags);
            options.DisplayedWinCount = configuration.ReadNumber(
                "EVENT_DISPLAYED_WIN_COUNT",
                options.DisplayedWinCount);
            options.PrizeLabels = configuration.ReadText("TOURNAMENT_PRIZE_LABELS") ?? options.PrizeLabels;
            options.TournamentMinimumLevel = configuration.ReadNumber(
                "TOURNAMENT_MINIMUM_LEVEL",
                options.TournamentMinimumLevel);
            options.TournamentMaximumLevel = configuration.ReadNumber(
                "TOURNAMENT_MAXIMUM_LEVEL",
                options.TournamentMaximumLevel);
            options.ReportGraceMilliseconds = configuration.ReadNumber(
                "EVENT_REPORT_GRACE_MILLISECONDS",
                options.ReportGraceMilliseconds);
            options.OutcomeSweepMilliseconds = configuration.ReadNumber(
                "EVENT_OUTCOME_SWEEP_MILLISECONDS",
                options.OutcomeSweepMilliseconds);
        });

        services.AddDbContextFactory<Mgo2DatabaseContext>(options =>
        {
            var connection = ApplyConnectionPolicy(ResolveDatabaseConnectionString(configuration));

            options.UseNpgsql(
                connection.ConnectionString,
                npgsql =>
                {
                    if (!connection.ContainsKey("Command Timeout"))
                    {
                        npgsql.CommandTimeout(CommandTimeoutSeconds);
                    }
                });
        });

        services.AddSingleton<CryptographyService>();
        services.AddSingleton<AuthenticationService>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<UserService>();
        services.AddSingleton<NewsService>();
        services.AddSingleton<LobbyService>();
        services.AddSingleton<LobbyGameTypeService>();

        // TryAdd: a server that speaks the coordination protocol registers its
        // own publisher before or after this call, and either way the tracker
        // has one to report to.
        services.TryAddSingleton<ILobbyPresencePublisher, NullLobbyPresencePublisher>();
        services.AddSingleton<LobbyTrackerService>();
        services.AddSingleton<CharacterPresenceService>();
        services.AddSingleton<CharacterService>();
        services.AddSingleton<CharacterStatisticsService>();
        services.AddSingleton<CharacterTitleService>();
        services.AddSingleton<InstructorService>();
        services.AddSingleton<GameService>();
        services.AddSingleton<RoundReportService>();
        services.AddSingleton<ClanService>();
        services.AddSingleton<MailService>();
        services.AddSingleton<RegistrationService>();
        services.AddSingleton<AutomatchService>();
        services.AddSingleton<AutomatchHooksService>();
        services.AddSingleton<EventInformationService>();
        services.AddSingleton<EventTeamService>();
        services.AddSingleton<EventTeamPushService>();
        services.AddSingleton<EventInvitationService>();
        services.AddSingleton<EventSessionDirectoryService>();
        services.AddSingleton<EventMatchService>();
        services.AddSingleton<EventMatchmakingService>();
        services.AddSingleton<EventHostLeaseService>();
        services.AddSingleton<EventAssignmentService>();
        services.AddSingleton<EventAssignmentPushService>();
        services.AddSingleton<EventScheduleService>();
        services.AddSingleton<TournamentRegistrationService>();
        services.AddSingleton<TournamentBracketService>();
        services.AddSingleton<TournamentMatchService>();
        services.AddSingleton<EventRewardService>();
        services.AddSingleton<EventOutcomeService>();
        services.AddSingleton<EventOutcomePushService>();
        services.AddSingleton<EventBracketPushService>();
        services.AddSingleton<EventGameEntryService>();
        services.AddSingleton<EventEntryService>();
        services.AddSingleton<RankingBoardService>();
        services.AddSingleton<RankingService>();

        // Registered whether or not a collector is configured, because the
        // services that publish totals take it as a dependency; without a
        // listener the instruments are silent and no query is made.
        services.AddSingleton<ServerMetricsService>();

        services.AddSingleton<PacketCodecService>();
        services.AddSingleton<SessionHelper>();
        services.AddSingleton<CommandRegistry>();
        services.AddSingleton<ActiveGameSessionsService>();

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

    /// <summary>
    /// Seconds a single statement may run before it is abandoned. Well under the
    /// provider's own default, because a statement that is going to fail should
    /// fail while the caller can still be told, and because every second it is
    /// held is a second of a worker's tick spent waiting for it.
    /// </summary>
    private const int CommandTimeoutSeconds = 15;

    /// <summary>Connections one process may hold open at once.</summary>
    private const int MaximumPoolSize = 20;

    /// <summary>Seconds a connection attempt may take before it is abandoned.</summary>
    private const int ConnectionTimeoutSeconds = 5;

    /// <summary>Seconds between keep-alive probes on an idle pooled connection.</summary>
    private const int KeepAliveSeconds = 30;

    /// <summary>
    /// Puts the settings this process is responsible for on the connection string.
    /// <para>
    /// Each one is a fact about the client rather than about the database, so it
    /// belongs here rather than in the deployment: how many connections one process
    /// may open, how long it waits for one, and how long a statement runs. They are
    /// applied only when the connection string does not already set them, so an
    /// operator who states one in the secret still has the last word.
    /// </para>
    /// <para>
    /// The pool ceiling matters most when the database is failing. Every attempt to
    /// reach a database that is away is a handshake and a certificate check, and
    /// with the provider's default ceiling a single instance can hold a hundred
    /// connections open at once — which, multiplied by every instance of every
    /// role, is a CPU bill paid to learn nothing.
    /// </para>
    /// </summary>
    /// <param name="connectionString">Connection string as the environment gave it.</param>
    private static NpgsqlConnectionStringBuilder ApplyConnectionPolicy(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (!builder.ContainsKey("Maximum Pool Size"))
        {
            builder.MaxPoolSize = MaximumPoolSize;
        }

        if (!builder.ContainsKey("Timeout"))
        {
            builder.Timeout = ConnectionTimeoutSeconds;
        }

        if (!builder.ContainsKey("Keepalive"))
        {
            builder.KeepAlive = KeepAliveSeconds;
        }

        return builder;
    }
}
