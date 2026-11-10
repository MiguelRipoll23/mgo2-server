using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The half of the gameplay server that empties a room when its last client leaves.
/// </summary>
/// <remarks>
/// <para>
/// The room's round belongs to the clients in it, so the last client out is the one
/// that ends the match: with the room empty the round count, its clock and its
/// tickets start over, and the next client into the room arrives at a first round
/// rather than at the one that emptied. A leaver among others changes nothing
/// (<c>MatchStartRunService.ResetWhenEmpty</c>, and the room's own rule in
/// <c>docs/analysis/empty-and-solo-room-clocks.md</c>).
/// </para>
/// <para>
/// <b>The bots go with the room.</b> A bot is a seat kept free for a player and not a
/// player (<c>RoomRosterService.HumanJoinerCount</c>): with nobody left there is
/// nothing to fill. A room that keeps its bots answers the next client with a roster
/// naming every seat it holds, which is the shape a joining client reads as full and
/// the reason the fill is seated on the report rather than at the profile. So the last
/// client out takes the fill with it, and the fill seats them again on the report the
/// next client sends once it has reached its briefing.
/// </para>
/// <para>
/// <b>What the client who started its session over keeps is its ready.</b> The rest of
/// its briefing state goes with the session it abandoned — its report has to be heard
/// again from the session that fills it now — but its ready was given on the screen it
/// is still looking at, and a room of one leaves its briefing on its ready set and on
/// nothing else. That is the difference between the round opening seconds after the
/// rejoin's report and a held count of sixty that never moves.
/// </para>
/// </remarks>
public sealed partial class GameplayServerService
{
    /// <summary>
    /// Returns an emptied room to its first round and clears the bots that filled it.
    /// </summary>
    /// <param name="session">Session of the client that has just left.</param>
    /// <param name="clientJoinsAgain">
    /// Whether that client started its session over and is coming back into the room
    /// (<see cref="RestartPeerIfHandshakeStartsOver"/>), in which case the ready it
    /// sent into the briefing it is still in stays.
    /// </param>
    private void ResetEmptiedRoom(PeerSession session, bool clientJoinsAgain = false)
    {
        if (!matchStart.ResetWhenEmpty(clientJoinsAgain ? session.DialBack.ToString() : null))
        {
            return;
        }

        settingsBurstScheduler.Reset();
        bots.Reset();
        botActors.Reset();
        botSimulation.Reset();
        positions.Reset();
        vitals.Reset();
        roster.Reset();
        rosterAnswers.Reset();
        teamArrays.Reset();

        logger.LogInformation(
            "[{LogPrefix}] {RemoteAddress} was the room's last client; every in-memory room state was reset, "
            + "so the next player joins a fresh room",
            LogPrefix,
            session.RemoteAddress);
    }
}
