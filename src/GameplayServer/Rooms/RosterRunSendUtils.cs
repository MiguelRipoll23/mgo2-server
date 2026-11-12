using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Writes a run of roster-shaped records to one peer: one datagram where the run
/// fits, and the reference's own packing where it does not.
/// </summary>
/// <remarks>
/// <para>
/// <b>The run is closed in the datagram that seats the room, and that is the whole
/// of what this file has to get right.</b> <c>actions.pcapng</c> t+0.60 writes it as
/// three datagrams — <c>[0x5001, 57, 63×6, 7, 10]</c>, then <c>[63×7]</c>, then
/// <c>[63, 63, 72, 54]</c> — so the close (<c>7</c>) and the room's count
/// (<c>10</c>) ride the <em>first</em> frame and the entries that did not fit
/// follow it. This host packed greedily instead: entries until the cap, close and
/// count last. With seventeen seats that put the close on the third datagram and
/// the join stopped completing; the number of entries was never the problem.
/// </para>
/// <para>
/// A datagram past <see cref="UdpCommandConstants.MaximumDatagramSize"/> is dropped
/// whole, so the run cannot simply be written as one frame either — see the
/// measurement on <see cref="UdpCommandConstants.MaximumContentSize"/>.
/// </para>
/// </remarks>
public static class RosterRunSendUtils
{
    /// <summary>
    /// Shortest body a roster entry is written with. The close record and the room's
    /// count are the run's tail and both are shorter than any entry; the answered
    /// peer's own settings payload is shorter still but closes the run behind them.
    /// </summary>
    private const int MinimumEntryBodyLength = 50;

    /// <summary>
    /// Writes a run of records, closed in its first datagram, or one message at a
    /// time where the context cannot batch.
    /// </summary>
    /// <param name="context">Context of the message being answered.</param>
    /// <param name="run">Records to write, in order.</param>
    public static async Task SendAsync(
        PeerContext context,
        IReadOnlyList<RosterRecord> run)
    {
        if (context.SendRecords is not { } sendRecords)
        {
            foreach (var record in run)
            {
                await context.Send(record.Type, record.Body, record.Ordinal);
            }

            return;
        }

        foreach (var group in Pack(run))
        {
            await sendRecords(group);
        }
    }

    /// <summary>
    /// Splits a run into the datagrams it travels as, the first of which carries the
    /// run's close and count.
    /// </summary>
    /// <param name="run">Records to split, in order.</param>
    /// <returns>The datagrams, each one a run of messages.</returns>
    private static IReadOnlyList<IReadOnlyList<UdpMessage>> Pack(
        IReadOnlyList<RosterRecord> run)
    {
        var messages = run
            .Select(record => FrameBuilderUtility.MessageOf(record.Type, record.Body, record.Ordinal))
            .ToList();

        if (SizeOf(messages) <= UdpCommandConstants.MaximumContentSize)
        {
            return [messages];
        }

        // Where the tail begins: the close record is the first body shorter than any
        // profile entry, and the room's count follows it one record on.
        var tailStart = messages.FindIndex(
            message => message.Body.Length is > 0 and < MinimumEntryBodyLength);
        if (tailStart < 1)
        {
            return FrameBuilderUtility.SplitByFrameSize(messages);
        }

        var entries = messages.Take(tailStart).ToList();
        var closing = messages.Skip(tailStart).Take(2).ToList();
        var remainder = messages.Skip(tailStart + 2).ToList();
        var closingSize = SizeOf(closing);

        // The settings payload that closes the run stays behind with whatever entries
        // did not fit, which is where the reference writes it: its last frame is two
        // entries and the payload, its first ends on the count.
        var first = new List<UdpMessage>();
        var used = 0;
        var index = 0;
        for (; index < entries.Count; index++)
        {
            var size = SizeOf(entries[index]);
            if (first.Count > 0 && used + size + closingSize > UdpCommandConstants.MaximumContentSize)
            {
                break;
            }

            first.Add(entries[index]);
            used += size;
        }

        first.AddRange(closing);

        var rest = entries.Skip(index).ToList();
        rest.AddRange(remainder);

        var groups = new List<IReadOnlyList<UdpMessage>> { first };
        groups.AddRange(FrameBuilderUtility.SplitByFrameSize(rest));
        return groups;
    }

    /// <summary>Wire size of one message, header included.</summary>
    /// <param name="message">Message to measure.</param>
    private static int SizeOf(UdpMessage message) =>
        UdpCommandConstants.MessageHeaderSize + message.Body.Length;

    /// <summary>Wire size of a run of messages.</summary>
    /// <param name="messages">Messages to measure.</param>
    private static int SizeOf(IReadOnlyList<UdpMessage> messages) =>
        messages.Sum(SizeOf);
}
