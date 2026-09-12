using Mgo2Server.GameplayServer;
using Mgo2Server.GameplayServer.Commands;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Infrastructure.DependencyInjection;
using Mgo2Server.Infrastructure.Persistence;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Udp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddServerLogging(builder.Configuration);

builder.Services.AddServerServices(builder.Configuration);

builder.Services.AddSingleton<HostIdentityService>();
builder.Services.AddSingleton<GameplayServerAccountService>();
builder.Services.AddSingleton<GameplayServerMatchService>();
builder.Services.AddSingleton<PeerCommandRegistry>();
builder.Services.AddSingleton<AcceptHandshakeHandler>();
builder.Services.AddSingleton<AcknowledgeKeepAliveHandler>();
builder.Services.AddSingleton<PlayerProfileHandler>();
builder.Services.AddSingleton<GameplayServerService>();

var host = builder.Build();

var registry = host.Services.GetRequiredService<PeerCommandRegistry>();
PeerCommandHandlerRegistration.RegisterCommandHandlers(registry);

await host.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();

var GameplayServer = host.Services.GetRequiredService<GameplayServerService>();
var options = host.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GameplayServer");

logger.LogInformation(
    "Starting Gameplay server on port {Port} (lobby {LobbyName})",
    options.GameplayServerPort,
    options.GameplayServerLobbyName);

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArguments) =>
{
    eventArguments.Cancel = true;
    logger.LogInformation("Shutdown requested");
    cancellation.Cancel();
};

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
