namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Identity the dedicated gameplay host presents on the peer-to-peer channel.
/// </summary>
public static class UdpHostIdentityConstants
{
    /// <summary>
    /// The peer identifier on the wire is the sender's own identifier: the
    /// handshake reply must carry the host's character identifier, because the
    /// joining client's drain gate compares it against the peer descriptor
    /// stored in its dial session rather than echoing its own identifier.
    /// </summary>
    public const uint HostPeerIdentifier = 1;

    /// <summary>Counter base the host announces in its handshake.</summary>
    public const uint HostCounterBase = 0x12345678;
}
