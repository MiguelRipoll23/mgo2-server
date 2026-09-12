using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Mgo2Server.Infrastructure.DependencyInjection;

/// <summary>
/// Configures Serilog logging for every server: events go to the console and,
/// when LOG_DIRECTORY is set, to a rolling file under that directory. The
/// minimum level defaults to Debug so UDP/TCP packet hex dumps are visible;
/// set LOG_LEVEL to override (Information, Warning, Error).
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    /// <summary>Name of the setting that overrides the level.</summary>
    public const string LogLevelSettingName = "LOG_LEVEL";

    /// <summary>Name of the setting that sets the log directory.</summary>
    public const string LogDirectorySettingName = "LOG_DIRECTORY";

    /// <summary>Path template used for the rolling file sink.</summary>
    private const string FilePathTemplate = "log-.txt";

    /// <summary>Output template shared by the console and file sinks.</summary>
    private const string OutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Shared level switch that the configuration system can update at runtime
    /// so the log level takes effect without restarting the process.
    /// </summary>
    public static LoggingLevelSwitch LevelSwitch { get; } = new(LogEventLevel.Debug);

    /// <summary>
    /// Applies Serilog logging with the configured minimum level. Log events go
    /// to the console and, when LOG_DIRECTORY is set, to a rolling file under
    /// that directory. The configuration is read from <paramref name="configuration"/>
    /// and watched for changes so the log level can be adjusted at runtime.
    /// </summary>
    /// <param name="logging">Logging builder to configure.</param>
    /// <param name="configuration">Configuration the level is read from.</param>
    /// <returns>The same builder, so the call can be chained.</returns>
    public static ILoggingBuilder AddServerLogging(
        this ILoggingBuilder logging,
        IConfiguration configuration)
    {
        logging.SetMinimumLevel(LogLevel.Debug);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LevelSwitch)
            .Enrich.FromLogContext()
            .WriteTo.File(path: "logs/log-.txt", outputTemplate: OutputTemplate);

        var logDirectory = configuration[LogDirectorySettingName];
        if (!string.IsNullOrWhiteSpace(logDirectory)
            && Directory.Exists(logDirectory))
        {
            var logPath = Path.Combine(logDirectory, FilePathTemplate);
            loggerConfig.WriteTo.File(
                logPath,
                outputTemplate: OutputTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10_000_000,
                rollOnFileSizeLimit: true);
        }

        // Watch the configuration for changes so the LOG_LEVEL environment
        // variable (or appsettings.json) takes effect at runtime.
        loggerConfig.ReadFrom.Configuration(configuration);

        Log.Logger = loggerConfig.CreateLogger();
        logging.AddSerilog(dispose: true);

        return logging;
    }
}
