using Mgo2Server.Infrastructure.DependencyInjection;
using Mgo2Server.GameLobbyServer;
using Mgo2Server.GameLobbyServer.Commands;
using Mgo2Server.GameLobbyServer.Maintenance;
using Mgo2Server.Shared.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddServerLogging(builder.Configuration);

builder.Services.AddServerServices(builder.Configuration);
builder.Services.AddCommandHandlers();
builder.Services.AddSingleton<LobbyHeartbeatService>();
builder.Services.AddSingleton<LobbyCleanupService>();
builder.Services.AddSingleton<GameLobbyServerRunner>();

var host = builder.Build();

var runner = host.Services.GetRequiredService<GameLobbyServerRunner>();
var lobbyOptions = host.Services.GetRequiredService<IOptions<LobbyOptions>>().Value;
var options = host.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GameLobbyServer");

lobbyOptions.Validate();

logger.LogInformation(
    "Starting gameplay lobby {LobbyName} ({Subtype}) on {ListeningIpAddress}",
    lobbyOptions.Name,
    lobbyOptions.Subtype,
    options.ListeningIpAddress);

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArguments) =>
{
    eventArguments.Cancel = true;
    logger.LogInformation("Shutdown requested");
    cancellation.Cancel();
};

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
