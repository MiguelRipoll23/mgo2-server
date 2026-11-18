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
