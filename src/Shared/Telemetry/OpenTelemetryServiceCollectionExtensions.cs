using Mgo2Server.Shared.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;

namespace Mgo2Server.Shared.Telemetry;

/// <summary>
/// Wires the OpenTelemetry integration every server shares: the metrics service
/// and, when telemetry is enabled, the OTLP/gRPC exporter that carries its
/// instruments to the collector.
/// </summary>
public static class OpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the metrics service and, when OTEL_ENABLED asks for it,
    /// configures the OTLP exporter that sends the metrics over gRPC. A server
    /// started without telemetry gets the service but no exporter, so no
    /// OpenTelemetry integration is configured and the instruments stay silent.
    /// </summary>
    /// <param name="services">Container to register with.</param>
    /// <param name="configuration">Configuration the settings are read from.</param>
    public static IServiceCollection AddServerTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName = "mgo2-server")
    {
        services.TryAddSingleton<ServerMetricsService>();

        var options = TelemetryOptions.From(configuration);
        if (!options.Enabled)
        {
            return services;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics => metrics
                .AddMeter(ServerMetricsService.MeterName)
                .AddOtlpExporter(
                    exporter =>
                    {
                        exporter.Endpoint = new Uri(options.Endpoint);
                        exporter.Protocol = OtlpExportProtocol.Grpc;
                    }))
            .WithLogging(logging => logging
                .AddOtlpExporter(
                    exporter =>
                    {
                        exporter.Endpoint = new Uri(options.Endpoint);
                        exporter.Protocol = OtlpExportProtocol.Grpc;
                    }));

        return services;
    }

    /// <summary>
    /// Builds the meter provider so the SDK starts exporting. The generic host
    /// builds it in StartAsync, which the console servers never call, so an
    /// entry point that wants its metrics exported activates it here. A server
    /// without telemetry has no provider, in which case this does nothing.
    /// </summary>
    /// <param name="services">Container the provider comes from.</param>
    public static void ActivateServerTelemetry(this IServiceProvider services) =>
        _ = services.GetService<MeterProvider>();
}
