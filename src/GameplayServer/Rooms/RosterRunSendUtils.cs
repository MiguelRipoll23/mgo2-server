using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Writes a run of roster-shaped records to one peer: several records as a
/// single datagram, or one message at a time where the context cannot batch.
/// </summary>
/// <remarks>
/// The recorded host sends a whole roster run in one frame, and the run's
/// records only mean anything together: writing them one per datagram spends an
/// outbound sequence on each and hands the peer a roster it never saw assembled.
/// The roster answer and the post-join burst both go out this way.
/// </remarks>
public static class RosterRunSendUtils
{
    /// <summary>
    /// Writes a run of records as one datagram, or one message at a time where
    /// the context cannot batch.
    /// </summary>
    /// <param name="context">Context of the message being answered.</param>
    /// <param name="run">Records to write, in order.</param>
    /// <param name="compressed">
    /// Whether the run goes out LZSS-compressed with the header's compression
    /// marker set. The recorded host sends the roster run, the roster repeat
    /// and the post-join burst compressed and the two one-byte records that
    /// close the exchange plain; where the context cannot batch, the fallback
    /// is the uncompressed path because a plain record cannot be made
    /// compressed without changing what it means.
    /// </param>
    public static async Task SendAsync(
        PeerContext context,
        IReadOnlyList<RosterRecord> run,
        bool compressed = false)
    {
        var batch = compressed
            ? context.SendRecordsCompressed ?? context.SendRecords
            : context.SendRecords;

        if (batch is not { } sendRecords)
        {
            foreach (var record in run)
            {
                await context.Send(record.Type, record.Body, record.Ordinal);
            }

            return;
        }

        await sendRecords(
            [.. run.Select(record => FrameBuilderUtility.MessageOf(record.Type, record.Body, record.Ordinal))]);
    }
}
