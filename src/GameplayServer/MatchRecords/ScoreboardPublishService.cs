using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Writes the scoreboard a score change leaves behind: the per-player deltas
/// <see cref="MatchScoreService"/> has queued, under the roster identifier.
/// </summary>
/// <remarks>
/// <para>
/// <b>The scoreboard rides the roster identifier, so it is published like the
/// ready set is.</b> Its body opens on <c>0x0b</c> and names a lobby slot, both
/// of which the room's own payloads do, and the recorded host writes it to the
/// client it is answering and to the rest of the room. The same two writes are
/// made here, for the same reason: a player that killed is owed the record that
/// says so, and every other client's scoreboard has to agree with it.
/// </para>
/// <para>
/// <b>One delta per player, and only the bits that changed.</b> The recorded
/// host writes a record when one of a player's numbers moves and carries the new
/// value; a publisher that rewrote every player's whole score on every kill
/// would send records the client has already accepted and would put values on
/// the wire that did not change.
/// </para>
/// <para>
/// Its fourth byte is the record's index within the roster exchange, and the
/// value the captured scoreboard carries is **[U]**: the parser reads a
/// scoreboard's slot from its body and never from the index, and no capture
/// lines a scoreboard's index up against the record before it. It continues the
/// roster run's numbering rather than restarting it, which is the one property
/// the surrounding payloads are measured to keep.
/// </para>
/// </remarks>
/// <param name="score">The host's own score, which this publishes.</param>
/// <param name="roster">Roster of the room, whose exchange the index continues.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class ScoreboardPublishService(
    MatchScoreService score,
    RoomRosterService roster,
    ILogger<ScoreboardPublishService> logger)
{
    /// <summary>
    /// Writes every queued score change to the peer the change arrived through
    /// and to the rest of the room.
    /// </summary>
    /// <param name="context">Context the change arrived through.</param>
    public async Task PublishAsync(PeerContext context)
    {
        var deltas = score.TakeDeltas();
        if (deltas.Count == 0)
        {
            return;
        }

        var ordinal = roster.NextRunOrdinal;
        var records = new List<RosterRecord>(deltas.Count);
        foreach (var delta in deltas)
        {
            records.Add(new RosterRecord(
                MatchScoreboardFramesUtils.RecordType,
                MatchScoreboardFramesUtils.BuildBody(delta),
                ordinal++));
        }

        logger.LogDebug(
            "UDP {LocalPort}: publishing {RecordCount} scoreboard delta(s) to {RemoteAddress} and the room",
            context.LocalPort,
            records.Count,
            context.Remote);

        await RosterRunSendUtils.SendAsync(context, records);
        foreach (var record in records)
        {
            await context.Broadcast(record.Type, record.Body, record.Ordinal);
        }
    }
}
