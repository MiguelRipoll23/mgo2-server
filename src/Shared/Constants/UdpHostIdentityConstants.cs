namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Identity the dedicated gameplay host presents on the peer-to-peer channel.
/// </summary>
public static class UdpHostIdentityConstants
{
    /// <summary>Counter base the host announces in its handshake.</summary>
    public const uint HostCounterBase = 0x12345678;
}
