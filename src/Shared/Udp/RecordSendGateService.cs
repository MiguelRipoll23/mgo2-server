namespace Mgo2Server.Shared.Udp;

/// <summary>
/// The image's <c>gate_p2p_record_send</c> (<c>0x263530</c>): the one step between a record
/// that has been built and the datagram it is appended to.
/// </summary>
/// <remarks>
/// <para>
/// The decompiled body does three things and nothing else: it clears the copy's retransmit
/// byte at <c>+8</c>, refuses while the channel's state byte is not ready
/// (<c>*(byte *)(channel + 1) &lt;= 1</c>, a channel that has not connected), and hands the
/// record to <c>append_udp_datagram_to_send_buffer</c> (<c>0x2696d8</c>). The append's
/// refusal (<c>-1</c>) is answered with the channel's abort vector.
/// </para>
/// <para>
/// <b>This host's answer to a full datagram is the next datagram, not a dead channel.</b>
/// The image aborts the channel on a refusal, and the flush that follows starts a new
/// buffer; tearing a session down for a burst that is one record too long would drop a peer
/// this host is still writing to, so the refusal is handed back unchanged and the caller
/// starts a new frame — the same wire, without the session's death.
/// </para>
/// <para>
/// <b>The readiness verdict is the caller's, because the channel's state is.</b> The image
/// reads it from the channel structure, which its handshake has already moved past 1 by the
/// time a record reaches this gate; this host sends a handshake reply through the same
/// writer, so the verdict is passed in rather than guessed from the session's own flag.
/// </para>
/// </remarks>
public static class RecordSendGateService
{
    /// <summary>The image's refusal answer, <c>0xffffffff</c>.</summary>
    public const int Refused = -1;

    /// <summary>
    /// Appends one record to the datagram under construction, if the channel is ready and
    /// the record fits.
    /// </summary>
    /// <param name="channelReady">Whether the channel may send at all.</param>
    /// <param name="used">Bytes already in the datagram.</param>
    /// <param name="recordSize">Size of the record, its header included.</param>
    /// <returns>The datagram's new size, or <see cref="Refused"/>.</returns>
    public static int TryAppend(bool channelReady, int used, int recordSize) =>
        channelReady ? OutboundDatagramBufferService.AppendToBuffer(used, recordSize) : Refused;

    /// <summary>
    /// Appends one record to the datagram under construction, which is the packing rule the
    /// split is made of.
    /// </summary>
    /// <param name="used">Bytes already in the datagram.</param>
    /// <param name="recordSize">Size of the record, its header included.</param>
    /// <returns>The datagram's new size, or <see cref="Refused"/>.</returns>
    public static int TryAppend(int used, int recordSize) =>
        TryAppend(channelReady: true, used, recordSize);
}
