namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Default network ports used when a value is not supplied by configuration.
/// </summary>
public static class PortConstants
{
    /// <summary>Port of the HTTP REST API.</summary>
    public const int HttpPort = 80;

    /// <summary>
    /// Port of the same API over TLS. It is served only where a certificate is
    /// mounted, because the console's own client speaks the plaintext port.
    /// </summary>
    public const int HttpsPort = 443;

    /// <summary>
    /// Port the HTTP API serves the internal server-to-server gRPC endpoint on.
    /// The endpoint is internal to the deployment and is never the port the
    /// game clients or the public API are reached on.
    /// </summary>
    public const int InternalGrpcPort = 5743;

    /// <summary>Port of the UDP domain name server.</summary>
    public const int DnsPort = 53;

    /// <summary>
    /// Port of the UDP port-check responder. The responder also serves the next
    /// port, which is the alternate port of RFC 3489 section 8.1.
    /// </summary>
    public const int StunPort = 3478;

    /// <summary>Default port of the gate server.</summary>
    public const int GatePort = 5731;

    /// <summary>Default port of the account server.</summary>
    public const int AccountPort = 5732;

    /// <summary>Default port of the dedicated UDP gameplay host.</summary>
    public const int GameplayServerPort = 5731;
}
