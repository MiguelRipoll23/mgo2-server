using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Mgo2Server.Infrastructure.DependencyInjection;

/// <summary>
/// Configures Serilog logging for every server: events go to the console,
/// which is what puts a container's log on its standard output, where
/// 'docker compose logs' reads it. When telemetry is on, the OpenTelemetry
/// exporter registered by AddServerTelemetry carries the same events to the
/// collector. The minimum level defaults to Warning; set LOG_LEVEL to override
/// (Debug, Information, Warning, Error). The run scripts export LOG_LEVEL=Debug
/// outright.
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    /// <summary>Name of the setting that overrides the level.</summary>
    public const string LogLevelSettingName = "LOG_LEVEL";

    /// <summary>Level applied when LOG_LEVEL is unset or names no level.</summary>
    private const LogLevel DefaultLevel = LogLevel.Warning;

    /// <summary>Output template of the console sink.</summary>
    private const string OutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Applies Serilog logging with the configured minimum level. Log events go
    /// to the console; the configuration is read once at startup.
    /// </summary>
    /// <param name="logging">Logging builder to configure.</param>
    /// <param name="configuration">Configuration the level is read from.</param>
    /// <returns>The same builder, so the call can be chained.</returns>
    public static ILoggingBuilder AddServerLogging(
        this ILoggingBuilder logging,
        IConfiguration configuration)
    {
        var level = ResolveLevel(configuration);

        // Serilog is the only console output. The providers the host wires by
        // default are dropped, so a container's log is exactly what Serilog
        // writes and no event is written twice.
        logging.ClearProviders();
        logging.SetMinimumLevel(level);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(ToEventLevel(level))
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .CreateLogger();

        logging.AddSerilog(dispose: true);

        return logging;
    }

    /// <summary>
    /// Reads LOG_LEVEL, falling back to the documented default when it is unset
    /// or does not name a level the logging framework knows.
    /// </summary>
    private static LogLevel ResolveLevel(IConfiguration configuration)
    {
        var configured = configuration[LogLevelSettingName];

        return Enum.TryParse<LogLevel>(configured, ignoreCase: true, out var level)
            ? level
            : DefaultLevel;
    }

    /// <summary>Maps a logging level onto the equivalent Serilog level.</summary>
    private static LogEventLevel ToEventLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        LogLevel.Critical => LogEventLevel.Fatal,
        // Serilog has no "off" level; fatal is the quietest level it has.
        LogLevel.None => LogEventLevel.Fatal,
        _ => LogEventLevel.Warning,
    };
}
