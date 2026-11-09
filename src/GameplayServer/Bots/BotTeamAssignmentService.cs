using Mgo2Server.GameplayServer.Rooms;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Resolves which side a bot may stand on, from the sides the match's players are
/// actually on.
/// </summary>
/// <remarks>
/// <para>
/// <b>No side is named anywhere, so no side is hardcoded.</b> The input is the set
/// of sides the room's own rows hold for its human players and for the bots already
/// placed, and the answer is derived from that set: <c>ENEMY</c> takes a side no
/// human holds, and <c>BOTH</c> takes whichever side holds the fewest bots. A match
/// where the humans sit on both sides has no side free for an enemy, so a bot is
/// placed opposite the smaller human presence — the reading of "the opposing team"
/// that a two-sided match leaves, and it is applied to measured sides rather than to
/// an assumed pair.
/// </para>
/// <para>
/// A room whose humans hold no side at all resolves to <c>null</c>: there is
/// nothing to oppose and nothing to fill, and a bot placed on a side picked at
/// random would be on the wrong side of a match nobody had started.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class BotTeamAssignmentService(ILogger<BotTeamAssignmentService> logger)
{
    /// <summary>
    /// The side a new bot should hold, or <c>null</c> when the match states none.
    /// </summary>
    /// <param name="policy">Which sides bots may hold.</param>
    /// <param name="humanSides">Sides the room's human players hold.</param>
    /// <param name="botSides">Sides the room's bots already hold.</param>
    /// <returns>A side, or <c>null</c> when none can be stated.</returns>
    public short? ChooseTeam(
        BotTeamPolicy policy,
        IReadOnlyList<short> humanSides,
        IReadOnlyList<short> botSides)
    {
        ArgumentNullException.ThrowIfNull(humanSides);
        ArgumentNullException.ThrowIfNull(botSides);

        if (policy == BotTeamPolicy.None)
        {
            return null;
        }

        var humans = humanSides.Where(side => RoomSideUtils.IsSide(side)).ToList();
        var bots = botSides.Where(side => RoomSideUtils.IsSide(side)).ToList();

        if (policy == BotTeamPolicy.Both)
        {
            var side = CandidateSides(humans, bots)
                .OrderBy(candidate => bots.Count(held => held == candidate))
                .ThenBy(candidate => humans.Count(held => held == candidate))
                .ThenBy(candidate => candidate)
                .First();

            logger.LogDebug(
                "BOTH: side {Side} holds the fewest bots ({BotCount} bot(s), {HumanCount} human(s))",
                side,
                bots.Count(held => held == side),
                humans.Count(held => held == side));
            return side;
        }

        var occupied = humans.Distinct().OrderBy(side => side).ToList();
        if (occupied.Count == 0)
        {
            logger.LogDebug(
                "ENEMY: no human holds a side of this match, so no side can be opposed and no bot is placed");
            return null;
        }

        if (occupied.Count == 1)
        {
            var enemy = RoomSideUtils.Opposite(occupied[0]);
            logger.LogDebug(
                "ENEMY: the humans hold side {HumanSide}, so the bot takes side {BotSide}",
                occupied[0],
                enemy);
            return enemy;
        }

        var fewerHumans = occupied
            .OrderBy(side => humans.Count(held => held == side))
            .ThenBy(side => side)
            .First();

        logger.LogDebug(
            "ENEMY: the humans hold both sides, so the bot opposes the smaller presence on side {Side}",
            fewerHumans);
        return fewerHumans;
    }

    /// <summary>
    /// The sides a fill may place a bot on: the ones this room's players and bots
    /// already state, and, while they state only one, the side a two-side match
    /// opposes to it.
    /// </summary>
    /// <remarks>
    /// <b>A room's sides are the ones it holds, not the ones the protocol allows.</b>
    /// The protocol can state three sides (blue, red and orange,
    /// <see cref="RoomSideUtils"/>), and a fill that considered all three would put a
    /// bot on orange in a two-side room that had never named it. So the candidates
    /// are derived from the room, and a room holding one side is a two-side match
    /// missing its other, which is where <see cref="RoomSideUtils.Opposite"/> comes
    /// in. A room holding all three fills across all three.
    /// </remarks>
    /// <param name="humans">Sides the room's human players hold.</param>
    /// <param name="bots">Sides the room's bots already hold.</param>
    /// <returns>The candidate sides, in ascending order while the room states any.</returns>
    private static List<short> CandidateSides(IReadOnlyList<short> humans, IReadOnlyList<short> bots)
    {
        var stated = humans
            .Concat(bots)
            .Distinct()
            .OrderBy(side => side)
            .ToList();

        if (stated.Count == 0)
        {
            return [RoomSideUtils.FirstSide];
        }

        if (stated.Count == 1 && RoomSideUtils.Opposite(stated[0]) is { } opposite)
        {
            stated.Add(opposite);
        }

        return stated;
    }
}
