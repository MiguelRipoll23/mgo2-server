namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The counting half of the room roster: how many members it holds, and how many
/// of those are people.
/// </summary>
/// <remarks>
/// <para>
/// <b>A bot is a roster member and not a player.</b> The recorded dedicated hosts
/// put their bots in the roster run — the briefing's ready set names them in the
/// same slots as the players — so <see cref="JoinerCount"/> counts them, and that
/// is the count the run's ordinals are numbered from.
/// </para>
/// <para>
/// <b>Whether anybody is playing is the other question.</b> A room whose only
/// members are this host's bots is a room nobody is in, and the round's reset
/// turns on exactly that (<see cref="MatchRecords.MatchStartRunService.ResetWhenEmpty"/>).
/// Measured on a local run: a client left a bot-filled room, the round stayed open
/// for a population that counted its bots, and the client that rejoined was served
/// "the round that has not ended" — it was never sent one, and it restarted its
/// session every thirteen seconds. A bot that keeps the round of a client who has
/// gone is a bot holding a match nobody is watching.
/// </para>
/// </remarks>
public sealed partial class RoomRosterService
{
    /// <summary>
    /// Number of joining players on the roster, the host itself not counted and
    /// this host's bots included.
    /// </summary>
    public int JoinerCount
    {
        get
        {
            lock (gate)
            {
                return members.Count;
            }
        }
    }

    /// <summary>
    /// Number of joining players on the roster that are people, this host's bots
    /// not counted.
    /// </summary>
    public int HumanJoinerCount
    {
        get
        {
            lock (gate)
            {
                return members.Count(member => !member.IsBot);
            }
        }
    }
}
