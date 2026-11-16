using System.Runtime.CompilerServices;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Gives a record the fourth byte it travels under, at the moment it is written, and
/// gives it the same byte again if the same record is written a second time.
/// </summary>
/// <remarks>
/// <para>
/// <b>The byte belongs to the record, not to the write.</b> A stream's count is spent
/// by a record that has never been written; a record written again carries the value
/// it was first given. The working host does exactly that: <c>.pcapngs/pcapng2</c>'s
/// stream <c>0x001</c> writes 1 424 own records whose fourth bytes take 256 distinct
/// values, and 484 of its 1 423 transitions repeat the byte before them — a third of
/// the stream is the host writing a value it has already written. A counter that
/// advanced on every write could not produce a single one of them.
/// </para>
/// <para>
/// <b>And it is not a lookback over the stream.</b> The repeats are not only adjacent:
/// <c>0xa54</c> at t+378.02 of the same capture runs <c>… 0x01 0x00 0x02 …</c>, a record
/// written after the stream has moved past it. The shape is a re-send, and the only
/// thing that tells it from a new record with the same body is that it <em>is</em> the
/// same record — which is why the value is held against the record instance and not
/// against its contents. Two different records that happen to carry the same bytes take
/// two values (<c>docs/analysis/replay-findings.md</c> §5.1: one stream's repeats are
/// "the same body twice (a retransmission)" and "two different bodies sharing
/// <c>0x03</c>").
/// </para>
/// <para>
/// <b>What a caller has to do to get this right.</b> Hold the record and write it
/// again; do not rebuild it. A builder that runs twice makes two records, and the
/// second one has never been written, so it takes the next value — which is correct,
/// and is why a periodic republish must keep the records it published rather than
/// rebuilding them each time it fires.
/// </para>
/// <para>
/// <b>The counter is one per stream for the whole room, and it is the same counter the
/// run builders draw from.</b> The stream is keyed by the identifier's low twelve bits
/// (<see cref="MatchRecordSequenceService.StreamOf"/>), and the roster's own positions,
/// the scoreboard copies and the confirmation run take their values from
/// <c>RoomRosterService.TakeRunOrdinal</c>, which spends the same stream in
/// <see cref="MatchRecordSequenceService"/>. Two counters for one stream read as a
/// stream that skips values: the peer takes a record only when its byte is the one it
/// expects and drops the rest without advancing (<c>read_queued_p2p_message</c>), so a
/// duplicate or a hole stalls the join there.
/// </para>
/// </remarks>
/// <param name="sequences">The room's stream counters, which the write path spends.</param>
public sealed class MatchRecordNumberingService(MatchRecordSequenceService sequences)
{
    /// <summary>
    /// The byte each record was given, held against the record instance itself.
    /// </summary>
    /// <remarks>
    /// <b>Instance identity and not value equality.</b> <see cref="UdpMessage"/> is a
    /// record, so a dictionary would key two separately built messages with equal bytes
    /// as one and hand the second the first's value; this table compares references.
    /// It is weak, so a record that is written once and dropped takes its entry with
    /// it — the map is the re-send path and not a history of the session.
    /// </remarks>
    private readonly ConditionalWeakTable<UdpMessage, StrongBox<byte>> assigned = [];

    /// <summary>
    /// Stamps the fourth byte of every record that was built to be numbered at write.
    /// </summary>
    /// <param name="messages">Records of one write, in order.</param>
    /// <returns>The records, each under the byte its own record holds.</returns>
    public IReadOnlyList<UdpMessage> Number(IReadOnlyList<UdpMessage> messages)
    {
        if (!messages.Any(message => message.AutoNumbered))
        {
            return messages;
        }

        var numbered = new List<UdpMessage>(messages.Count);
        foreach (var message in messages)
        {
            numbered.Add(message.AutoNumbered ? NumberOne(message) : message);
        }

        return numbered;
    }

    /// <summary>
    /// The byte one record travels under: the one it already has if it has been
    /// written, and the next value of its stream if it has not.
    /// </summary>
    /// <param name="message">Record to number.</param>
    /// <returns>The record, carrying the byte it takes.</returns>
    private UdpMessage NumberOne(UdpMessage message)
    {
        if (assigned.TryGetValue(message, out var held))
        {
            return message with { Flags = held.Value, AutoNumbered = false };
        }

        var value = sequences.Take(message.Type);
        assigned.AddOrUpdate(message, new StrongBox<byte>(value));
        return message with { Flags = value, AutoNumbered = false };
    }
}
