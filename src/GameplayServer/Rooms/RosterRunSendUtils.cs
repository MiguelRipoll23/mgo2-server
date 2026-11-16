using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Writes a run of roster-shaped records to one peer, in the order the run was numbered.
/// </summary>
/// <remarks>
/// <para>
/// <b>A run goes out in numbering order, and that is the whole of what this file has to
/// get right.</b> The peer takes a stream's records only when each arrives carrying the
/// byte it is expecting, and it has no buffer and sends no negative answer
/// (<c>read_queued_p2p_message</c>): a record that arrives ahead of the one it waits for
/// is discarded and its value is never taken. The stream then cannot move past that
/// value until the byte-wide counter comes round to it — 256 values, which is minutes at
/// a round's rate.
/// </para>
/// <para>
/// <b>Seating the close in the first datagram costs exactly those values.</b>
/// <c>actions.pcapng</c> writes the run's close and the room's count in its first frame
/// with the entries that did not fit behind them, and this host copied that shape.
/// Measured on <c>myserver.pcapng</c> at t+4.06, the join's run then went out as
/// <c>… 0x06 0x11 0x12 0x07 …</c>: the close and the count arrived two values early and
/// the client refused both, and nothing wrote them again. From that moment the client's
/// expectation sat two behind this host's counter, so every scoreboard delta and every
/// ticket written afterwards was refused — which is why writing one body across the
/// whole ordinal space is what made it land. The recorded host survives its own
/// reordering because it re-sends what was refused; this host writes a value once, so
/// the reorder is not worth its two values.
/// </para>
/// <para>
/// <b>Which datagram a record lands in is not decided here.</b> The run is handed to the
/// write path whole and that path appends records to the datagram under construction while
/// the next one still fits, starting another when it does not — the image's own rule
/// (<c>append_udp_datagram_to_send_buffer</c>, <c>0x2696d8</c>, and
/// <see cref="Mgo2Server.Shared.Udp.OutboundDatagramBufferService"/>). Splitting here as
/// well would be a second rule for the same decision, and the two cannot agree: the write
/// path's bound counts the three framing bytes the image aligns each entry by, and a
/// split computed without them fills a datagram further than the image would.
/// </para>
/// </remarks>
public static class RosterRunSendUtils
{
    /// <summary>
    /// Writes a run of records, in the order the run numbers them, or one message at a
    /// time where the context cannot batch.
    /// </summary>
    /// <param name="context">Context of the message being answered.</param>
    /// <param name="run">Records to write, in numbering order.</param>
    public static async Task SendAsync(PeerContext context, IReadOnlyList<RosterRecord> run)
    {
        if (run.Count == 0)
        {
            return;
        }

        if (context.SendRecords is not { } sendRecords)
        {
            // A record marked for numbering is written with the byte it carries here,
            // which is zero: this is the path for a context with no batching delegate, and
            // every context this host builds carries one, where the write path numbers it
            // (<see cref="MatchRecords.MatchRecordNumberingService"/>).
            foreach (var record in run)
            {
                await context.Send(record.Type, record.Body, record.Ordinal);
            }

            return;
        }

        // A record marked for numbering keeps its marker all the way to the write path,
        // which stamps it from the stream its type is counted on; the roster run's own
        // records carry their positions here instead.
        var messages = new List<UdpMessage>(run.Count);
        foreach (var record in run)
        {
            messages.Add(record.AutoNumbered
                ? UdpMessage.CreateAutoNumbered(record.Type, record.Body)
                : FrameBuilderUtility.MessageOf(record.Type, record.Body, record.Ordinal));
        }

        await sendRecords(messages);
    }
}
