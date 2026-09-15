namespace Mgo2Server.Shared.Options;

/// <summary>
/// Describes where a server sends its metrics. Telemetry is off unless it is
/// asked for, so a server started without the settings exports nothing and the
/// metric instruments record into the void.
/// </summary>
public sealed class TelemetryOptions
{
    /// <summary>Port the OTLP/gRPC collector listens on by default.</summary>
    public const int DefaultPort = 4317;

    /// <summary>Whether this server exports its metrics over OpenTelemetry.</summary>
    public bool Enabled { get; set; }

    /// <summary>Host of the OTLP/gRPC collector the metrics are sent to.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Port of the OTLP/gRPC collector the metrics are sent to.</summary>
    public int Port { get; set; } = DefaultPort;

    /// <summary>Endpoint the OTLP/gRPC exporter connects to.</summary>
    public string Endpoint => $"http://{Host}:{Port}";

    /// <summary>Rejects a collector endpoint that cannot be reached.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the port is out of range.</exception>
    public void Validate()
    {
        if (Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException(
                $"OTEL_PORT must be a port between 1 and 65535. Received '{Port}'.");
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException(
                "OTEL_HOST must be the host of the OpenTelemetry collector.");
        }
    }

    /// <summary>
    /// Reads the settings from the flat environment variables the servers are
    /// configured with. Telemetry stays off unless OTEL_ENABLED is a true value.
    /// </summary>
    /// <param name="configuration">Configuration to read from.</param>
    /// <returns>The resolved options.</returns>
    public static TelemetryOptions From(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var options = new TelemetryOptions
        {
            Enabled = bool.TryParse(configuration["OTEL_ENABLED"], out var enabled) && enabled,
            Host = configuration["OTEL_HOST"]?.Trim() is { Length: > 0 } host ? host : "localhost",
            Port = int.TryParse(configuration["OTEL_PORT"], out var port) ? port : DefaultPort,
        };

        if (options.Enabled)
        {
            options.Validate();
        }

        return options;
    }
}
