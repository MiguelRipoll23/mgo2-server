namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Default network ports used when a value is not supplied by configuration.
/// </summary>
public static class PortConstants
{
    /// <summary>Port of the HTTP REST API.</summary>
    public const int HttpPort = 80;

    /// <summary>Port of the UDP domain name server.</summary>
    public const int DnsPort = 53;

    /// <summary>Default port of the gate server.</summary>
    public const int GatePort = 5731;

    /// <summary>Default port of the account server.</summary>
    public const int AccountPort = 5732;

    /// <summary>Default port of the dedicated UDP gameplay host.</summary>
    public const int GameplayServerPort = 5730;
}
