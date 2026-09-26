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
/// outright. Entity Framework is held at Warning regardless, so that a
/// server asked for Debug says what went wrong without narrating every query
/// it ran.
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    /// <summary>Name of the setting that overrides the level.</summary>
    public const string LogLevelSettingName = "LOG_LEVEL";

    /// <summary>Level applied when LOG_LEVEL is unset or names no level.</summary>
    private const LogLevel DefaultLevel = LogLevel.Warning;

    /// <summary>
    /// Category prefix whose events are held at <see cref="EntityFrameworkLevel"/>
    /// when LOG_LEVEL asks for something noisier.
    /// </summary>
    private const string EntityFrameworkCategoryPrefix = "Microsoft.EntityFrameworkCore";

    /// <summary>
    /// The level the Entity Framework categories are never written below.
    /// <para>
    /// A lobby running at Debug writes almost nothing else: on the Survival pod a
    /// six-minute sample was 4,644 lines of which 3,131 were Debug events from
    /// this namespace and 1,298 were the SQL text those events carry across
    /// further lines. That is what rotated a team-creation refusal out of the
    /// container log inside half an hour, and it is what made the log unusable
    /// for the one thing it is for.
    /// </para>
    /// <para>
    /// Warning is the level that keeps the record of what actually went wrong
    /// and drops the per-query narration, which at Information is one
    /// "Executed DbCommand" line plus the SQL text it carries — the bulk of
    /// the output by a wide margin, and the part nobody reads after the fault
    /// it would have explained is in the exception above it. A deployment that
    /// wants the SQL turns the floor off for a run rather than paying for it
    /// on every run. It is a floor and not a ceiling: LOG_LEVEL still governs
    /// everything else, and a deployment that asks for less than this gets
    /// what it asked for.
    /// </para>
    /// </summary>
    private const LogLevel EntityFrameworkLevel = LogLevel.Warning;

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

        // LogLevel orders from Trace (0) upwards, so the stricter of the two is
        // the larger. Serilog's override is absolute rather than a clamp - it
        // would happily make this namespace chattier than a LOG_LEVEL that asked
        // for less - so the floor is resolved here instead of being applied
        // blind. A deployment at Error or above is left alone; only a Trace,
        // Debug or Information is capped, at Warning.
        var entityFrameworkLevel = level > EntityFrameworkLevel ? level : EntityFrameworkLevel;

        // Serilog is the only console output. The providers the host wires by
        // default are dropped, so a container's log is exactly what Serilog
        // writes and no event is written twice.
        logging.ClearProviders();
        logging.SetMinimumLevel(level);

        // Set on the builder as well as on the Serilog logger, because the two
        // cover different providers and neither covers the other's. AddSerilog
        // registers a catch-all rule at Trace for its own provider, and a
        // provider-specific rule outranks a category-only one, so the filter
        // below is never consulted for the console sink - that is what the
        // Serilog override is for. What the filter is for is every provider
        // that is not Serilog, the OTLP logging provider above all, which would
        // otherwise export the same spam to the collector.
        logging.AddFilter(EntityFrameworkCategoryPrefix, entityFrameworkLevel);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(ToEventLevel(level))
            .MinimumLevel.Override(EntityFrameworkCategoryPrefix, ToEventLevel(entityFrameworkLevel))
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
