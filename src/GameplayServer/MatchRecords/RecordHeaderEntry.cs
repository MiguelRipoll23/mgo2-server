using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The handle entry a record is built on, as the image holds it before the record is sent:
/// the identifier at <c>+4</c>, the state byte at <c>+9</c>, the flags byte at <c>+0xa</c>
/// and the channel byte at <c>+0xd</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are the four fields <c>P2P_SetRecordHeaderField</c> (<c>0x26b2b0</c>) writes,
/// at the offsets it writes them.</b> The image's builders do not assemble a wire header
/// byte by byte: they hold an entry and stamp its fields, and
/// <c>P2P_SendRecord</c> (<c>0x26b778</c>) copies the header word out of the entry
/// unchanged when it sends. <see cref="RecordHeaderFieldService"/> is that stamp, and this
/// is the entry it stamps.
/// </para>
/// <para>
/// <b>The fourth wire byte is not one of the four, and that is not an omission.</b> The
/// decompiled setter writes no such field: selector 0 only owns the low nibble of
/// <c>+0xa</c>, and the byte is stamped by the send path instead — <c>P2P_SendRecord</c>
/// (<c>0x26b778</c>) reads the stream's counter from the entry, writes it into the record
/// it queues, and increments it in the same call (<c>docs/GHIDRA.md</c>). This entry
/// therefore carries a placeholder on the way out (<see cref="ToMessage"/>) and the host's
/// write path replaces it under the session gate.
/// </para>
/// <para>
/// <b>The offsets are the entry's own, not the wire's.</b> The wire header is identifier,
/// length and the fourth byte; the state and channel bytes live on the entry and never
/// travel, which is why a capture cannot show them
/// (<c>docs/analysis/stream-sequencing.md</c>).
/// </para>
/// </remarks>
public sealed class RecordHeaderEntry
{
    /// <summary>The record's identifier, class bits included: the halfword at <c>+4</c>.</summary>
    public ushort Identifier { get; set; }

    /// <summary>
    /// The entry's state byte at <c>+9</c>: the byte the reader gates on
    /// (<c>(state &amp; 5) == 5</c> is what "sequenced" means there) and that the queue
    /// sets <c>0x80</c> on once the record has been delivered.
    /// </summary>
    public byte State { get; set; }

    /// <summary>
    /// The flags byte at <c>+0xa</c>, of which selector 0 owns the low nibble: bit 0 is
    /// what <c>P2P_SendRecord</c> tests to decide the record is sequenced at all.
    /// </summary>
    public byte SequenceFlags { get; set; }

    /// <summary>
    /// The channel byte at <c>+0xd</c>: the field selector 1 writes, a byte wide and
    /// refused at <c>0x100</c> and above.
    /// </summary>
    public byte Channel { get; set; }

    /// <summary>Body the record carries, kept here so the header can be built into a record.</summary>
    public byte[] Body { get; init; } = [];

    /// <summary>
    /// Builds a record from an entry: the identifier is written through
    /// <see cref="RecordHeaderFieldService.Set"/> and the record is then copied out of the
    /// entry, which is the order the image's builders and senders use.
    /// </summary>
    /// <remarks>
    /// <b>Every record this host builds comes through here.</b> The state, flags and channel
    /// bytes are left clear because this server does not model them on the wire — the
    /// identifier is the one field both sides share — and the fourth byte passed here is a
    /// placeholder: the sender stamps the stream's own value, as the image does.
    /// </remarks>
    /// <param name="identifier">Record's identifier, class bits included.</param>
    /// <param name="body">Body the record carries.</param>
    /// <param name="fourthByte">Value of the record's fourth byte, taken where it is built.</param>
    /// <returns>The record to send.</returns>
    public static UdpMessage Build(ushort identifier, ReadOnlySpan<byte> body, byte fourthByte)
    {
        var entry = new RecordHeaderEntry { Body = body.ToArray() };

        // A ushort always fits the field's sixteen-bit width, so this cannot be refused.
        RecordHeaderFieldService.Set(entry, RecordHeaderField.Identifier, identifier);
        return entry.ToMessage(fourthByte);
    }

    /// <summary>
    /// Builds the record the entry describes, under a fourth byte the caller has assigned.
    /// </summary>
    /// <remarks>
    /// <b>This is the copy <c>P2P_SendRecord</c> makes.</b> The entry's header is what the
    /// sender carries out — it reads the header word and stores it into the packet, so the
    /// record a peer receives is the entry's, not a second one rebuilt beside it. The
    /// fourth byte is passed rather than stored because the image's field setter does not
    /// write it (see the type's remarks).
    /// </remarks>
    /// <param name="fourthByte">Value of the record's fourth byte.</param>
    /// <returns>The record to send.</returns>
    public UdpMessage ToMessage(byte fourthByte) =>
        UdpMessage.Create(Identifier, Body, fourthByte);
}
