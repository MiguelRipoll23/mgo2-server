using System.Net;

namespace Mgo2Server.Shared.Types;

/// <summary>
/// Negotiated peer-to-peer session with one peer, keyed by its remote endpoint.
/// </summary>
public sealed class PeerSession
{
    /// <summary>Remote endpoint this session is keyed by, formatted as "address:port".</summary>
    public required string RemoteAddress { get; init; }

    /// <summary>Endpoint frames are sent back to, which may differ from <see cref="RemoteAddress"/>.</summary>
    public required IPEndPoint DialBack { get; set; }

    /// <summary>Counter base the peer announced in its handshake.</summary>
    public required uint CounterBase { get; set; }

    /// <summary>Shared outbound frame counter; every outbound frame draws from it.</summary>
    public ushort OutboundCounter { get; set; }

    /// <summary>Session key: the peer's counter base exclusive-ORed with the host's counter base.</summary>
    public required uint SessionKey { get; set; }

    /// <summary>Character identifier the peer announced (its handshake peer identifier).</summary>
    public required uint PeerIdentifier { get; set; }

    /// <summary>Peer identifier the host announced to this peer (its own character identifier).</summary>
    public required uint HostPeerIdentifier { get; init; }

    /// <summary>True once the session key has been established.</summary>
    public bool Established { get; set; }

    /// <summary>Timestamp of the last datagram received from this peer.</summary>
    public long LastSeenAt { get; set; }

    /// <summary>
    /// Highest inbound frame sequence processed; zero means none yet. Every
    /// newly observed sequence is acknowledged and duplicates are not
    /// re-acknowledged. Gaps self-heal, because a lost frame returns as a
    /// re-send with a fresh sequence.
    /// </summary>
    public ushort LastInboundSequence { get; set; }

    /// <summary>
    /// Inbound acknowledgement type identifiers already seen. The joining
    /// client keeps re-sending its acknowledgement until it sees ours, so
    /// duplicates are expected and must not re-trigger an acknowledgement.
    /// </summary>
    public HashSet<ushort> SeenAcknowledgements { get; } = [];
}
