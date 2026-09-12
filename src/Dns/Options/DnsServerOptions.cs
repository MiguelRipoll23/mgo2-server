using Mgo2Server.Shared.Constants;
using Microsoft.Extensions.Configuration;

namespace Mgo2Server.Dns.Options;

/// <summary>Options that describe how the name server runs.</summary>
public sealed class DnsServerOptions
{
    /// <summary>Port the name server listens on.</summary>
    public int Port { get; set; } = PortConstants.DnsPort;

    /// <summary>Address the name server binds.</summary>
    public string ListeningIpAddress { get; set; } = "0.0.0.0";

    /// <summary>Address the locally resolved domains are answered with.</summary>
    public string ResolvedIpAddress { get; set; } = "0.0.0.0";

    /// <summary>Upstream name server every other query is forwarded to.</summary>
    public string AlternativeNameServer { get; set; } = "8.8.8.8";

    /// <summary>Port of the upstream name server.</summary>
    public int AlternativeNameServerPort { get; set; } = 53;

    /// <summary>Domains answered locally instead of being forwarded upstream.</summary>
    public List<string> LocalResolvedDomains { get; set; } = ["mgo2pc.com", "game.mgo2pc.com"];

    /// <summary>How long an upstream query may take before it is abandoned.</summary>
    public int ForwardTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>Resolves the options from configuration, accepting the conventional variables.</summary>
    /// <param name="configuration">Configuration to read from.</param>
    public static DnsServerOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new DnsServerOptions();

        if (int.TryParse(configuration["DNS_PORT"], out var port))
        {
            options.Port = port;
        }

        options.ListeningIpAddress = configuration["LISTENING_IP"] ?? options.ListeningIpAddress;
        options.ResolvedIpAddress = configuration["LISTENING_IP"] ?? options.ResolvedIpAddress;
        options.AlternativeNameServer =
            configuration["ALTERNATIVE_DNS_SERVER"] ?? options.AlternativeNameServer;

        if (int.TryParse(configuration["ALTERNATIVE_DNS_PORT"], out var alternativePort))
        {
            options.AlternativeNameServerPort = alternativePort;
        }

        var localResolvedDomains = configuration["LOCAL_RESOLVED_DOMAINS"];
        if (!string.IsNullOrWhiteSpace(localResolvedDomains))
        {
            options.LocalResolvedDomains = [.. localResolvedDomains
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(domain => domain.ToLowerInvariant())
                .Where(domain => domain.Length > 0)];
        }

        return options;
    }
}
