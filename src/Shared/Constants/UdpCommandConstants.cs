namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Message types carried in the content region of a peer-to-peer frame.
/// </summary>
public static class UdpCommandConstants
{
    /// <summary>Handshake message; also the base of the reliable class.</summary>
    public const ushort Handshake = 0x1000;

    /// <summary>Keep-alive message mirrored back to the peer.</summary>
    public const ushort KeepAlive = 0x5000;

    /// <summary>Player-profile record sent by the joining peer.</summary>
    public const ushort PlayerProfile = 0x1001;

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
