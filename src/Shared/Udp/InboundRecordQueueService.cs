using System.Collections.Concurrent;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Udp;

/// <summary>
/// The record queue a peer's inbound records are written into and read back out of, in the
/// order they arrived, one channel per peer.
/// </summary>
/// <remarks>
/// <para>
/// <b>The image receives into a queue rather than straight into a handler.</b>
/// <c>receive_p2p_datagrams</c> hands each record to
/// <c>P2P_QueueInboundRecord</c> (<c>0x269b00</c>), which copies it into the channel's entry
/// buffer and returns the entry's size, or <c>0xffffffff</c> when the record does not fit.
/// The tick then walks that buffer with <c>P2P_InboundQueueBegin</c> and
/// <c>P2P_InboundQueueCurrent</c>/<c>Next</c> (<c>0x269390</c>/<c>0x2693a8</c>/<c>0x2693e0</c>)
/// and reads each entry where it is, which is why a frame's records are handled one at a
/// time and a handler that ends the frame ends the walk with it.
/// </para>
/// <para>
/// <b>A record that does not fit is refused, not allocated for.</b> There is no growth and no
/// eviction: the queue holds what its capacity holds, and the caller is told when it cannot
/// take one more. That is the whole back-pressure the image has, and it is what this
/// service keeps — a full queue reports <c>false</c> and the caller decides, rather than
/// silently dropping a record the peer will never hear about.
/// </para>
/// <para>
/// Entries carry the framing in <see cref="RecordEntryFramingUtils"/>: twelve-byte header,
/// body behind it, write offset advanced by the four-byte-aligned size. The buffer is reset
/// to its start when the last entry has been read, which is the image's queue begin and end
/// folding into the same empty state.
/// </para>
/// </remarks>
public sealed class InboundRecordQueueService
{
    /// <summary>
    /// Capacity of one channel's queue: <b>the size of the buffer the game receives a
    /// datagram into</b>, <c>0x578</c> bytes, which is the most a queue of records can ever
    /// be handed at once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a fixed size in the executable, not a menu setting.</b>
    /// <c>receive_p2p_datagrams</c> (<c>0x2622a0</c>) allocates <c>0x578</c> bytes from the
    /// network arena and then reads with <c>recvfrom(socket, buffer, 0x578, …)</c>, so 1 400
    /// bytes is the unit of everything the receive path can be holding. The image's image has
    /// no "MTU", "kbps" or "512" string anywhere in it, and no bandwidth figure is read on
    /// this path: the buffer is the constant. The kbps choices in the game's menus are labels
    /// in the game's resources, and they do not reach this code.
    /// </para>
    /// <para>
    /// The rest of the network bookkeeping around it, for scale: the arena the allocations
    /// come from is <c>0x4000</c> (16 KiB, carved in sixteen-byte-aligned pieces by
    /// <c>0x26d350</c>); a channel's record table holds <b>256</b> handles (<c>0x600</c> bytes
    /// of six-byte entries, cleared by <c>0x269808</c>); the parsed-datagram index costs two
    /// bytes per record; the channel record itself is <c>0x6d8</c> bytes, walked for
    /// twenty-four channels; and a walk of every record handle takes a <c>0x400</c>-byte
    /// scratch buffer.
    /// </para>
    /// <para>
    /// <b>The image drains by halves and this queue drains whole.</b> Its flush routine
    /// (<c>0x285b58</c>) reads the capacity from the queue's first word and the used count from
    /// its second, starts freeing once the used count reaches <c>capacity &gt;&gt; 1</c>, and frees
    /// the oldest entries until it is back under <c>capacity &gt;&gt; 2</c> — a quarter's worth,
    /// which is the newest records, always stays. Those freed entries are marked and re-sent
    /// rather than dropped, so the rule is a bound on the buffer and not a loss policy. This
    /// host reads a frame's records into the queue and drains it whole within the same turn,
    /// so the half threshold is never reached and the rule is recorded rather than reproduced.
    /// </para>
    /// </remarks>
    private const int CapacityBytes = 0x578;

    private readonly ConcurrentDictionary<string, ChannelQueue> channels = new(StringComparer.Ordinal);

    /// <summary>
    /// Writes one record into its peer's queue.
    /// </summary>
    /// <param name="channel">Channel the record belongs to, which is the peer's endpoint.</param>
    /// <param name="record">Record to queue.</param>
    /// <returns><c>true</c> when it was taken, <c>false</c> when the queue could not hold it.</returns>
    public bool TryQueue(string channel, QueuedRecord record) =>
        channels.GetOrAdd(channel, _ => new ChannelQueue()).TryQueue(record);

    /// <summary>
    /// Reads the oldest queued record of a channel.
    /// </summary>
    /// <param name="channel">Channel to read from.</param>
    /// <param name="record">The record read, when there was one.</param>
    /// <returns><c>true</c> when a record was read, <c>false</c> when the queue is empty.</returns>
    public bool TryDequeue(string channel, out QueuedRecord record)
    {
        if (channels.TryGetValue(channel, out var queue))
        {
            return queue.TryDequeue(out record);
        }

        record = default!;
        return false;
    }

    /// <summary>Drops a peer's queue, which is what a departure leaves behind.</summary>
    /// <param name="channel">Channel to forget.</param>
    public void Forget(string channel) => channels.TryRemove(channel, out _);

    /// <summary>One peer's buffer of queued entries.</summary>
    private sealed class ChannelQueue
    {
        private readonly byte[] entries = new byte[CapacityBytes];
        private readonly Lock gate = new();
        private int readOffset;
        private int writeOffset;
        private int queued;

        /// <summary>Writes one entry at the write offset, exactly as the image's queue does.</summary>
        /// <param name="record">Record to write.</param>
        public bool TryQueue(QueuedRecord record)
        {
            var entrySize = RecordEntryFramingUtils.EntrySize(record.Body.Length);
            lock (gate)
            {
                // The image tests the entry's own size against the capacity and then advances
                // by the aligned size, so the slack above an entry is never checked for.
                if (entrySize + writeOffset > entries.Length)
                {
                    return false;
                }

                var at = writeOffset;
                WriteUInt32(at + RecordEntryFramingUtils.HandleOffset, record.Type);
                WriteUInt16(at + RecordEntryFramingUtils.EntrySizeOffset, (ushort)entrySize);
                entries[at + RecordEntryFramingUtils.ContainerIdOffset] =
                    (byte)(record.Type & RecordEntryFramingUtils.ContainerByteMask);

                // The code the reader tests, `(state & 5) == 5`: both of its bits mean the
                // entry's fourth byte is compared before the record is handed out.
                entries[at + RecordEntryFramingUtils.StateOffset] = (byte)(
                    RecordEntryFramingUtils.DeclaresSequenced(record.Type)
                        ? RecordEntryState.QueuedSequenced
                        : RecordEntryState.None);
                entries[at + RecordEntryFramingUtils.OrdinalOffset] = record.Flags;
                entries[at + RecordEntryFramingUtils.RetransmitOffset] = 0;
                record.Body.CopyTo(entries, at + RecordEntryFramingUtils.BodyOffset);

                writeOffset += RecordEntryFramingUtils.AlignedSize(entrySize);
                queued++;
                return true;
            }
        }

        /// <summary>Reads the oldest entry and moves the read offset past it.</summary>
        /// <param name="record">The record read.</param>
        public bool TryDequeue(out QueuedRecord record)
        {
            lock (gate)
            {
                if (queued == 0)
                {
                    record = default!;
                    return false;
                }

                var at = readOffset;
                var type = ReadUInt16(at + RecordEntryFramingUtils.HandleOffset);
                var entrySize = ReadUInt16(at + RecordEntryFramingUtils.EntrySizeOffset);
                var bodyLength = entrySize - RecordEntryFramingUtils.EntryHeaderSize;
                var body = entries[
                    (at + RecordEntryFramingUtils.BodyOffset)..(at + RecordEntryFramingUtils.BodyOffset + bodyLength)];
                record = new QueuedRecord(
                    type,
                    entries[at + RecordEntryFramingUtils.OrdinalOffset],
                    body);
                readOffset += RecordEntryFramingUtils.AlignedSize(entrySize);
                queued--;

                // The image begins its queue at the start of the buffer and ends it there:
                // an emptied channel is a channel whose two offsets are back at zero.
                if (queued == 0)
                {
                    readOffset = 0;
                    writeOffset = 0;
                }

                return true;
            }
        }

        private void WriteUInt16(int offset, ushort value)
        {
            entries[offset] = (byte)(value & 0xff);
            entries[offset + 1] = (byte)(value >> 8);
        }

        private void WriteUInt32(int offset, uint value)
        {
            entries[offset] = (byte)(value & 0xff);
            entries[offset + 1] = (byte)((value >> 8) & 0xff);
            entries[offset + 2] = (byte)((value >> 16) & 0xff);
            entries[offset + 3] = (byte)((value >> 24) & 0xff);
        }

        private ushort ReadUInt16(int offset) =>
            (ushort)(entries[offset] | (entries[offset + 1] << 8));
    }
}
