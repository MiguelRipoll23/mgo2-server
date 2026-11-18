using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Udp;

/// <summary>
/// The framing of one entry in a channel's record queue: a twelve-byte header and the
/// body behind it, every entry advanced by a four-byte-aligned size.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both of the image's queues hold this same entry.</b>
/// <c>P2P_QueueInboundRecord</c> (<c>0x269b00</c>) writes one on the receive path and
/// <c>append_udp_datagram_to_send_buffer</c> (<c>0x2696d8</c>) writes one on the send
/// path; the two bodies differ only in where the entry comes from. The header carries,
/// in order: the record's handle as a word, a zero word, the entry's own size as a
/// sixteen-bit value, then three bytes of identity — the container id, the record's
/// state code and the fourth wire byte — and a retransmit flag.
/// </para>
/// <para>
/// <b>The advance is <c>(3 + size) &amp; 0xfffc</c>, not the entry's size.</b> Each entry
/// carries three bytes of framing above its own header, and the write offset rounds down
/// to a four-byte boundary after adding them, so entries land on word boundaries and the
/// slack is never written. It is also why a record can be refused when its size is under
/// the capacity but its aligned advance is not: the image tests the entry alone
/// (<c>size + offset &lt;= capacity</c>) and then advances by the aligned figure.
/// </para>
/// <para>
/// <b>The fourth wire byte is the ordinal, and its offset depends on the length flag.</b>
/// A record whose header byte 1 has bit <c>0x20</c> set carries a sixteen-bit length in
/// bytes 2 and 3 and its ordinal in byte 4; without the flag the length is byte 2 alone
/// and the ordinal is byte 3. Bit <c>0x10</c> is what makes the record sequenced at all:
/// with it clear the queue zeroes the state code, so an unsequenced record carries no place
/// in its stream and is read whatever the stream is waiting for; its fourth byte is kept as
/// the attribute class this host's codec separates from the body.
/// </para>
/// </remarks>
public static class RecordEntryFramingUtils
{
    /// <summary>Length of the header each queued entry carries.</summary>
    public const int EntryHeaderSize = 12;

    /// <summary>Framing bytes added above an entry when the write offset advances.</summary>
    public const int FramingOverhead = 3;

    /// <summary>Wire flag: the record declares a sixteen-bit length, so its ordinal is one byte later.</summary>
    public const byte ExtendedLengthFlag = (byte)RecordClassBits.ExtendedLength;

    /// <summary>Wire flag: the record is sequenced, so its fourth byte is a place in its stream.</summary>
    public const byte SequencedFlag = (byte)RecordClassBits.Sequenced;

    /// <summary>Entry offset of the record's handle, a word.</summary>
    public const int HandleOffset = 0;

    /// <summary>Entry offset of the entry's own size, a sixteen-bit value.</summary>
    public const int EntrySizeOffset = 6;

    /// <summary>Entry offset of the record's container id, one byte.</summary>
    public const int ContainerIdOffset = 8;

    /// <summary>Entry offset of the record's state code, one byte; zero when unsequenced.</summary>
    public const int StateOffset = 9;

    /// <summary>
    /// Entry offset of the fourth wire byte, one byte: the ordinal when the record is
    /// sequenced, and the attribute class when it is not.
    /// </summary>
    public const int OrdinalOffset = 10;

    /// <summary>Entry offset of the retransmit flag, one byte; a send clears it.</summary>
    public const int RetransmitOffset = 11;

    /// <summary>Entry offset of the first body byte.</summary>
    public const int BodyOffset = EntryHeaderSize;

    /// <summary>
    /// Mask that keeps the identifier's container byte, which the entry stores at
    /// <see cref="ContainerIdOffset"/>: the low byte of a record identifier, the part that
    /// stays the same when the class bits above it change.
    /// </summary>
    public const byte ContainerByteMask = 0xff;

    /// <summary>
    /// The size an entry advances the write offset by: its own size plus the framing
    /// overhead, rounded down to a four-byte boundary.
    /// </summary>
    /// <param name="entrySize">The entry's size as its header states it.</param>
    /// <returns>Bytes to advance the write offset by.</returns>
    public static int AlignedSize(int entrySize) => (FramingOverhead + entrySize) & 0xfffc;

    /// <summary>The entry size for a body of the given length.</summary>
    /// <param name="bodyLength">Length of the record's body.</param>
    /// <returns>The size the entry's header states.</returns>
    public static int EntrySize(int bodyLength) => EntryHeaderSize + bodyLength;

    /// <summary>
    /// Offset of the fourth wire byte in a record header, which the length flag decides.
    /// </summary>
    /// <param name="headerFlags">The record's header byte 1.</param>
    /// <returns>Three without the extended length flag, four with it.</returns>
    public static int OrdinalWireOffset(byte headerFlags) =>
        (headerFlags & ExtendedLengthFlag) == 0 ? 3 : 4;

    /// <summary>Whether a record's header says it is sequenced.</summary>
    /// <param name="headerFlags">The record's header byte 1, which is the identifier's high byte.</param>
    public static bool IsSequenced(byte headerFlags) => (headerFlags & SequencedFlag) != 0;

    /// <summary>
    /// Whether an identifier declares the sequenced class, which is what the reader tests
    /// before it compares a fourth byte at all.
    /// </summary>
    /// <remarks>
    /// <b>The flags are not a byte beside the identifier — they are its high byte.</b>
    /// <c>P2P_QueueInboundRecord</c> reads <c>*(byte *)(record + 1)</c>, and for a
    /// <c>0x1001</c> record that byte is <c>0x10</c>: bit <c>0x10</c> set, sequenced. The
    /// answer class sets <c>0x40</c> on the same byte, so <c>0x5001</c> reads <c>0x50</c> and is
    /// sequenced as well. A record whose high byte carries neither is unsequenced —
    /// <c>0x0001</c> reads <c>0x00</c> — and one whose high byte has <c>0x20</c> declares a
    /// sixteen-bit length, which is the class that gives the ordinal its fourth position.
    /// </remarks>
    /// <param name="identifier">Record identifier as it travels.</param>
    public static bool DeclaresSequenced(ushort identifier) =>
        IsSequenced((byte)(identifier >> 8));

    /// <summary>
    /// Whether an identifier declares a sixteen-bit length, which moves its ordinal one byte
    /// later and is the class bit <c>0x20</c> of the identifier's high byte.
    /// </summary>
    /// <param name="identifier">Record identifier as it travels.</param>
    public static bool DeclaresExtendedLength(ushort identifier) =>
        ((identifier >> 8) & ExtendedLengthFlag) != 0;
}
