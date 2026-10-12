using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;

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
    /// <summary>
    /// Counter base this host announces, drawn once per process.
    /// </summary>
    /// <remarks>
    /// The captures show a fresh, apparently random 32-bit base on every
    /// session (game1 host <c>0x3c8a65d6</c>, game2 host <c>0x4f4ed224</c>),
    /// not a fixed constant, so the base is drawn here rather than written as
    /// one. It is announced in the handshake, so the peer derives the same
    /// session key from it and nothing else has to know the value in advance.
    /// </remarks>
    private readonly uint counterBase = DrawCounterBase();
    private readonly string? privateAddress = ResolvePrivateAddress(options.Value);

    private uint? peerIdentifier;

    /// <summary>
    /// Reports the endpoints this host will advertise, so a host with no
    /// distinct private address says so at startup rather than discovering it
    /// as an unanswered join.
    /// </summary>
    /// <param name="logger">Logger to write the notice to.</param>
    public void LogAdvertisedEndpoints(ILogger logger)
    {
        if (privateAddress is not null)
        {
            logger.LogInformation(
                "Gameplay host endpoints: public {PublicAddress}:{Port}, private {PrivateAddress}:{Port}",
                AdvertisedAddress,
                AdvertisedPort,
                privateAddress,
                AdvertisedPort);
            return;
        }

        // One endpoint in both slots is not what the captured hosts send, and a
        // client reading the roster is handed the same address twice. The wire
        // still carries the advertised address -- a zeroed pair would be worse,
        // being no address at all -- so this is a notice, not a refusal.
        logger.LogWarning(
            "No private address to advertise: every address on this machine is the advertised one "
            + "({AdvertisedAddress}). The handshake and the roster entry will carry that endpoint in "
            + "both slots, which is not what the recorded hosts send. Set a distinct "
            + "GameplayServerPrivateAddress to advertise a LAN address beside the public one.",
            AdvertisedAddress);
    }

    /// <summary>Identifier of the character this host plays as.</summary>
    public uint PeerIdentifier => peerIdentifier
        ?? throw new InvalidOperationException("The gameplay server character has not been resolved.");

    /// <summary>
    /// The character identifier, or <c>null</c> while it is still being
    /// resolved. The packet-replay console comes up before the database step
    /// that resolves it, and it reports what it does not yet have rather than
    /// failing the request that asked.
    /// </summary>
    public uint? TryPeerIdentifier() => peerIdentifier;


    /// <summary>Sets the peer identifier to the configured character's database identifier.</summary>
    /// <param name="characterIdentifier">Identifier of the configured character.</param>
    public void SetCharacterIdentifier(int characterIdentifier)
    {
        if (characterIdentifier <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(characterIdentifier),
                "The gameplay server character identifier must be positive.");
        }

        peerIdentifier = checked((uint)characterIdentifier);
    }

    /// <summary>Counter base this host announces.</summary>
    public uint CounterBase => counterBase;

    /// <summary>
    /// Private address this host announces as the second pair of its handshake
    /// and roster records.
    /// </summary>
    /// <remarks>
    /// The captured hosts advertise a public address first and a private LAN
    /// address second, and the two differ -- game1's host entry holds
    /// <c>99.66.131.177</c> and then <c>10.104.10.28</c>. The value is resolved
    /// in order: the configured
    /// <see cref="ServerOptions.GameplayServerPrivateAddress"/>, then the
    /// machine's detected private address.
    /// <para>
    /// Detection is asked for an address <em>other</em> than the one already
    /// advertised, so a host whose only reachable address is its public one is
    /// not handed that address twice. Such a host has no private address to
    /// offer, and the constructor says so rather than letting the same endpoint
    /// stand in for both -- which is what this did before, silently, on any
    /// single-address machine running with a LAN address configured as its
    /// advertised one.
    /// </para>
    /// </remarks>
    public string PrivateAddress => privateAddress ?? AdvertisedAddress;

    /// <summary>
    /// Draws the host's counter base from the cryptographic random source.
    /// </summary>
    /// <returns>A four-byte base, read little-endian like the one on the wire.</returns>
    private static uint DrawCounterBase()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        RandomNumberGenerator.Fill(bytes);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    /// <summary>
    /// Resolves the private address the host advertises, configuring it first
    /// and detecting it second.
    /// </summary>
    /// <param name="options">Options of this instance.</param>
    /// <returns>
    /// The private address, or <c>null</c> when none is configured and none can
    /// be detected that differs from the advertised address.
    /// </returns>
    private static string? ResolvePrivateAddress(ServerOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.GameplayServerPrivateAddress))
        {
            return options.GameplayServerPrivateAddress;
        }

        return LocalAddressUtils.TryGetPrivateAddress(options.GameplayServerAdvertisedAddress);
    }

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
    /// Endpoint pair bytes this host registered and advertises. The roster uses
    /// the same public/private endpoints as the host handshake.
    /// </summary>
    public byte[] AddressData
    {
        get
        {
            var handshakeBody = FrameBuilderUtility.BuildHandshakeBody(
                PeerIdentifier,
                CounterBase,
                AdvertisedAddress,
                AdvertisedPort,
                PrivateAddress);
            var handshake = FrameBuilderUtility.ParseHandshakeBody(handshakeBody);
            return handshake is null ? [] : FrameBuilderUtility.EndpointData(handshake.Pairs);
        }
    }

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
