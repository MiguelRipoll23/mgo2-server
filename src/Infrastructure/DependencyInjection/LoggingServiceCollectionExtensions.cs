using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Infrastructure.DependencyInjection;

/// <summary>
/// Configures the console logging every server shares: the traffic of the wire
/// protocol is logged, so debug is the default level.
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    /// <summary>Name of the setting that overrides the level.</summary>
    public const string LogLevelSettingName = "LOG_LEVEL";

    /// <summary>Level used when the setting is absent or unreadable.</summary>
    private const LogLevel DefaultLevel = LogLevel.Debug;

    /// <summary>
    /// Applies the configured level and the shared single-line console format.
    /// </summary>
    /// <param name="logging">Logging builder to configure.</param>
    /// <param name="configuration">Configuration the level is read from.</param>
    /// <returns>The same builder, so the call can be chained.</returns>
    public static ILoggingBuilder AddServerLogging(this ILoggingBuilder logging, IConfiguration configuration)
    {
        logging.SetMinimumLevel(ResolveLevel(configuration));
        logging.AddSimpleConsole(console =>
        {
            console.SingleLine = true;
            console.TimestampFormat = "HH:mm:ss ";
        });

        return logging;
    }

    /// <summary>Reads the level named by the setting.</summary>
    /// <param name="configuration">Configuration to read from.</param>
    public static LogLevel ResolveLevel(IConfiguration configuration) =>
        Enum.TryParse<LogLevel>(configuration.ReadText(LogLevelSettingName), ignoreCase: true, out var level)
            ? level
            : DefaultLevel;
}
