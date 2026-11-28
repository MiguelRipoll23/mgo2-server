using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The placement half of the round opener: the room's player entries, written
/// behind the round's run so that a round places its players whether or not
/// their clients ask to be placed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The client asks to be placed in every round, and this host's stops.</b>
/// The request is a four-byte <c>83 NN</c> that follows the run by 0.17 s in
/// <c>rounded-ended-by-reaching-tickets-limit</c> (run t+99.364, request
/// t+99.533) and by 0.84 s in <c>briefing-then-solo-game</c> (run t+147.65,
/// request t+148.49), and the answer to it — the pair, one entry per player of
/// the room and the roster's close — is what gives a player a body in the map
/// (<see cref="Rooms.TeamArrayService.EnterRoomAsync"/>). The live session of
/// 2026-11-28 00:23 asked at its join (19 records at 00:23:09, the round it
/// spawned into) and not once at round 2's open, and it then reported its
/// map-loaded state every second for the whole of that round without writing a
/// position record: the round played under a client that never appeared in it.
/// </para>
/// <para>
/// <b>So the answer is written behind the run, unasked.</b> The reference writes
/// that same answer at every round: the room's entries land 0.18 s behind round
/// 2's run (t+99.547) and behind round 1's at t+18.021. The entries are the
/// spawn, so a client handed them stands where the round puts it; a client that
/// also asks is answered again with the same records, which is what the captures'
/// re-sent requests show.
/// </para>
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <remarks>
    /// It is skipped when the room has left the round the run was written for, for
    /// the reason the run's repeat is: a spawn handed to a room that moved on lands
    /// in the phase behind it. The entries go out once per round and peer
    /// (<see cref="Rooms.TeamArrayService"/>'s placed-round marker), so the pass
    /// that places a peer on its report and the run behind it do not write the set
    /// twice.
    /// </remarks>
    /// <summary>
    /// Places the room's players in the map behind a round's run.
    /// </summary>
    /// <param name="context">Context the entries are written through.</param>
    /// <param name="round">Round number the run was written for.</param>
    private async Task PlacePeerInRoundAsync(PeerContext context, int round)
    {
        if (!roundState.IsOpen || roundState.RoundNumber != round)
        {
            return;
        }

        logger.LogDebug(
            "UDP {LocalPort}: placing {RemoteAddress} in the map behind the round's run, "
            + "at sequence {Sequence}",
            context.LocalPort,
            context.Remote,
            roster.PairSequenceOf(context.Remote.ToString()));

        await teamArrays.EnterRoomAsync(
            context,
            roster.PairSequenceOf(context.Remote.ToString()));
    }
}
