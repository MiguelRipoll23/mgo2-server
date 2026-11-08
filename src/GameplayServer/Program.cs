using Mgo2Server.GameplayServer;
using Mgo2Server.GameplayServer.Bots;
using Mgo2Server.GameplayServer.Commands;
using Mgo2Server.GameplayServer.Gameplay;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Infrastructure.DependencyInjection;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Telemetry;
using Mgo2Server.Shared.Udp;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddServerLogging(builder.Configuration);

builder.Services.AddServerServices(builder.Configuration);

// The same exporter every lobby registers, under this server's own service
// name: a gameplay host's logs are read beside the lobby that leased the match,
// and they are only in one place if they arrive through the same collector.
builder.Services.AddServerTelemetry(builder.Configuration, "mgo2-gameplay");

builder.Services.AddSingleton<HostIdentityService>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<MatchService>();
builder.Services.AddSingleton<MatchSettingsService>();
builder.Services.AddSingleton<RoomRosterService>();
builder.Services.AddSingleton<LobbySlotService>();
builder.Services.AddSingleton<PeerControlByteService>();
builder.Services.AddSingleton<LobbyLoadoutService>();
builder.Services.AddSingleton<PlayerSettingsBurstService>();
builder.Services.AddSingleton<PlayerSettingsBurstSchedulerService>();
builder.Services.AddSingleton<MatchRecordSequenceService>();
builder.Services.AddSingleton<MatchRecordService>();
builder.Services.AddSingleton<BriefingCountdownService>();
builder.Services.AddSingleton<BriefingReadyPublishService>();
builder.Services.AddSingleton<BriefingRoomPublishService>();
builder.Services.AddSingleton<MatchRoundStateService>();
builder.Services.AddSingleton<MatchStartRunService>();
builder.Services.AddSingleton<MatchScoreService>();
builder.Services.AddSingleton<ScoreboardPublishService>();
builder.Services.AddSingleton<MatchResultService>();
builder.Services.AddSingleton<BriefingStateService>();
builder.Services.AddSingleton<PlayerVitalsService>();
builder.Services.AddSingleton<WeaponSelectionService>();
builder.Services.AddSingleton<TeamArrayService>();
// The bots are assembled from their parts: the identity service names them, the
// population service holds them, and the decision stack is what a bot's tick
// calls in order. Every one of them is a singleton, because a room and its bots
// are one room for the life of the process.
builder.Services.AddSingleton<BotIdentityService>();
builder.Services.AddSingleton<BotTeamAssignmentService>();
builder.Services.AddSingleton<BotRoleAssignmentService>();
builder.Services.AddSingleton<BotObjectiveService>();
builder.Services.AddSingleton<BotPerceptionService>();
builder.Services.AddSingleton<BotDecisionService>();
builder.Services.AddSingleton<BotWeaponCatalogService>();
builder.Services.AddSingleton<BotTargetSelectorService>();
builder.Services.AddSingleton<BotCombatService>();
builder.Services.AddSingleton<StageNavigationCatalogService>();
builder.Services.AddSingleton<StageBotNavigationProvider>();
builder.Services.AddSingleton<IBotNavigationProvider>(
    provider => provider.GetRequiredService<StageBotNavigationProvider>());
builder.Services.AddSingleton<IBotMapNavigationDataProvider>(
    provider => provider.GetRequiredService<StageBotNavigationProvider>());
builder.Services.AddSingleton<BotNavigationAgent>();
builder.Services.AddSingleton<BotMovementDestinationService>();
builder.Services.AddSingleton<BotTeamCoordinationService>();
builder.Services.AddSingleton<BotSideService>();
builder.Services.AddSingleton<BotPlayService>();
builder.Services.AddSingleton<BotController>();
builder.Services.AddSingleton<BotPopulationService>();
builder.Services.AddSingleton<PeerCommandRegistry>();
// The peer command handlers are registered beside the types their messages are
// bound to, so a handler cannot be mapped without being resolvable.
builder.Services.AddCommandHandlers();
builder.Services.AddSingleton<GameplayServerService>();

var host = builder.Build();

var options = host.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GameplayServer");

// The room's rotation is named in this instance's configuration and nowhere
// else, so a name that is no rule or no map is a round the client cannot play,
// and a zero-length briefing or an empty rotation is a room nothing can start.
// Both are refused here, before a peer is answered, rather than by a round that
// never opens: a named fault, in a process that has served nothing yet.
MatchSettingsUtils.Validate(options);

// The bots are configured the way the room is: a value that names no policy stops
// the process before a peer is answered, rather than a room that silently holds
// the population a default happened to name.
var botSettings = host.Services.GetRequiredService<IOptions<BotOptions>>().Value;
var botFaults = BotOptionsUtils.Validate(botSettings);
if (botFaults.Count > 0)
{
    throw new InvalidOperationException(
        "The bot settings are not readable:" + Environment.NewLine
        + string.Join(Environment.NewLine, botFaults));
}

logger.LogInformation(
    "Bots: {TeamPolicy} sides, {PopulationMode} population, {BotCount} configured",
    BotOptionsUtils.Team(botSettings),
    BotOptionsUtils.PopulationMode(botSettings),
    botSettings.Count);

var rotation = MatchSettingsUtils.Rotation(options);
logger.LogInformation(
    "The room plays {RoundCount} round(s) for {BriefingMinutes}-minute briefings: {Rotation}",
    rotation.Count,
    options.GameplayServerBriefingMinutes,
    string.Join(
        ", ",
        rotation.Select(
            round => $"{MatchRuleUtils.Name(round.Rule)} on {MatchMapUtils.Name(round.Map)} for "
                + $"{round.TimeLimitMinutes} min"
                + (round.Tickets is { } limit ? $" to {limit} kills" : string.Empty))));

var registry = host.Services.GetRequiredService<PeerCommandRegistry>();
PeerCommandHandlerRegistration.RegisterCommandHandlers(registry);

var GameplayServer = host.Services.GetRequiredService<GameplayServerService>();

logger.LogInformation(
    "Starting Gameplay server on port {Port} (lobby {LobbyName})",
    options.GameplayServerPort,
    options.GameplayLobbyName);

using var cancellation = new CancellationTokenSource();

// Ends the host when a person interrupts it or a deployment asks it to terminate.
// The gameplay server finds the second one on every rollout, and without it the
// process is gone the moment the signal lands, taking the match with it.
using var stopSignals = ShutdownSignalUtils.OnStopRequested(cancellation.Cancel, logger);

try
{
    await GameplayServer.RunAsync(cancellation.Token);
}
catch (OperationCanceledException)
{
    // Normal shutdown.
}
finally
{
    await GameplayServer.DisposeAsync();
}
