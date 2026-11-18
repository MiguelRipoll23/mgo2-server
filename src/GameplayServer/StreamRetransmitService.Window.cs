using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Udp;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The outstanding records of a stream, measured the way the image measures its queue, and the
/// drain that keeps the window under the queue's capacity.
/// </summary>
public sealed partial class StreamRetransmitService
{
    /// <summary>
    /// One stream's outstanding records, held in the order the peer reads them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The list is ordered by the stream, not by the socket.</b> A record's fourth byte is
    /// its place in an ordered channel, but two writers can number their records and then
    /// reach the send path in the other order — the round-start run of 2026-11-18 does
    /// exactly that, writing <c>0x67</c>/<c>0x68</c> one frame before the <c>0x5e…0x66</c>
    /// they follow. A list in call order would put those two ahead of the hole they belong
    /// behind, and the cumulative free below would take them for records the peer had
    /// accepted. Placing every entry by its sequence keeps the head the byte the peer is
    /// waiting for, which is the byte a re-send has to start at.
    /// </para>
    /// <para>
    /// <b>The sequence is the byte, unwrapped.</b> The stream's counter is one byte wide, so
    /// the place of a new record is read from its distance to the newest one already held:
    /// a small forward distance extends the sequence, a large one is a record from before
    /// it. That is enough for a window the size of this one — a drain keeps it near a
    /// dozen — and it survives the wrap, which a plain comparison of bytes cannot.
    /// </para>
    /// </remarks>
    private sealed class StreamWindow
    {
        /// <summary>Records held, oldest first: the order the peer reads and a re-send keeps.</summary>
        public List<Pending> Pending { get; } = [];

        private bool started;
        private byte newestOrdinal;
        private long newestSequence;
        private long answeredThrough = long.MinValue;

        /// <summary>
        /// Holds one written record, placed by its stream position, unless the peer has
        /// already taken that position.
        /// </summary>
        /// <remarks>
        /// A copy of a held record keeps the byte it already carries rather than entering
        /// the window a second time: the peer answers a byte, and two entries under one byte
        /// would make that answer ambiguous. A copy whose position the peer has already
        /// answered — a scoreboard delta's later copy, written after its answer arrived — is
        /// not held at all. Holding it would put a byte the peer has already passed at the
        /// head of the window, where every re-send would start with it and the attempts it
        /// spends would end a stream that has nothing wrong with it.
        /// </remarks>
        /// <param name="message">Record just written.</param>
        /// <param name="now">Instant of the write, in milliseconds since boot.</param>
        public void Hold(UdpMessage message, long now)
        {
            var held = Pending.Find(entry => entry.Type == message.Type && entry.Ordinal == message.Flags);
            if (held is not null)
            {
                held.WrittenAt = now;
                return;
            }

            var sequence = SequenceOf(message.Flags);
            if (sequence <= answeredThrough)
            {
                return;
            }

            var at = Pending.FindIndex(entry => entry.Sequence > sequence);
            Pending.Insert(
                at < 0 ? Pending.Count : at,
                new Pending(message.Type, message.Flags, message.Body, now, sequence));
        }

        /// <summary>
        /// Frees everything the peer's answer proves it took: the answered record and every
        /// record held before it in the stream.
        /// </summary>
        /// <remarks>
        /// <b>The free is by sequence and not by list position.</b> An answer is cumulative —
        /// the peer takes a stream in order — so everything before the answered byte is
        /// taken and can go. What sits after it was refused, whatever its place in the call
        /// order, and stays held so the next re-send writes it again.
        /// </remarks>
        /// <param name="ordinal">Fourth byte the answer echoes.</param>
        public void FreeThrough(byte ordinal)
        {
            var index = Pending.FindIndex(entry => entry.Ordinal == ordinal);
            if (index < 0)
            {
                return;
            }

            answeredThrough = Math.Max(answeredThrough, Pending[index].Sequence);
            Pending.RemoveAll(entry => entry.Sequence <= answeredThrough);
        }

        /// <summary>
        /// The unwrapped place of a record carrying this byte, advancing the stream when it
        /// is a new record and not when it is one from before the newest.
        /// </summary>
        /// <param name="ordinal">Fourth byte the record carries.</param>
        /// <returns>Its sequence, counted across the byte's wrap.</returns>
        private long SequenceOf(byte ordinal)
        {
            if (!started)
            {
                started = true;
                newestOrdinal = ordinal;
                newestSequence = 0;
                return 0;
            }

            var delta = (ordinal - newestOrdinal) & 0xff;
            if (delta == 0)
            {
                return newestSequence;
            }

            if (delta < 0x80)
            {
                newestSequence += delta;
                newestOrdinal = ordinal;
                return newestSequence;
            }

            return newestSequence - (0x100 - delta);
        }
    }

    /// <summary>
    /// Capacity of one stream's window, in bytes: the record queue's own capacity word.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The header is established; the number is stated with what it rests on.</b> A queue's
    /// header is a pair of halfwords — capacity at <c>+0</c>, used at <c>+2</c> — which is what
    /// <c>P2P_QueueRecordHandles</c> (<c>0x269ca8</c>) tests when it refuses with
    /// <c>0xffffffff</c> for <c>capacity &lt; used + size</c>, and what
    /// <c>P2P_CompactRecordQueue</c> (<c>0x269ea8</c>) shrinks as it drops delivered entries.
    /// <b>No function writes the capacity:</b> <c>P2P_ResetRecordHandleTable</c>
    /// (<c>0x269808</c>) sets only the used halfword, to <c>0xc</c>, and the slots' queue
    /// pointers are already in place when it runs, so the capacity is laid out before any of
    /// this code.
    /// </para>
    /// <para>
    /// <b>The number itself is not in the code.</b> A word scan of the data segment finds no
    /// queue-sized value an instruction refers to, so the capacity is laid out with the buffer
    /// the image gives the queue. <c>0x578</c> (1 400) is that size: it is what
    /// <c>receive_p2p_datagrams</c> (<c>0x2622a0</c>) allocates and hands to <c>recvfrom</c>, so
    /// it is the one buffer length the record path uses, and the entry accounting is the same
    /// in each direction. The <i>rule</i> below — half, then a quarter, oldest first — is the
    /// image's without qualification; the figure is its receive size applied to the same queue
    /// header, and is stated as that rather than as a located write.
    /// </para>
    /// </remarks>
    private const int WindowCapacityBytes = 0x578;

    /// <summary>Where a stream's window is drained: half its capacity, 700 bytes.</summary>
    private const int DrainThresholdBytes = WindowCapacityBytes >> 1;

    /// <summary>
    /// What a drain leaves: everything under a quarter of the capacity, 350 bytes.
    /// </summary>
    /// <remarks>
    /// The image takes <c>used − (capacity &gt;&gt; 2)</c> as the amount to free and walks the
    /// oldest entries subtracting each one's size from it until nothing of that budget is left.
    /// </remarks>
    private const int DrainTargetBytes = WindowCapacityBytes >> 2;

    /// <summary>
    /// The bytes a stream's window is using, by the queue's own accounting: every entry's
    /// twelve-byte header and body, advanced by <c>(3 + size) &amp; 0xfffc</c>.
    /// </summary>
    /// <param name="pending">Records held, oldest first.</param>
    /// <returns>Bytes the image's queue would be holding them in.</returns>
    private static int UsedBytes(List<Pending> pending) =>
        pending.Sum(entry => SizeBytes(entry.Body.Length));

    /// <summary>The bytes one held record takes in the image's queue.</summary>
    /// <param name="bodyLength">Length of the record's body.</param>
    /// <returns>Its entry size, aligned.</returns>
    private static int SizeBytes(int bodyLength) =>
        RecordEntryFramingUtils.AlignedSize(RecordEntryFramingUtils.EntrySize(bodyLength));

    /// <summary>
    /// Drops the oldest records of a stream that has reached half the queue's capacity, until
    /// what is held fits under a quarter of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The image's drain, in the image's unit.</b> <c>P2P_FlushChannelQueues</c>
    /// (<c>0x285b58</c>) drains a queue that has reached <c>capacity &gt;&gt; 1</c> back under
    /// <c>capacity &gt;&gt; 2</c>, oldest first, subtracting each entry's size from the amount to
    /// free until none of it is left. Each freed sequenced entry has its acknowledged base at
    /// <c>+0xc</c> set to its write count at <c>+0xb</c>, so a record it forgets is one it will
    /// neither count nor write again.
    /// </para>
    /// <para>
    /// Removing the entry here is that same decision: the window is the outstanding set, and
    /// what leaves it stops being re-sent.
    /// </para>
    /// </remarks>
    /// <param name="stream">Stream the records belong to, for the log line.</param>
    /// <param name="pending">Records held for it, oldest first.</param>
    private void DrainIfFull(ushort stream, List<Pending> pending)
    {
        var used = UsedBytes(pending);
        if (used < DrainThresholdBytes)
        {
            return;
        }

        var freed = 0;
        var remaining = used;
        while (freed < pending.Count && remaining >= DrainTargetBytes)
        {
            remaining -= SizeBytes(pending[freed].Body.Length);
            freed++;
        }

        logger.LogDebug(
            "Stream 0x{Stream:x3} drains {Count} record(s), leaving {Bytes} of {Limit} bytes: the "
            + "window reached half the queue's capacity",
            stream,
            freed,
            remaining,
            WindowCapacityBytes);
        pending.RemoveRange(0, freed);
    }
}
