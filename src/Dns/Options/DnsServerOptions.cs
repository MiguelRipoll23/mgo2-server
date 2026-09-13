using Mgo2Server.Shared.Constants;
using Microsoft.Extensions.Configuration;

namespace Mgo2Server.Dns.Options;

/// <summary>Options that describe how the name server runs.</summary>
public sealed class DnsServerOptions
{
    /// <summary>Port the name server listens on.</summary>
    public int Port { get; set; } = PortConstants.DnsPort;

    /// <summary>
    /// Address a query from another machine is answered with for a local
    /// domain: the private address the clients reach this machine on.
    /// </summary>
    public string ResolvedIpAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// Address an IPv6 address record for a local domain is answered with when
    /// the query comes from another machine. It defaults to the IPv6 twin of
    /// <see cref="ResolvedIpAddress"/>.
    /// </summary>
    public string ResolvedIpv6Address { get; set; } = "::";

    /// <summary>
    /// Address a query from this machine is answered with for a local domain.
    /// The default, the wildcard address, is what a client on this machine
    /// connects to through the loopback interface.
    /// </summary>
    public string LocalResolvedIpAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// Address an IPv6 address record for a local domain is answered with when
    /// the query comes from this machine. It defaults to the IPv6 twin of
    /// <see cref="LocalResolvedIpAddress"/>.
    /// </summary>
    public string LocalResolvedIpv6Address { get; set; } = "::";

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

        options.ResolvedIpAddress = configuration["PUBLIC_IP"] ?? options.ResolvedIpAddress;
        options.LocalResolvedIpAddress =
            configuration["LOCAL_RESOLVED_IP"] ?? options.LocalResolvedIpAddress;

        // An IPv6 question is answered with the twin of the address the IPv4
        // questions are answered with: the wildcard address when the wildcard
        // is resolved, and the IPv4 address in its IPv6-mapped form otherwise.
        // Either one is overridden by its own variable when it is set.
        options.ResolvedIpv6Address =
            configuration["PUBLIC_IPV6"] ?? options.ResolvedIpv6Address;
        options.LocalResolvedIpv6Address =
            configuration["LOCAL_RESOLVED_IPV6"] ?? options.LocalResolvedIpv6Address;

        if (configuration["PUBLIC_IPV6"] is null)
        {
            options.ResolvedIpv6Address = options.ResolvedIpAddress is "0.0.0.0"
                ? "::"
                : MapToIPv6Twin(options.ResolvedIpAddress);
        }

        if (configuration["LOCAL_RESOLVED_IPV6"] is null)
        {
            options.LocalResolvedIpv6Address = options.LocalResolvedIpAddress is "0.0.0.0"
                ? "::"
                : MapToIPv6Twin(options.LocalResolvedIpAddress);
        }
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

    /// <summary>
    /// Writes an IPv4 address as its IPv6-mapped form, the twelve-byte prefix
    /// followed by the four bytes of the address, so a question for an IPv6
    /// address record can be answered with the same address the IPv4
    /// questions get.
    /// </summary>
    /// <param name="ipv4Address">Address to map, in dotted-quad form.</param>
    /// <exception cref="FormatException">The address is not dotted-quad form.</exception>
    private static string MapToIPv6Twin(string ipv4Address)
    {
        var octets = ipv4Address.Split('.');

        if (octets.Length != 4 || octets.Any(octet => octet.Length is 0 || octet.Length > 3 || octet.Any(character => character is < '0' or > '9')))
        {
            throw new FormatException($"'{ipv4Address}' is not an IPv4 address in dotted-quad form");
        }

        var octetValues = octets.Select(int.Parse).ToArray();

        return $"::ffff:{octetValues[0]:X2}{octetValues[1]:X2}:{octetValues[2]:X2}{octetValues[3]:X2}".ToLowerInvariant();
    }
}
