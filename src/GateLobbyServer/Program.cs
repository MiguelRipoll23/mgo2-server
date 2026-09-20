using Mgo2Server.GateLobbyServer;
using Mgo2Server.GateLobbyServer.Commands;
using Mgo2Server.Infrastructure.DependencyInjection;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Telemetry;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddServerLogging(builder.Configuration);

builder.Services.AddServerServices(builder.Configuration);
builder.Services.AddServerTelemetry(builder.Configuration, "mgo2-gate");
builder.Services.AddCommandHandlers();
builder.Services.AddSingleton<GateLobbyServerRunner>();

var host = builder.Build();

var runner = host.Services.GetRequiredService<GateLobbyServerRunner>();
var lobbyOptions = host.Services.GetRequiredService<IOptions<LobbyOptions>>().Value;
var options = host.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GateLobbyServer");

// The gate is a permanent endpoint rather than a gameplay lobby, so it needs no
// game type: LOBBY_NAME and LOBBY_PORT name the row it publishes.
lobbyOptions.Validate(isGameLobby: false);

logger.LogInformation(
    "Starting lobby {LobbyName} on port {Port}, announcing {AnnouncedIpAddress}",
    lobbyOptions.Name,
    lobbyOptions.Port,
    options.AnnouncedIpAddress);

using var cancellation = new CancellationTokenSource();

// Both stops reach the runner: the interrupt a person sends from a terminal, and
// the SIGTERM a rollout sends. The runner closes the listener on it and then waits
// for the players it is serving to leave, instead of ending on top of them.
using var stopSignals = ShutdownSignalUtils.OnStopRequested(cancellation.Cancel, logger);

try
{
    await runner.RunAsync(cancellation.Token);
}
catch (OperationCanceledException)
{
    // Normal shutdown.
}
finally
{
    await runner.StopAsync();
}
