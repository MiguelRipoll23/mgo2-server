using System.Net;
using Mgo2Server.Shared.Constants;

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

    /// <summary>Serializes outbound counter allocation and writes for this session.</summary>
    public Lock OutboundGate { get; } = new();

    /// <summary>Session key: the peer's counter base exclusive-ORed with the host's counter base.</summary>
    public required uint SessionKey { get; set; }

    /// <summary>Character identifier the peer announced (its handshake peer identifier).</summary>
    public required uint PeerIdentifier { get; set; }

    /// <summary>Public and private IPv4 endpoint pairs the peer announced in its handshake, in roster wire format.</summary>
    public byte[] PeerAddressData { get; set; } = [];

    /// <summary>Peer identifier the host announced to this peer (its own character identifier).</summary>
    public required uint HostPeerIdentifier { get; init; }

    /// <summary>True once the session key has been established.</summary>
    public bool Established { get; set; }

    /// <summary>
    /// True once a newer session has replaced this one for the same peer.
    /// </summary>
    /// <remarks>
    /// A peer that starts its handshake over gets a fresh session, and the
    /// workers of the old one are watching this object: they are waiting on a
    /// report or on a round for a peer that has already told this host to start
    /// again. Retiring it is what ends them, because nothing else does — there
    /// is no close to watch, and the peer keeps sending, so the idle window
    /// would never run out for the object nobody reads any more.
    /// </remarks>
    public bool Retired { get; set; }

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

    private readonly HashSet<(ushort Type, byte Ordinal)> acknowledgedRecords = [];

    /// <summary>Records of ours this peer has acknowledged, each with its fourth byte.</summary>
    public IReadOnlyCollection<(ushort Type, byte Ordinal)> AcknowledgedRecords => acknowledgedRecords;

    /// <summary>
    /// Notes that the peer answered one of our records, and reports whether it
    /// is the first time this record has been answered.
    /// </summary>
    /// <remarks>
    /// A client answers a roster run once and then stops, so the count is the
    /// evidence and the boolean only keeps a re-sent answer from being logged
    /// twice. Nothing here changes what is sent: an inbound answer needs no
    /// reply, and treating it as an unhandled record is what used to log a
    /// roster acknowledgement as a dropped message.
    /// </remarks>
    /// <param name="recordType">Identifier of the record being answered, answer bit cleared.</param>
    /// <param name="ordinal">Fourth byte of the answer, which names the record's place.</param>
    /// <returns><c>true</c> when this record had not been acknowledged before.</returns>
    public bool NoteAcknowledgement(ushort recordType, byte ordinal) =>
        acknowledgedRecords.Add((recordType, ordinal));

    /// <summary>
    /// Whether the peer acknowledged a player-profile record, which is the only
    /// acknowledgement a roster run can draw.
    /// </summary>
    /// <remarks>
    /// The roster opens with the same <c>0x5001</c> that would otherwise be the
    /// answer to the joiner's own profile, so an accepted roster and an accepted
    /// profile are the same event on the wire and cannot be told apart here.
    /// That is the point: this is the flag that says the client took the roster,
    /// and its absence is the whole of the stalled join.
    /// </remarks>
    public bool RosterAcknowledged =>
        acknowledgedRecords.Any(acknowledged => acknowledged.Type
            is UdpCommandConstants.PlayerProfile or UdpCommandConstants.JoinRequest);
}
