using Mgo2Server.GameplayServer;
using Mgo2Server.GameplayServer.Commands;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.PacketReplay;
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
// What the host has written itself, which the replay console badges a captured
// frame with. It is fed by the outbound path, so it is only as large as the
// traffic this instance has actually carried.
builder.Services.AddSingleton<SentMessageTypesService>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<MatchService>();
builder.Services.AddSingleton<RoomRosterService>();
builder.Services.AddSingleton<PlayerSettingsBurstService>();
builder.Services.AddSingleton<PlayerSettingsBurstSchedulerService>();
builder.Services.AddSingleton<MatchRecordService>();
builder.Services.AddSingleton<MatchRoundStateService>();
builder.Services.AddSingleton<MatchStartRunService>();
builder.Services.AddSingleton<TeamArrayService>();
builder.Services.AddSingleton<PeerCommandRegistry>();
// The peer command handlers are registered beside the types their messages are
// bound to, so a handler cannot be mapped without being resolvable.
builder.Services.AddCommandHandlers();
builder.Services.AddSingleton<GameplayServerService>();

// The console that resends a captured frame needs the live peer sessions, so it
// lives inside this process rather than beside it.
builder.Services.AddSingleton<PacketReplayDataService>();
builder.Services.AddSingleton<PacketReplaySenderService>();
builder.Services.AddSingleton<PacketReplayWebService>();

var host = builder.Build();

var registry = host.Services.GetRequiredService<PeerCommandRegistry>();
PeerCommandHandlerRegistration.RegisterCommandHandlers(registry);

var GameplayServer = host.Services.GetRequiredService<GameplayServerService>();
var packetReplayWeb = host.Services.GetRequiredService<PacketReplayWebService>();
var options = host.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GameplayServer");

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
    await packetReplayWeb.StartAsync(cancellation.Token);
    await GameplayServer.RunAsync(cancellation.Token);
}
catch (OperationCanceledException)
{
    // Normal shutdown.
}
finally
{
    await packetReplayWeb.DisposeAsync();
    await GameplayServer.DisposeAsync();
}
