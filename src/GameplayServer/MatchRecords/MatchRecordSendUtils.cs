using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Writes a run of match records to one peer: several records as a single
/// datagram on one outbound counter, or one message at a time where the context
/// cannot batch.
/// </summary>
/// <remarks>
/// The two writers of the round's own records — the opener that writes the run
/// and the closer that writes its result — send them the same way, and both need
/// the same fallback, so the write lives here rather than in either of them.
/// </remarks>
public static class MatchRecordSendUtils
{
    /// <summary>
    /// Writes records to a peer.
    /// </summary>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="records">Records to write, in order.</param>
    /// <param name="compressed">
    /// Whether the records go out LZSS-compressed with the header's compression
    /// marker set. The recorded host writes a whole run compressed and the
    /// one-byte records plain, so the flag belongs to the caller.
    /// </param>
    public static async Task SendAsync(
        PeerContext context,
        IReadOnlyList<MatchRecordFramesUtils.RecordMessage> records,
        bool compressed = false)
    {
        var messages = records.Select(MatchRecordFramesUtils.ToUdpMessage).ToList();
        var batch = compressed
            ? context.SendRecordsCompressed ?? context.SendRecords
            : context.SendRecords;
        if (batch is { } sendRecords)
        {
            await sendRecords(messages);
            return;
        }

        foreach (var message in messages)
        {
            await context.Send(message.Type, message.Body, message.Flags);
        }
    }
}
