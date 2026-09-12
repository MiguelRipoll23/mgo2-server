using Mgo2Server.AccountLobbyServer;
using Mgo2Server.AccountLobbyServer.Commands;
using Mgo2Server.Infrastructure.DependencyInjection;
using Mgo2Server.Shared.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddServerLogging(builder.Configuration);

builder.Services.AddServerServices(builder.Configuration);
builder.Services.AddCommandHandlers();
builder.Services.AddSingleton<AccountLobbyServerRunner>();

var host = builder.Build();

var runner = host.Services.GetRequiredService<AccountLobbyServerRunner>();
var options = host.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AccountLobbyServer");

logger.LogInformation("Starting the account server on {ListeningIpAddress}", options.ListeningIpAddress);

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
    runner.Stop();
}
