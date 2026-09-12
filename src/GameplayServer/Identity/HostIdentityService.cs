using Mgo2Server.Shared.Constants;

namespace Mgo2Server.GameplayServer.Identity;

/// <summary>
/// Identity the gameplay server presents on the peer-to-peer channel: its
/// character identifier, which the joining client gates on, and the counter
/// base that feeds the session key.
/// </summary>
public sealed class HostIdentityService
{
    /// <summary>Identifier of the character this host plays as.</summary>
    public uint PeerIdentifier => UdpHostIdentityConstants.HostPeerIdentifier;

    /// <summary>Counter base this host announces.</summary>
    public uint CounterBase => UdpHostIdentityConstants.HostCounterBase;
}
