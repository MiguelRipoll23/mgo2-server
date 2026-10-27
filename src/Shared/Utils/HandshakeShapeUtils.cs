using System.Net;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Shapes a handshake body takes on the peer-to-peer channel.
/// </summary>
public enum HandshakeShape
{
    /// <summary>
    /// Native client: no endpoint pairs, so the peer is answered at the endpoint
    /// its datagram came from.
    /// </summary>
    Native,

    /// <summary>
    /// Emulated client: the public and private endpoint pairs the peer listens
    /// on, with the pair that matches the observed endpoint preferred.
    /// </summary>
    Emulator,
}

/// <summary>
/// Reads the native and emulated handshake shapes together: which shape a body
/// is, where to answer it, and which endpoint bytes a roster echoes for it.
/// </summary>
/// <remarks>
/// The two clients carry the same peer identifier, counter base and module
/// magic. They differ only in the endpoint list: the emulated client sends two
/// pairs after the pair count, while the native client sends none and lets the
/// receiver answer the source of the datagram.
/// </remarks>
public static class HandshakeShapeUtils
{
    /// <summary>Reads the shape a parsed handshake body was sent in.</summary>
    /// <param name="handshake">Parsed handshake body.</param>
    /// <returns>The shape the body belongs to.</returns>
    public static HandshakeShape ShapeOf(HandshakeBody handshake) =>
        handshake.Pairs.Count == 0 ? HandshakeShape.Native : HandshakeShape.Emulator;

    /// <summary>Whether a message body is a valid handshake in either shape.</summary>
    /// <param name="body">Message body to test.</param>
    /// <returns><c>true</c> when the body parses as a handshake.</returns>
    public static bool IsHandshakeBody(ReadOnlySpan<byte> body) =>
        HandshakeBodyUtils.ParseHandshakeBody(body) is not null;

    /// <summary>
    /// Picks the endpoint to answer a peer at, from the pairs it advertised.
    /// </summary>
    /// <remarks>
    /// The recorded host answered the joiner's second advertised pair. A pair
    /// equal to the endpoint the datagram came from is preferred, because that
    /// endpoint is known to reach us; when no pair matches, the first valid
    /// pair is used. A native handshake advertises none, so the caller falls
    /// back to the observed endpoint.
    /// </remarks>
    /// <param name="handshake">The peer's handshake.</param>
    /// <param name="observed">Endpoint the handshake actually arrived from.</param>
    /// <returns>The endpoint to answer at, or <c>null</c> when none was advertised.</returns>
    public static IPEndPoint? DialBackEndpoint(HandshakeBody handshake, IPEndPoint observed)
    {
        IPEndPoint? first = null;

        foreach (var pair in handshake.Pairs)
        {
            if (pair.Port == 0 || !IPAddress.TryParse(pair.Address, out var address))
            {
                continue;
            }

            var endpoint = new IPEndPoint(address, pair.Port);
            if (endpoint.Equals(observed))
            {
                return endpoint;
            }

            first ??= endpoint;
        }

        return first;
    }

    /// <summary>
    /// Endpoint bytes a roster echoes for a peer.
    /// </summary>
    /// <remarks>
    /// An emulated handshake carries both pairs itself. A native handshake
    /// carries none, so the observed endpoint stands in for both: it is the one
    /// address the peer has just proved reaches this host.
    /// </remarks>
    /// <param name="handshake">The peer's handshake.</param>
    /// <param name="observed">Endpoint the handshake actually arrived from.</param>
    /// <returns>Twelve packed endpoint bytes, or an empty array when none are valid.</returns>
    public static byte[] AddressData(HandshakeBody handshake, IPEndPoint observed)
    {
        if (handshake.Pairs.Count >= 2)
        {
            return EndpointDataUtils.EndpointData(handshake.Pairs);
        }

        var observedPair = EndpointDataUtils.EndpointPairBytes(
            observed.Address.ToString(),
            observed.Port);
        return observedPair.Length == 0 ? [] : [.. observedPair, .. observedPair];
    }

    /// <summary>Builds a handshake reply in the same shape as the peer's request.</summary>
    /// <param name="shape">Shape the peer used.</param>
    /// <param name="peerIdentifier">Identifier of this host.</param>
    /// <param name="counterBase">Counter base this host announced.</param>
    /// <param name="advertiseAddress">Public address to advertise.</param>
    /// <param name="advertisePort">Port to advertise.</param>
    /// <param name="privateAddress">Private address to advertise, when there is one.</param>
    /// <returns>The reply body for the peer's shape.</returns>
    public static byte[] BuildReplyBody(
        HandshakeShape shape,
        uint peerIdentifier,
        uint counterBase,
        string advertiseAddress,
        int advertisePort,
        string? privateAddress = null) =>
        HandshakeBodyUtils.BuildHandshakeBody(
            peerIdentifier,
            counterBase,
            advertiseAddress,
            advertisePort,
            privateAddress,
            advertisePairs: shape == HandshakeShape.Emulator);
}
