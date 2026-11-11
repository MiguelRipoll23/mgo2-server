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
/// <b>Its fourth byte is the roster exchange's next counter value.</b> Measured
/// on `mgo2-game-joined-when-red-team-then-briefing`, every record under the
/// roster identifier carries one ascending sequence: the lobby-slot arrays take
/// a pair of consecutive values (23 and 24, then 28 and 29) and the scoreboard
/// deltas take the values between them (25, 26 and 27), so the index is the
/// identifier's counter and not a restart. Each delta here takes the next value
/// (<see cref="Rooms.RoomRosterService.TakeRunOrdinal"/>); the replay parser
/// reads a scoreboard's slot from its body and never from the index.
/// </para>
/// </remarks>
/// <param name="score">The host's own score, which this publishes.</param>
/// <param name="roster">Roster of the room, whose exchange the index continues.</param>
/// <param name="roundState">The room's round, whose rule states the ticket allotment.</param>
/// <param name="lobbySlots">Reader of the room's players, by slot and side.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class ScoreboardPublishService(
    MatchScoreService score,
    RoomRosterService roster,
    MatchRoundStateService roundState,
    LobbySlotService lobbySlots,
    ILogger<ScoreboardPublishService> logger)
{
    /// <summary>
    /// The remaining tickets each side has already been told about: a death that
    /// does not move a side's count writes nothing.
    /// </summary>
    private readonly Dictionary<short, int> publishedTickets = [];

    /// <summary>The round the cached ticket counts belong to, which resets them.</summary>
    private int publishedRound;

    /// <summary>
    /// Writes every queued score change to the peer the change arrived through
    /// and to the rest of the room, then each side's remaining tickets when a death
    /// has spent one (<see cref="TeamTicketBodiesUtils"/>).
    /// </summary>
    /// <param name="context">Context the change arrived through.</param>
    public async Task PublishAsync(PeerContext context)
    {
        var deltas = score.TakeDeltas();
        var tickets = await TicketRecordsAsync();
        if (deltas.Count == 0 && tickets.Count == 0)
        {
            return;
        }

        var records = new List<RosterRecord>(deltas.Count + tickets.Count);
        foreach (var delta in deltas)
        {
            records.Add(new RosterRecord(
                MatchScoreboardFramesUtils.RecordType,
                MatchScoreboardFramesUtils.BuildBody(delta),
                roster.TakeRunOrdinal()));
        }

        // The ticket record rides behind the scoreboard delta the death queued, in
        // the order the capture writes them: the victim's and killer's deltas, then
        // the side's remaining count (`0b 00 00 00 08 …` at t+40.85 of the working
        // capture).
        foreach (var ticket in tickets)
        {
            records.Add(new RosterRecord(
                MatchScoreboardFramesUtils.RecordType,
                ticket,
                roster.TakeRunOrdinal()));
        }

        logger.LogDebug(
            "UDP {LocalPort}: publishing {RecordCount} scoreboard record(s) to {RemoteAddress} and the room",
            context.LocalPort,
            records.Count,
            context.Remote);

        await RosterRunSendUtils.SendAsync(context, records);
        foreach (var record in records)
        {
            await context.Broadcast(record.Type, record.Body, record.Ordinal);
        }
    }

    /// <summary>
    /// Each side's remaining tickets, for the sides whose count a death has moved.
    /// </summary>
    /// <remarks>
    /// <b>A ticket is spent on a death, and a side's allotment is the rule's own
    /// ticket word.</b> The record is the capture's, its count the allotment less
    /// the deaths this host has counted for that side, and it is written only when
    /// the count changes — the reference writes one per death, never the same value
    /// twice (see <see cref="TeamTicketBodiesUtils"/>). A rule that states no
    /// allotment plays no ticketed round and writes nothing.
    /// </remarks>
    /// <returns>The bodies to publish, one per side that moved.</returns>
    private async Task<IReadOnlyList<byte[]>> TicketRecordsAsync()
    {
        if (roundState.RoundSettings is not { Tickets: { } allotment })
        {
            return [];
        }

        // A round's tickets start over with its score, so the cache is the round's
        // and not the room's: the first death of a new round states the new count
        // even when the number it reaches is one the previous round also held.
        if (roundState.RoundNumber != publishedRound)
        {
            publishedRound = roundState.RoundNumber;
            publishedTickets.Clear();
        }

        var bodies = new List<byte[]>(2);
        foreach (var team in score.TeamScores(await lobbySlots.ReadAsync()))
        {
            var remaining = Math.Max(0, allotment - team.Deaths);
            if (publishedTickets.TryGetValue(team.Team, out var held) && held == remaining)
            {
                continue;
            }

            publishedTickets[team.Team] = remaining;
            bodies.Add(TeamTicketBodiesUtils.Build(team.Team, remaining));
            logger.LogDebug(
                "Team {Team} has {Remaining} of its round's {Allotment} ticket(s) left after {Deaths} "
                + "death(s)",
                team.Team,
                remaining,
                allotment,
                team.Deaths);
        }

        return bodies;
    }
}
