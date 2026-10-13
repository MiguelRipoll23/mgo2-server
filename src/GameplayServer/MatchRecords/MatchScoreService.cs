using Mgo2Server.GameplayServer.Rooms;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The host's own score for the round: what each player of the room has killed,
/// died and scored, with the pending scoreboard deltas a change leaves behind.
/// </summary>
/// <remarks>
/// <para>
/// <b>The scoreboard is the host's, and this is the host's own count.</b> Every
/// scoreboard record and every kill-feed record in the captures travels host →
/// client, so the host is the side that has a score at all; a client that only
/// reports its own state publishes none
/// (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §3). What the host has to count
/// from is the players' own records as they arrive, which is what
/// <see cref="NoteDeath"/> and <see cref="NoteKill"/> are the two doors for.
/// </para>
/// <para>
/// <b>A team's score falls back to the opposing team's deaths.</b> The host
/// cannot attribute a killer from the players' records the room reports — nothing
/// on this channel says who fired — but it does know who died, and in the rule
/// whose round ends on tickets the two are the same count: a team's kill total is
/// the deaths it inflicted, and a team reaches the limit when its opponents have
/// run out of the lives the limit grants. So a side's limit score is the kills a
/// kill feed has named for it, and the opposing side's deaths when no feed has
/// named any (<see cref="TeamLimits"/>) — the measured quantity the rule is
/// played on rather than a killer invented to stand behind it.
/// </para>
/// <para>
/// A change is queued, not written here: the record that publishes it is a
/// roster-shaped payload and belongs to whoever is on the channel, so
/// <see cref="TakeDeltas"/> hands the changed bits to the publisher and clears
/// them. That is also what keeps one player's number from being re-sent on
/// every change another player makes.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class MatchScoreService(ILogger<MatchScoreService> logger)
{
    /// <summary>One team's total for the round.</summary>
    /// <param name="Team">Raw team value the room's rows hold, 0 or 1.</param>
    /// <param name="Kills">Kills the team's players have been credited with.</param>
    /// <param name="Deaths">Times the team's players have been recorded dead.</param>
    /// <param name="KillsKnown">
    /// Whether a kill feed has named any of the team's kills. A team it has not
    /// is a team whose score is read from its opponents' deaths instead
    /// (<see cref="TeamLimits"/>).
    /// </param>
    public sealed record TeamScore(short Team, int Kills, int Deaths, bool KillsKnown);

    private sealed class Row
    {
        public int Kills;
        public int Deaths;
        public int Score;
        public bool KillsKnown;
        public bool ScoreKnown;
    }

    private readonly Lock gate = new();
    private readonly Dictionary<byte, Row> rows = [];
    private readonly Dictionary<byte, Dictionary<int, int>> pending = [];

    /// <summary>
    /// Records that a player died, and queues the scoreboard change.
    /// </summary>
    /// <param name="slot">Key in the room's 24-entry player table.</param>
    /// <returns><c>true</c> when the player's number changed.</returns>
    public bool NoteDeath(byte slot)
    {
        lock (gate)
        {
            var row = RowOf(slot);
            row.Deaths++;
            Queue(slot, MatchScoreboardFramesUtils.DeathsBit, row.Deaths);
            logger.LogDebug(
                "Player in slot {Slot} is dead; the round's scoreboard now reads {Deaths} death(s)",
                slot,
                row.Deaths);
            return true;
        }
    }

    /// <summary>
    /// Records a kill and the death that came with it, which is what a kill-feed
    /// record names: the killer's slot and the victim's.
    /// </summary>
    /// <remarks>
    /// A killer that is also the victim is a suicide: it is a death and not a
    /// kill, which is the replay parser's own reading of the feed
    /// (<c>docs/analysis/replay-console/rpdt.js</c>).
    /// </remarks>
    /// <param name="killerSlot">Slot the feed names as the killer.</param>
    /// <param name="victimSlot">Slot the feed names as the victim.</param>
    /// <returns><c>true</c> when the round's score changed.</returns>
    public bool NoteKill(byte killerSlot, byte victimSlot)
    {
        lock (gate)
        {
            var victim = RowOf(victimSlot);
            victim.Deaths++;
            Queue(victimSlot, MatchScoreboardFramesUtils.DeathsBit, victim.Deaths);

            if (killerSlot == victimSlot)
            {
                logger.LogDebug(
                    "Player in slot {Slot} killed itself; the death is counted and the kill is not",
                    killerSlot);
                return true;
            }

            var killer = RowOf(killerSlot);
            killer.Kills++;
            killer.KillsKnown = true;
            Queue(killerSlot, MatchScoreboardFramesUtils.KillsBit, killer.Kills);
            logger.LogDebug(
                "Player in slot {KillerSlot} killed the player in slot {VictimSlot}; {Kills} kill(s), "
                + "{Deaths} death(s)",
                killerSlot,
                victimSlot,
                killer.Kills,
                victim.Deaths);
            return true;
        }
    }

    /// <summary>
    /// Records a kill count a source stated for a player, rather than one a kill
    /// feed named.
    /// </summary>
    /// <remarks>
    /// A scoreboard delta carries the new value of each bit it sets, so a host
    /// that reads one is told a total and not an event. It is kept as the total
    /// it is: the record already carries the count, and a host that added it to
    /// its own would double every kill. Nothing is queued either, because the
    /// delta that stated the total has itself been relayed to the room — a host
    /// that published it a second time would send the same number twice.
    /// </remarks>
    /// <param name="slot">Key in the room's 24-entry player table.</param>
    /// <param name="kills">Kill count the source stated.</param>
    /// <returns><c>true</c> when the player's kills changed.</returns>
    public bool NoteKills(byte slot, int kills)
    {
        lock (gate)
        {
            var row = RowOf(slot);
            if (row.KillsKnown && row.Kills == kills)
            {
                return false;
            }

            row.Kills = kills;
            row.KillsKnown = true;
            logger.LogDebug("Player in slot {Slot} now has {Kills} kill(s)", slot, kills);
            return true;
        }
    }

    /// <summary>
    /// Records a death count a source stated for a player, rather than one this
    /// host measured.
    /// </summary>
    /// <param name="slot">Key in the room's 24-entry player table.</param>
    /// <param name="deaths">Death count the source stated.</param>
    /// <returns><c>true</c> when the player's deaths changed.</returns>
    public bool NoteDeaths(byte slot, int deaths)
    {
        lock (gate)
        {
            var row = RowOf(slot);
            if (row.Deaths == deaths)
            {
                return false;
            }

            row.Deaths = deaths;
            logger.LogDebug("Player in slot {Slot} now has {Deaths} death(s)", slot, deaths);
            return true;
        }
    }

    /// <summary>
    /// Records a score a scoreboard source stated for a player.
    /// </summary>
    /// <param name="slot">Key in the room's 24-entry player table.</param>
    /// <param name="score">Score the source stated.</param>
    /// <returns><c>true</c> when the player's score changed.</returns>
    public bool NoteScore(byte slot, int score)
    {
        lock (gate)
        {
            var row = RowOf(slot);
            if (row.ScoreKnown && row.Score == score)
            {
                return false;
            }

            row.Score = score;
            row.ScoreKnown = true;
            logger.LogDebug(
                "Player in slot {Slot} now scores {Score}",
                slot,
                score);
            return true;
        }
    }

    /// <summary>
    /// Starts a round: every player's count is cleared, and the scoreboard's
    /// queued changes with them.
    /// </summary>
    /// <remarks>
    /// A round's score is the round's, which is why the reset is here and not
    /// with the room: the captures' scoreboard steps back to one kill per round
    /// as each round opens.
    /// </remarks>
    public void Reset()
    {
        lock (gate)
        {
            if (rows.Count > 0)
            {
                logger.LogInformation(
                    "Clearing the score of {PlayerCount} player(s) for the round that is opening",
                    rows.Count);
            }

            rows.Clear();
            pending.Clear();
        }
    }

    /// <summary>
    /// The scoreboard deltas queued since the last call, and clears them. Each
    /// delta names only the bits that changed.
    /// </summary>
    /// <returns>The deltas to publish, ordered by slot.</returns>
    public IReadOnlyList<MatchScoreboardFramesUtils.ScoreboardDelta> TakeDeltas()
    {
        lock (gate)
        {
            var deltas = new List<MatchScoreboardFramesUtils.ScoreboardDelta>(pending.Count);
            foreach (var entry in pending.OrderBy(entry => entry.Key))
            {
                if (entry.Value.Count == 0)
                {
                    continue;
                }

                deltas.Add(new MatchScoreboardFramesUtils.ScoreboardDelta(
                    entry.Key,
                    [.. entry.Value.OrderBy(value => value.Key)
                        .Select(value => new MatchScoreboardFramesUtils.ScoreboardValue(value.Key, value.Value))]));
            }

            pending.Clear();
            return deltas;
        }
    }

    /// <summary>
    /// The round's two team totals, from the slots the room itself assigns to
    /// each side.
    /// </summary>
    /// <remarks>
    /// A player the room's rows put on no side counts for neither team, which is
    /// the same reading the lobby-slot pair writes: it marks the slot taken and
    /// names no side, so a score summed over a team the player is not on would
    /// put its numbers on a side the client does not show it as being on.
    /// </remarks>
    /// <param name="slots">The room's players, by slot and side.</param>
    /// <returns>One entry per side the room holds players on.</returns>
    public IReadOnlyList<TeamScore> TeamScores(IReadOnlyList<LobbyTeamSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        lock (gate)
        {
            var totals = new Dictionary<short, (int Kills, int Deaths, bool KillsKnown)>();
            foreach (var slot in slots.Where(slot => slot.Team is 0 or 1))
            {
                var team = slot.Team!.Value;
                var row = rows.TryGetValue(slot.Slot, out var stored) ? stored : null;
                var (kills, deaths, known) = totals.TryGetValue(team, out var total) ? total : (0, 0, false);
                totals[team] = (
                    kills + (row?.Kills ?? 0),
                    deaths + (row?.Deaths ?? 0),
                    known || (row?.KillsKnown ?? false));
            }

            return
            [
                .. totals.OrderBy(entry => entry.Key)
                    .Select(entry => new TeamScore(
                        entry.Key,
                        entry.Value.Kills,
                        entry.Value.Deaths,
                        entry.Value.KillsKnown)),
            ];
        }
    }

    /// <summary>
    /// Each side's score as the rule whose round ends on tickets counts it: the
    /// kills a kill feed has named, and the opposing side's deaths for a side no
    /// feed has named any for.
    /// </summary>
    /// <remarks>
    /// The fallback is the win condition read the other way round rather than a
    /// second opinion: in the rule that ends on a kill limit, a team's score is
    /// the deaths it inflicted, so the side whose opponents have died most is the
    /// side ahead. It is only used while no feed has named a killer, because once
    /// one has, the kills are the measurement and the deaths are a consequence of
    /// them.
    /// </remarks>
    /// <param name="slots">The room's players, by slot and side.</param>
    /// <returns>One score per side the room holds players on, ordered by side.</returns>
    public IReadOnlyList<(short Team, int Score)> TeamLimits(IReadOnlyList<LobbyTeamSlot> slots)
    {
        var teams = TeamScores(slots);
        if (teams.Count < 2)
        {
            return [.. teams.Select(team => (team.Team, team.Kills))];
        }

        return
        [
            .. teams.Select(team => (
                team.Team,
                team.KillsKnown
                    ? team.Kills
                    : teams.First(other => other.Team != team.Team).Deaths)),
        ];
    }

    private Row RowOf(byte slot)
    {
        if (!rows.TryGetValue(slot, out var row))
        {
            row = new Row();
            rows[slot] = row;
        }

        return row;
    }

    private void Queue(byte slot, int bit, int value)
    {
        if (!pending.TryGetValue(slot, out var values))
        {
            values = [];
            pending[slot] = values;
        }

        values[bit] = value;
    }
}
