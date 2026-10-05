using Mgo2Server.GameplayServer;
using Mgo2Server.GameplayServer.Commands;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Match;
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
builder.Services.AddSingleton<RoomRosterService>();
builder.Services.AddSingleton<PostJoinBurstService>();
builder.Services.AddSingleton<PostJoinBurstSchedulerService>();
builder.Services.AddSingleton<PeerCommandRegistry>();
// The peer command handlers are registered beside the types their messages are
// bound to, so a handler cannot be mapped without being resolvable.
builder.Services.AddCommandHandlers();
builder.Services.AddSingleton<PlayerStateService>();
builder.Services.AddSingleton<GameplayServerService>();

var host = builder.Build();

var registry = host.Services.GetRequiredService<PeerCommandRegistry>();
PeerCommandHandlerRegistration.RegisterCommandHandlers(registry);

var GameplayServer = host.Services.GetRequiredService<GameplayServerService>();
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
