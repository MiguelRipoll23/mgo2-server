using Microsoft.Extensions.Options;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Options;

namespace Mgo2Server.GameplayServer.Identity;

/// <summary>
/// Identity the gameplay server presents on the peer-to-peer channel: its
/// character identifier, which the joining client gates on, the counter base
/// that feeds the session key, and the names it puts in its own player-profile
/// record.
/// </summary>
/// <param name="options">Options of this instance.</param>
public sealed class HostIdentityService(IOptions<ServerOptions> options)
{
    /// <summary>Identifier of the character this host plays as.</summary>
    public uint PeerIdentifier => UdpHostIdentityConstants.HostPeerIdentifier;

    /// <summary>Counter base this host announces.</summary>
    public uint CounterBase => UdpHostIdentityConstants.HostCounterBase;

    /// <summary>Character identifier narrowed to the single byte the record carries.</summary>
    public byte ProfileCharacterIdentifier => (byte)(PeerIdentifier & 0xff);

    /// <summary>Account name this host announces in its player-profile record.</summary>
    public string AccountName => options.Value.GameplayServerAccountName;

    /// <summary>Clan name this host announces; empty when it runs without one.</summary>
    public string ClanName => options.Value.GameplayServerClanName ?? string.Empty;

    /// <summary>
    /// Address this host puts in its own handshake. It is the configured
    /// advertised address rather than the address a datagram arrived from,
    /// because behind a load balancer the two are different and only the
    /// configured one is dialable by the peer.
    /// </summary>
    public string AdvertisedAddress => options.Value.GameplayServerAdvertisedAddress;

    /// <summary>Port this host puts in its own handshake and binds.</summary>
    public int AdvertisedPort => options.Value.GameplayServerPort;
}
