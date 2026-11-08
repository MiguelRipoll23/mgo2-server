using Mgo2Server.GameplayServer.Rooms;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The room's sides, as the round's scoreboard and its ticket limit need them:
/// one total per side over the players the room itself places on it.
/// </summary>
/// <remarks>
/// It is split from <c>MatchScoreService.cs</c> for the size rule, and because the
/// two answer different questions: the main file is each player's own numbers and
/// the deltas they queue, and this is what those numbers add up to across a side —
/// which is what a round that ends on its kill limit is decided on
/// (<see cref="TeamLimits"/>).
/// </remarks>
public sealed partial class MatchScoreService
{
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
}
