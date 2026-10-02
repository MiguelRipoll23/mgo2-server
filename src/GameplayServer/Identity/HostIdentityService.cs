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

    /// <summary>
    /// Character name this host announces in its player-profile record.
    /// </summary>
    /// <remarks>
    /// The record's name field is the <em>character</em> name, not the account
    /// name the host logs in with: the capture shows the joining player's own
    /// character name echoed back, and the two are separate values
    /// (<see cref="ServerOptions.GameplayServerCharacterName"/> against
    /// <see cref="ServerOptions.GameplayServerAccountName"/>).
    /// </remarks>
    public string CharacterName => options.Value.GameplayServerCharacterName;

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
