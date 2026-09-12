using Mgo2Server.Dns;
using Mgo2Server.Dns.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

using var loggerFactory = LoggerFactory.Create(logging =>
{
    // Every query is logged, so debug is the default level; LOG_LEVEL raises it.
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

var options = DnsServerOptions.FromConfiguration(configuration);
var logger = loggerFactory.CreateLogger<DnsServer>();

logger.LogInformation(
    "Starting the name server with local domains {LocalDomains}",
    string.Join(", ", options.LocalResolvedDomains));

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArguments) =>
{
    eventArguments.Cancel = true;
    logger.LogInformation("Shutdown requested");
    cancellation.Cancel();
};

try
{
    await new DnsServer(options, logger).RunAsync(cancellation.Token);
}
catch (OperationCanceledException)
{
    // Normal shutdown.
}
