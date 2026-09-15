using System.Diagnostics.Metrics;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the metrics the servers publish: their names are the contract with
/// the Grafana Alloy configuration, and a change to one of them silently stops
/// the dashboards from updating.
/// </summary>
public sealed class ServerMetricsServiceTests
{
    [Fact]
    public void Records_the_totals_under_the_documented_metric_names()
    {
        var measurements = new Dictionary<string, (long Value, object? Lobby)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, activeListener) =>
        {
            if (instrument.Meter.Name == ServerMetricsService.MeterName)
            {
                activeListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            object? lobby = null;
            foreach (var tag in tags)
            {
                if (tag.Key == ServerMetricsService.LobbyAttributeName)
                {
                    lobby = tag.Value;
                }
            }

            measurements[instrument.Name] = (value, lobby);
        });
        listener.Start();

        using var metricsService = new ServerMetricsService(UnusedContextFactory.Instance);
        metricsService.RecordTotalUsers(42);
        metricsService.RecordTotalPlayers(3, "Free Battle");
        metricsService.RecordTotalMatches(7, "Free Battle");

        Assert.Equal(42, measurements[ServerMetricsService.TotalUsersMetricName].Value);
        Assert.Equal(3, measurements[ServerMetricsService.TotalPlayersMetricName].Value);
        Assert.Equal(7, measurements[ServerMetricsService.TotalMatchesMetricName].Value);

        // The accounts are not tied to a lobby; the players and the matches are,
        // which is what lets a dashboard filter them by lobby.
        Assert.Null(measurements[ServerMetricsService.TotalUsersMetricName].Lobby);
        Assert.Equal("Free Battle", measurements[ServerMetricsService.TotalPlayersMetricName].Lobby);
        Assert.Equal("Free Battle", measurements[ServerMetricsService.TotalMatchesMetricName].Lobby);
    }

    [Fact]
    public void Publishes_the_endpoint_of_the_default_collector_port()
    {
        var options = new TelemetryOptions();

        Assert.False(options.Enabled);
        Assert.Equal(4317, options.Port);
        Assert.Equal("http://localhost:4317", options.Endpoint);
    }

    [Fact]
    public void Rejects_an_out_of_range_port()
    {
        var options = new TelemetryOptions { Enabled = true, Port = 70000 };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    /// <summary>Context factory that a test recording a value directly never uses.</summary>
    private sealed class UnusedContextFactory : IDbContextFactory<Mgo2DatabaseContext>
    {
        /// <summary>One factory is enough; nothing is created through it.</summary>
        public static readonly UnusedContextFactory Instance = new();

        /// <inheritdoc />
        public Mgo2DatabaseContext CreateDbContext() =>
            throw new NotSupportedException("A test never reads the database.");

        /// <inheritdoc />
        public Task<Mgo2DatabaseContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("A test never reads the database.");
    }
}
