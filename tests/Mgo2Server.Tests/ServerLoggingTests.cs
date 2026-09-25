using Mgo2Server.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ServerLogging = Mgo2Server.Infrastructure.DependencyInjection.LoggingServiceCollectionExtensions;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the floor the Entity Framework categories are held at. It is a floor
/// rather than a default because the one setting it protects against is
/// LOG_LEVEL=Debug, which a deployment sets while chasing something else and
/// then leaves: the namespaces it covers are the loudest on the server, and a
/// refusal logged at Warning is lost inside them.
/// </summary>
[Trait("Category", "Infrastructure")]
public sealed class ServerLoggingTests
{
    /// <summary>The category EF logs its executed commands under.</summary>
    private const string EfCommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";

    /// <summary>A category of this server's own, which LOG_LEVEL does govern.</summary>
    private const string ServerCategory = "Mgo2Server.Shared.Tcp.PacketCodecService";

    /// <summary>The prefix AddServerLogging holds the Entity Framework at.</summary>
    private const string EfPrefix = "Microsoft.EntityFrameworkCore";

    /// <summary>
    /// Asks the console sink's own logger whether it would write an event of a
    /// category, which is what the override is keyed on. The source context is
    /// set the way the Serilog logging provider sets it, so the answer is the
    /// one a real event would get.
    /// </summary>
    private static bool ConsoleSinkWrites(LogEventLevel level, string category) =>
        Log.Logger.ForContext(Constants.SourceContextPropertyName, category).IsEnabled(level);

    [Theory]
    [InlineData("Trace")]
    [InlineData("Debug")]
    public void The_console_sink_drops_entity_framework_debug_whatever_log_level_says(string logLevel)
    {
        Build(logLevel);

        Assert.False(ConsoleSinkWrites(LogEventLevel.Debug, EfCommandCategory));
        Assert.True(ConsoleSinkWrites(LogEventLevel.Information, EfCommandCategory));
    }

    [Fact]
    public void The_console_sink_still_logs_debug_for_the_servers_own_categories()
    {
        Build("Debug");

        Assert.True(ConsoleSinkWrites(LogEventLevel.Debug, ServerCategory));
    }

    [Fact]
    public void The_floor_does_not_raise_a_quiet_log_level_for_entity_framework()
    {
        // Serilog's override is absolute, so applying the floor blind would hand
        // a Warning deployment Information. A floor is only ever a floor.
        Build("Warning");

        Assert.False(ConsoleSinkWrites(LogEventLevel.Information, EfCommandCategory));
    }

    /// <summary>
    /// Guards the other half of the floor. The Serilog override reaches the
    /// console sink only; a provider that is not Serilog - the OTLP logging
    /// provider carries the same events to the collector - is filtered by the
    /// rule on the builder, and that rule has to be there or the exported copy
    /// of the spam is untouched.
    /// </summary>
    [Fact]
    public void Other_providers_are_filtered_by_a_rule_on_the_builder()
    {
        using var provider = BuildProvider("Debug");

        var rules = provider
            .GetRequiredService<IOptions<LoggerFilterOptions>>()
            .Value
            .Rules;

        Assert.Contains(rules, rule =>
            rule.CategoryName == EfPrefix && rule.LogLevel == LogLevel.Information);
    }

    [Fact]
    public void The_rule_on_the_builder_does_not_raise_a_quiet_log_level_either()
    {
        using var provider = BuildProvider("Warning");

        var rules = provider
            .GetRequiredService<IOptions<LoggerFilterOptions>>()
            .Value
            .Rules;

        Assert.DoesNotContain(rules, rule =>
            rule.CategoryName == EfPrefix && rule.LogLevel == LogLevel.Information);
    }

    /// <summary>
    /// Builds the server's container at the given LOG_LEVEL.
    /// </summary>
    private static ServiceProvider BuildProvider(string logLevel)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddServerLogging(Configuration(logLevel)));
        return services.BuildServiceProvider();
    }

    /// <summary>Builds the server logging pipeline at the given LOG_LEVEL.</summary>
    private static void Build(string logLevel) => _ = BuildProvider(logLevel);

    /// <summary>Configuration carrying only the level the server reads.</summary>
    private static IConfiguration Configuration(string logLevel) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ServerLogging.LogLevelSettingName] = logLevel,
            })
            .Build();
}
