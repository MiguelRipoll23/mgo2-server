using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The entry half of the round opener: the note that a peer has asked to be
/// placed in the room, which is the event a briefing's run is written in answer
/// to.
/// </summary>
/// <remarks>
/// <para>
/// <b>The room's state answers the client's entry and not its report.</b> A
/// joining client's map-loaded report arrives while it is still loading its map:
/// 0.4 s before its entry in <c>mgo2-started-briefing-then-solo-game</c> and
/// 0.5 s in <c>mgo2-game-started-on-briefing</c>, the two captures of a working
/// host that hold a single join. Every run those captures write follows an entry
/// — 0.3 s, 0.6 s, 0.7 s, 0.9 s, 1.2 s, 1.4 s, 1.9 s, 2.3 s and 4.7 s behind
/// one, and in <c>mgo2-game-joined-when-red-team-then-briefing</c> two of them
/// follow an entry 39 s and 75 s after that room's last report, which no report
/// can explain (<c>docs/analysis/join-stall-after-mode2.md</c> §18).
/// </para>
/// <para>
/// <b>What a run written for a report instead produces.</b> The room's state is
/// handed to a client that has not entered yet, so the client that reaches its
/// briefing screen never hears the briefing's state at all: it reports again, it
/// readies nothing and it never shows the screen — `myserver19`'s join, and the
/// same shape in the live sessions before it (`myserver16`, `myserver17`).
/// </para>
/// <para>
/// The note is taken in the pass that answers the echo
/// (<see cref="Commands.Peer.Room.PlayerProfileHandler"/>), because the echo is
/// the lobby-slot handshake's own request and its answer
/// (<see cref="Rooms.TeamArrayService.ConfirmAsync"/>) is written in the same
/// pass; the run then waits on the note beside its report
/// (<c>MatchStartRunService.Runs.cs</c>).
/// </para>
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Peers that have been placed in the room: each one echoed the lobby-slot
    /// pair (<c>82 NN</c>/<c>84 NN</c>), which is the client's own request to
    /// enter.
    /// </summary>
    private readonly HashSet<PeerSession> entered = [];

    /// <summary>
    /// Notes that a peer has asked to be placed in the room, which is what a
    /// briefing's run is written in answer to.
    /// </summary>
    /// <param name="context">Context of the entry the peer asked for.</param>
    public void OnRoomEntry(PeerContext context)
    {
        lock (gate)
        {
            if (!entered.Add(context.Session))
            {
                return;
            }
        }

        logger.LogDebug(
            "UDP {LocalPort}: {RemoteAddress} asked to be placed in the room; the run it is owed "
            + "follows its entry and not its report",
            context.LocalPort,
            context.Remote);
    }

    /// <summary>
    /// Whether a peer has asked to be placed in the room.
    /// </summary>
    /// <param name="session">Session the peer is on.</param>
    /// <returns><c>true</c> once the peer's entry has been heard.</returns>
    private bool HasEntered(PeerSession session)
    {
        lock (gate)
        {
            return entered.Contains(session);
        }
    }
}
