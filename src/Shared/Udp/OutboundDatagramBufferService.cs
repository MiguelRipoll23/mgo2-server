using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Udp;

/// <summary>
/// The datagram a channel is filling: entries appended one at a time until the next one no
/// longer fits, advanced by the same four-byte-aligned rule as the inbound queue.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the send path's half of the image's queue pair.</b>
/// <c>append_udp_datagram_to_send_buffer</c> (<c>0x2696d8</c>) copies one record header and
/// its body into the datagram under construction, advances the write offset by
/// <c>(3 + size) &amp; 0xfffc</c>, and returns the entry's size — or <c>0xffffffff</c> when the
/// record does not fit, which is how the send path learns that this datagram is full and the
/// next record starts a new one. It clears the entry's retransmit byte on the way, because a
/// record that has just gone out is no longer waiting to.
/// </para>
/// <para>
/// <b>Both paths therefore pack by the same arithmetic.</b> A frame carries records while
/// <c>aligned(4 + body)</c> still fits, and the remainder becomes the next frame, which is the
/// split <see cref="Split"/> reproduces. Nothing is padded on the wire: the alignment is
/// bookkeeping between entries, and the slack it leaves is never written, exactly as the
/// image's buffer leaves it.
/// </para>
/// </remarks>
public static class OutboundDatagramBufferService
{
    /// <summary>
    /// Capacity of the buffer being filled, which is the frame's <b>content region</b> and not
    /// the datagram: the image appends records to the content and the chain, keys and tail
    /// digest are added around it afterwards, so the bound it tests is the content's own.
    /// </summary>
    public const int CapacityBytes = UdpCommandConstants.MaximumContentSize;

    /// <summary>
    /// Whether one more record fits, which is the image's own test before it copies.
    /// </summary>
    /// <param name="used">Bytes already in the datagram.</param>
    /// <param name="recordSize">Size of the record about to be appended, header included.</param>
    /// <returns><c>true</c> when the record fits.</returns>
    public static bool Fits(int used, int recordSize) => recordSize + used <= CapacityBytes;

    /// <summary>
    /// Advances the datagram's write offset past a record, by the image's aligned rule.
    /// </summary>
    /// <param name="used">Bytes already in the datagram.</param>
    /// <param name="recordSize">Size of the record appended, header included.</param>
    /// <returns>The datagram's new size.</returns>
    public static int Append(int used, int recordSize) =>
        used + RecordEntryFramingUtils.AlignedSize(recordSize);

    /// <summary>
    /// Groups messages into datagrams by that rule: as many as fit, in order, each record's
    /// size being its four-byte header and its body.
    /// </summary>
    /// <param name="messages">Messages to write, in order.</param>
    /// <returns>The groups, in order, each one datagram.</returns>
    public static List<IReadOnlyList<UdpMessage>> Split(IReadOnlyList<UdpMessage> messages)
    {
        var groups = new List<IReadOnlyList<UdpMessage>>();
        var group = new List<UdpMessage>();
        var used = 0;

        foreach (var message in messages)
        {
            // A record's wire size is its identifier, its length byte, its fourth byte and
            // the body; that whole record is what the image's buffer appends.
            var recordSize = UdpCommandConstants.MessageHeaderSize + message.Body.Length;
            if (!Fits(used, recordSize) && group.Count > 0)
            {
                groups.Add(group);
                group = [message];
                used = Append(0, recordSize);
                continue;
            }

            group.Add(message);
            used = Append(used, recordSize);
        }

        if (group.Count > 0)
        {
            groups.Add(group);
        }

        return groups;
    }
}
