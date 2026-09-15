using Mgo2Server.Stun;
using Mgo2Server.Stun.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

using var loggerFactory = LoggerFactory.Create(logging =>
{
    // Every request is logged, so debug is the default level; LOG_LEVEL raises it.
    logging.SetMinimumLevel(
        Enum.TryParse<LogLevel>(configuration["LOG_LEVEL"], ignoreCase: true, out var level)
            ? level
            : LogLevel.Debug);
    logging.AddSimpleConsole(console =>
    {
        console.SingleLine = true;
        console.TimestampFormat = "HH:mm:ss ";
    });
});

var options = StunServerOptions.FromConfiguration(configuration);
var logger = loggerFactory.CreateLogger<StunServer>();

logger.LogInformation(
    "Starting the port-check responder: address {PrimaryAddress}, port {Port}{SecondaryAddress}",
    options.PrimaryAddress,
    options.Port,
    options.SecondaryAddress is null ? string.Empty : $" and {options.SecondaryAddress}");

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArguments) =>
{
    eventArguments.Cancel = true;
    logger.LogInformation("Shutdown requested");
    cancellation.Cancel();
};

try
{
    await new StunServer(options, logger).RunAsync(cancellation.Token);
}
catch (OperationCanceledException)
{
    // Normal shutdown.
}
