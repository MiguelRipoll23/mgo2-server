namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Message types carried in the content region of a peer-to-peer frame.
/// </summary>
public static class UdpCommandConstants
{
    /// <summary>Handshake message; also the base of the reliable class.</summary>
    public const ushort Handshake = 0x1000;

    /// <summary>
    /// Keep-alive message mirrored back to the peer. It is id 0 in the
    /// keep-alive class: the serializer builds a wire type from a twelve-bit
    /// id plus flag-derived class bits, so this value is `0 | 0x5000`.
    /// </summary>
    public const ushort KeepAlive = 0x5000;

    /// <summary>Player-profile record sent by the joining peer.</summary>
    public const ushort PlayerProfile = 0x1001;

    /// <summary>
    /// Join request the joiner opens the exchange with. It carries the same
    /// player record as <see cref="PlayerProfile"/> under a different tag, and
    /// it is the one a live console actually sends first (hdr 0x8003, 149-byte
    /// body opening with 0x02). §6.4 describes the roster record that answers
    /// it, not this one. The recorded host also sends its own roster entry under
    /// this tag, so it is both what a joiner opens with and what a host answers
    /// its own entry with.
    /// </summary>
    public const ushort JoinRequest = 0x9001;

    /// <summary>
    /// Zero-length record the recorded host puts at the head of its roster
    /// answer, ahead of its own entry and the joining players'.
    /// </summary>
    /// <remarks>
    /// The live capture puts this type first in every roster frame, and shows
    /// it arriving from a peer 481 times over a round with a fourth byte that
    /// climbs monotonically to 194 rather than resetting. What it says is
    /// **[U]**, and in particular it is <em>not</em> the acknowledgement: its
    /// identifier never varies, so it names no sequence to acknowledge. See
    /// docs/protocol/UDP_GAME_CAPTURE.md §3. It is sent at the head of a roster
    /// because the live server sends it there and this server does not invent
    /// records a peer never wrote, not because its meaning is known, and it is
    /// not answered when it arrives.
    /// </remarks>
    public const ushort RosterHead = 0x5001;

    /// <summary>
    /// One-byte in-game control record. The live host writes nearly all of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The capture holds 181 of these, <b>176 from the server to the client</b>
    /// and 5 the other way, so this is predominantly a record the host emits
    /// rather than one it only receives. Two go out inside the post-join burst,
    /// with bodies <c>0x24</c> and <c>0x02</c>.
    /// </para>
    /// <para>
    /// The body is a small counter rather than an identifier — sixteen distinct
    /// values across the round — and it arrives with the same fourth-byte
    /// counter <see cref="RosterHead"/> carries. What any of them *means* is
    /// **[U]**. The server recognises the type so it is not logged as an unknown
    /// command, and an inbound one is not answered: answering a record this
    /// server cannot read is how <c>0x43CA</c>/<c>0x43CB</c> and <c>0x4442</c>
    /// went wrong in this project.
    /// </para>
    /// </remarks>
    public const ushort InGameControl = 0xd001;

    /// <summary>
    /// Fixed-length state blob that closes the post-join burst.
    /// </summary>
    /// <remarks>
    /// Every one of the 141 blobs the round contains is 22 bytes and every one
    /// of them goes server to client. Eighteen are zero, then a marker, a zero,
    /// and a sixteen-bit big-endian value that is the only part that moves.
    /// <see cref="GameplayServer.Rooms.PostJoinBurstService"/> builds it.
    /// </remarks>
    public const ushort GameStateBlob = 0x0002;

    /// <summary>Reliable-class base: an acknowledgement of frame sequence N travels as this value ORed with N.</summary>
    public const ushort AcknowledgementClass = 0x1000;

    /// <summary>Mask applied to the acknowledged frame sequence.</summary>
    public const ushort AcknowledgementIdentifierMask = 0x0fff;

    /// <summary>Size of the frame header in bytes.</summary>
    public const int HeaderSize = 2;

    /// <summary>Size of the frame tail digest in bytes.</summary>
    public const int TailSize = 10;

    /// <summary>Size of a message header in bytes: type, length and flags.</summary>
    public const int MessageHeaderSize = 4;

    /// <summary>Total frame overhead added around the content region.</summary>
    public const int FrameOverhead = HeaderSize + TailSize;

    /// <summary>Header bit marking the content region as a raw LZSS stream.</summary>
    public const ushort CompressionMarker = 0x8000;

    /// <summary>Mask that strips the compression marker from the header counter.</summary>
    public const ushort CounterMask = 0x7fff;

    /// <summary>
    /// First record identifier read under the in-game framing. A record below
    /// this is a tick record, whose length byte counts the body plus the
    /// attribute class; a record at or above it is a session record, whose
    /// length byte is the body length. Both share the wire and the header
    /// shape, and the identifier is what tells them apart.
    /// </summary>
    /// <remarks>
    /// Established from a live dedicated-server game
    /// (docs/protocol/UDP_GAME_CAPTURE.md §2): reading every length as a body
    /// length walks 291 of 19018 frames to their last byte, reading every
    /// length as body+1 walks 17423, and partitioning on this threshold walks
    /// 18728.
    /// </remarks>
    public const int TickRecordThreshold = 0x1000;

    /// <summary>Body of an acknowledgement message.</summary>
    public static ReadOnlySpan<byte> AcknowledgementBody => [0x00];

    /// <summary>Value of the send-attempt field on a first acknowledgement.</summary>
    public const byte AcknowledgementFirstAttempt = 1;

    /// <summary>Ring buffer size of the LZSS decompressor.</summary>
    public const int LzssRingSize = 0x200;

    /// <summary>Maximum output size of the LZSS decompressor.</summary>
    public const int LzssMaximumOutput = 0x800;

    /// <summary>Wire type of the acknowledgement covering the frame with sequence <paramref name="sequence"/>.</summary>
    /// <param name="sequence">Sequence of the acknowledged frame.</param>
    public static ushort AcknowledgementTypeOf(ushort sequence) =>
        (ushort)(AcknowledgementClass | (sequence & AcknowledgementIdentifierMask));
}
