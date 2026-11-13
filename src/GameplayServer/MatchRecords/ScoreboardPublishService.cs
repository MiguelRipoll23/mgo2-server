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
    /// The deaths each side has already been told about: a side whose count has not
    /// moved since the last publish writes nothing.
    /// </summary>
    /// <remarks>
    /// The deaths are kept rather than the remaining count because the reference
    /// writes one record per death and only for the side that lost the player — the
    /// working capture's first death writes team <c>0</c>'s <c>19</c> alone and
    /// leaves team <c>1</c>'s twenty standing, not a pair of records. A cache of the
    /// counts wrote both sides on the round's first death, which is a record per
    /// side the capture never sends.
    /// </remarks>
    private readonly Dictionary<short, int> publishedDeaths = [];

    /// <summary>The round the cached ticket counts belong to, which resets them.</summary>
    private int publishedRound;

    /// <summary>
    /// Copies of a delta still to be written: the reference writes each one four times,
    /// the copies 0.6 s apart (slot 11's kill at t+46.45 of <c>working-server-v2</c> is
    /// written again at 47.05, 47.63 and 48.30), so a client that missed the first still
    /// reads the number.
    /// </summary>
    /// <remarks>
    /// <b>A copy is the same record, so it keeps its ordinal.</b> Measured on
    /// <c>.pcapngs/actions/actions.pcapng</c>: slot 12's death is written at t+43.78,
    /// 44.43 and 45.10 and all three carry ordinal <c>0xac</c>; slot 16's kill at
    /// t+48.55 and 49.23 carries <c>0xb1</c> both times. This host took a fresh value
    /// from the room's exchange for every copy — <c>myserver.pcapng</c>'s slot 6 death
    /// reads <c>0xbe</c>, <c>0xc5</c>, <c>0xc9</c> and <c>0xcb</c> on its four writes —
    /// which spends three values of a byte-wide counter per change and makes the
    /// client read one change as four records.
    /// </remarks>
    private readonly List<(long Due, byte Ordinal, byte[] Body)> repeats = [];

    private const int RepeatCount = 3;

    private const int RepeatIntervalMilliseconds = 600;

    /// <summary>Longest a copy may wait before it is dropped as belonging to a past round.</summary>
    private const int StaleRepeatMilliseconds = 3_000;

    /// <summary>
    /// Writes every queued score change to the peer the change arrived through
    /// and to the rest of the room, then each side's remaining tickets when a death
    /// has spent one (<see cref="TeamTicketBodiesUtils"/>).
    /// </summary>
    /// <param name="context">Context the change arrived through.</param>
    public async Task PublishAsync(PeerContext context)
    {
        // **A record says a number moved, so a record of zeroes says nothing moved.**
        // Measured on `.pcapngs/actions/actions.pcapng`: not one of its 451 non-zero
        // scoreboard deltas carries a value of zero, and the reference writes no
        // delta at all for a player whose numbers have not left their starting point.
        // This host wrote sixteen of them for the whole room at t+17.85 of
        // `myserver.pcapng` — one per slot, every value zero — which is sixteeen
        // records a client reads as "back to nothing" for rows it already knows.
        var deltas = score.TakeDeltas()
            .Where(delta => delta.Values.Any(value => value.Value != 0))
            .ToList();
        var tickets = await TicketRecordsAsync();
        if (deltas.Count == 0 && tickets.Count == 0)
        {
            return;
        }

        var records = new List<RosterRecord>(deltas.Count + tickets.Count);
        foreach (var delta in deltas)
        {
            var body = MatchScoreboardFramesUtils.BuildBody(delta);
            var ordinal = roster.TakeRunOrdinal();
            var due = Environment.TickCount64;
            lock (repeats)
            {
                for (var index = 1; index <= RepeatCount; index++)
                {
                    repeats.Add((due + index * RepeatIntervalMilliseconds, ordinal, body));
                }
            }

            records.Add(new RosterRecord(MatchScoreboardFramesUtils.RecordType, body, ordinal));
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
    /// Writes the copies of earlier deltas whose time has come, to the peer and the room.
    /// </summary>
    /// <param name="context">Context the beat runs on.</param>
    public async Task PublishRepeatsAsync(PeerContext context)
    {
        var now = Environment.TickCount64;
        List<(byte Ordinal, byte[] Body)> due;
        lock (repeats)
        {
            repeats.RemoveAll(entry => entry.Due < now - StaleRepeatMilliseconds);
            due = [.. repeats.Where(entry => entry.Due <= now).Select(entry => (entry.Ordinal, entry.Body))];
            repeats.RemoveAll(entry => entry.Due <= now);
        }

        if (due.Count == 0)
        {
            return;
        }

        var records = due
            .Select(copy => new RosterRecord(
                MatchScoreboardFramesUtils.RecordType,
                copy.Body,
                copy.Ordinal))
            .ToList();
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
    /// <b>A ticket is spent on a death, and a side's allotment is the room's own
    /// pair.</b> The record is the capture's, its count the room body's count less
    /// the deaths this host has counted for that side, and it is written only for a
    /// side whose deaths moved — the reference writes one per death, never the same
    /// value twice and never the side that lost nobody (see
    /// <see cref="TeamTicketBodiesUtils"/>). A rule that states no allotment plays
    /// no ticketed round and writes nothing.
    /// </remarks>
    /// <returns>The bodies to publish, one per side that moved.</returns>
    private async Task<IReadOnlyList<byte[]>> TicketRecordsAsync()
    {
        if (roundState.RoundSettings is not { Tickets: not null })
        {
            return [];
        }

        // A round's tickets start over with its score, so the cache is the round's
        // and not the room's: the first death of a new round states the new count
        // even when the number it reaches is one the previous round also held.
        if (roundState.RoundNumber != publishedRound)
        {
            publishedRound = roundState.RoundNumber;
            publishedDeaths.Clear();
        }

        // The count is the room body's own pair and not the bank's ticket word: the
        // panel counts down from what the room states, forty, while the bank's
        // fifty is the kill limit the round ends on. Counting from fifty wrote a
        // count the panel was not holding, so it never moved.
        var allotment = RoomStateReferenceBodyUtils.TicketAllotment;
        var bodies = new List<byte[]>(2);
        foreach (var team in score.TeamScores(await lobbySlots.ReadAsync()))
        {
            if (publishedDeaths.TryGetValue(team.Team, out var held) && held == team.Deaths)
            {
                continue;
            }

            publishedDeaths[team.Team] = team.Deaths;
            var remaining = Math.Max(0, allotment - team.Deaths);
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
