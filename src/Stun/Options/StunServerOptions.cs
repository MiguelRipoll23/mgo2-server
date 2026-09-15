using System.Net;
using System.Net.Sockets;
using Mgo2Server.Shared.Constants;
using Microsoft.Extensions.Configuration;

namespace Mgo2Server.Stun.Options;

/// <summary>Options that describe how the port-check responder runs.</summary>
public sealed class StunServerOptions
{
    /// <summary>Port the console dials. The next port is served as well.</summary>
    public int Port { get; set; } = PortConstants.StunPort;

    /// <summary>
    /// Address the responder answers the console on. It is the private address of
    /// this machine, the same one the name server resolves the local domains to.
    /// </summary>
    public string PrimaryAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// Second address of this machine, which the answer to a change-address
    /// request is sent from. Without it the responder can only move the port, and
    /// it tells the console something untrue; see the class remarks of the server.
    /// </summary>
    public string? SecondaryAddress { get; set; }

    /// <summary>Resolves the options from configuration, accepting the conventional variables.</summary>
    /// <param name="configuration">Configuration to read from.</param>
    /// <exception cref="ArgumentException">A configured address is not an IPv4 address.</exception>
    public static StunServerOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new StunServerOptions();

        if (int.TryParse(configuration["STUN_PORT"], out var port))
        {
            options.Port = port;
        }

        // The address the console dials is the one it was told to dial: the
        // advertised address, unless the responder is pointed somewhere else.
        var primarySettingName = "STUN_PRIMARY_ADDRESS";
        var primaryAddress = configuration[primarySettingName];
        if (string.IsNullOrWhiteSpace(primaryAddress))
        {
            primarySettingName = "ADVERTISED_ADDRESS";
            primaryAddress = configuration[primarySettingName];
        }

        if (!string.IsNullOrWhiteSpace(primaryAddress))
        {
            options.PrimaryAddress = primaryAddress.Trim();
        }

        var secondaryAddress = configuration["STUN_SECONDARY_ADDRESS"];
        if (!string.IsNullOrWhiteSpace(secondaryAddress))
        {
            options.SecondaryAddress = secondaryAddress.Trim();
        }

        ValidateAddress(primarySettingName, options.PrimaryAddress);

        if (options.SecondaryAddress is not null)
        {
            ValidateAddress("STUN_SECONDARY_ADDRESS", options.SecondaryAddress);
        }

        return options;
    }

    /// <summary>
    /// Rejects an address the responder cannot answer with. The console's port
    /// check asks for IPv4 addresses only, so an address of another family would
    /// be written into a reply it cannot read.
    /// </summary>
    /// <param name="settingName">Setting the address came from.</param>
    /// <param name="address">Address to check.</param>
    /// <exception cref="ArgumentException">The address is not an IPv4 address.</exception>
    private static void ValidateAddress(string settingName, string address)
    {
        if (!IPAddress.TryParse(address, out var parsed) ||
            parsed.AddressFamily is not AddressFamily.InterNetwork)
        {
            throw new ArgumentException(
                $"{settingName} '{address}' is not an IPv4 address",
                settingName);
        }
    }
}
