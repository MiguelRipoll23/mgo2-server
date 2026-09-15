using Mgo2Server.Infrastructure.DependencyInjection;
using Mgo2Server.GameLobbyServer;
using Mgo2Server.GameLobbyServer.Commands;
using Mgo2Server.GameLobbyServer.Maintenance;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddServerLogging(builder.Configuration);

builder.Services.AddServerServices(builder.Configuration);
builder.Services.AddServerTelemetry(builder.Configuration);
builder.Services.AddCommandHandlers();
builder.Services.AddSingleton<LobbyHeartbeatService>();
builder.Services.AddSingleton<LobbyCleanupService>();
builder.Services.AddSingleton<GameCleanupService>();
builder.Services.AddSingleton<AutomatchTickerService>();
builder.Services.AddSingleton<GameLobbyServerRunner>();

var host = builder.Build();

var runner = host.Services.GetRequiredService<GameLobbyServerRunner>();
var lobbyOptions = host.Services.GetRequiredService<IOptions<LobbyOptions>>().Value;
var automatchOptions = host.Services.GetRequiredService<IOptions<AutomatchOptions>>().Value;
var options = host.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GameLobbyServer");

lobbyOptions.Validate();

// Refused here rather than at first use, so a schedule that cannot be honoured
// stops the process instead of closing the feature in a way that looks deliberate.
automatchOptions.Validate();

logger.LogInformation(
    "Automatching is {State} ({Schedule})",
    automatchOptions.Enabled ? "enabled" : "disabled",
    string.IsNullOrWhiteSpace(automatchOptions.Windows)
        ? "open all day"
        : $"{automatchOptions.Windows} in {automatchOptions.TimeZone}");

logger.LogInformation(
    "Starting gameplay lobby {LobbyName} ({Subtype}), announcing {AnnouncedIpAddress}",
    lobbyOptions.Name,
    lobbyOptions.Subtype,
    options.AnnouncedIpAddress);

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
