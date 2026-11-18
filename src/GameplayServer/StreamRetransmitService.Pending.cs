namespace Mgo2Server.GameplayServer;

/// <summary>
/// The entry a stream's window holds, which is the record as it was written.
/// </summary>
/// <remarks>
/// A partial of <see cref="StreamRetransmitService"/>, split out to keep each file under the
/// repository's size limit. The entry is what the flush re-appends: the body and the fourth
/// byte together, so a copy repeats the record and not just its number
/// (<c>P2P_FlushChannelQueues</c>, <c>0x285b58</c>).
/// </remarks>
public sealed partial class StreamRetransmitService
{
    /// <summary>
    /// One record of a stream's window: the record as written, so a re-send repeats the
    /// body and the byte and not just the number.
    /// </summary>
    /// <param name="type">Identifier the record travels under.</param>
    /// <param name="ordinal">Fourth byte it was given when it was first written.</param>
    /// <param name="body">Body it carries.</param>
    /// <param name="writtenAt">Instant of its last write, in milliseconds since boot.</param>
    /// <param name="sequence">
    /// Its place in the stream, counted across the byte's wrap: the window's own order, which
    /// is the order the peer reads and therefore the order a re-send has to use.
    /// </param>
    private sealed class Pending(ushort type, byte ordinal, byte[] body, long writtenAt, long sequence)
    {
        /// <summary>Identifier the record travels under.</summary>
        public ushort Type { get; } = type;

        /// <summary>Fourth byte it was given when it was first written.</summary>
        public byte Ordinal { get; } = ordinal;

        /// <summary>Body it carries.</summary>
        public byte[] Body { get; } = body;

        /// <summary>Instant of its last write, which is what the age is measured from.</summary>
        public long WrittenAt { get; set; } = writtenAt;

        /// <summary>Times it has been written, the first one included.</summary>
        public int Attempts { get; set; }

        /// <summary>
        /// Its place in the stream, counted across the byte's wrap. Two records that carry
        /// the same fourth byte in different wraps are told apart by this and nothing else.
        /// </summary>
        public long Sequence { get; } = sequence;
    }
}
