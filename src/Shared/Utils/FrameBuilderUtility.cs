using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Plaintext frame builders for the peer-to-peer channel; wire framing is added
/// afterwards by <see cref="FrameCryptoUtility.EncodeFrame"/>.
/// </summary>
public static class FrameBuilderUtility
{
    /// <summary>
    /// Builds a plaintext frame wrapping messages: the header word, the
    /// serialized messages and the zeroed tail region that
    /// <see cref="FrameCryptoUtility.EncodeFrame"/> fills in.
    /// </summary>
    /// <param name="counter">Frame counter.</param>
    /// <param name="messages">Messages to wrap.</param>
    /// <param name="compressed">
    /// Whether the content region goes out as an LZSS stream with the
    /// compression marker set in the header word. The recorded host marks the
    /// roster run, the roster repeat and the post-join burst and leaves the
    /// short records that close them unmarked, so this is per run rather than
    /// a property of the peer.
    /// </param>
    public static byte[] BuildMessageFrame(
        ushort counter,
        IReadOnlyList<UdpMessage> messages,
        bool compressed = false)
    {
        var header = new byte[UdpCommandConstants.HeaderSize];
        BinaryUtility.WriteUInt16LittleEndian(
            header,
            0,
            compressed ? (ushort)(counter | UdpCommandConstants.CompressionMarker) : counter);
        var content = MessageCodecUtility.SerializeMessages(messages);
        return BinaryUtility.Concatenate(
            header,
            compressed ? LzssUtility.Compress(content) : content,
            new byte[UdpCommandConstants.TailSize]);
    }

    /// <summary>
    /// Splits messages into the groups one datagram can carry, in order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A datagram that outgrows <see cref="UdpCommandConstants.MaximumDatagramSize"/>
    /// is dropped whole, and every record on it with it.</b> The working host
    /// spreads a roster run across several frames to stay under the cap; this
    /// host wrote the same kind of run as one frame and the client answered none
    /// of its records, though it answered every frame before that one. The
    /// measurement is on the constant.
    /// </para>
    /// <para>
    /// A single record larger than a whole datagram's content region travels
    /// alone rather than being cut: its length is its own, and a frame is where
    /// a split belongs.
    /// </para>
    /// </remarks>
    /// <param name="messages">Messages to group, in order.</param>
    /// <returns>The groups, each one a datagram's content region.</returns>
    public static IReadOnlyList<IReadOnlyList<UdpMessage>> SplitByFrameSize(
        IReadOnlyList<UdpMessage> messages)
    {
        // **One packing rule, and it is the image's.** This used to add each record's own
        // size and compare the sum with the content region, while the write path appended
        // records under the four-byte alignment the image's buffer keeps — two figures for
        // one decision, and the aligned one is the larger.
        return Mgo2Server.Shared.Udp.OutboundDatagramBufferService.Split(messages);
    }

    /// <summary>Creates a message whose declared length matches its body.</summary>
    /// <param name="type">Message type.</param>
    /// <param name="body">Body bytes.</param>
    /// <param name="flags">Per-message flags byte.</param>
    public static UdpMessage MessageOf(ushort type, ReadOnlySpan<byte> body, byte flags = 0) =>
        UdpMessage.Create(type, body, flags);

    /// <summary>Creates the empty keep-alive message.</summary>
    public static UdpMessage KeepAliveMessage() =>
        UdpMessage.Create(UdpCommandConstants.KeepAlive, ReadOnlySpan<byte>.Empty);
}
