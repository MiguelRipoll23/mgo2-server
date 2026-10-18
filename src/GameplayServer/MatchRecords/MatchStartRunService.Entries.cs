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
/// (<see cref="Commands.Peer.Room.PlayerProfileHandler"/>), and what that pass
/// writes is the briefing's run and nothing else: the lobby-slot handshake's own
/// confirmation is written when the room's round opens
/// (<see cref="ConfirmEntryAsync"/>), which is where the captures put it.
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
    /// The sequence each peer's entry named, which is the one its lobby-slot
    /// confirmation answers.
    /// </summary>
    /// <remarks>
    /// It is kept rather than answered in the pass that heard it: no capture
    /// confirms the entry's own echo (see <see cref="ConfirmEntryAsync"/>).
    /// </remarks>
    private readonly Dictionary<PeerSession, byte> entrySequences = [];

    /// <summary>
    /// Notes that a peer has asked to be placed in the room, which is what a
    /// briefing's run is written in answer to.
    /// </summary>
    /// <param name="context">Context of the entry the peer asked for.</param>
    public void OnRoomEntry(PeerContext context)
    {
        bool first;
        lock (gate)
        {
            first = entered.Add(context.Session);
            if (first)
            {
                entrySequences[context.Session] =
                    Rooms.TeamArrayFramesUtils.SequenceOf(context.Message.Body);
            }
        }

        if (!first)
        {
            return;
        }

        logger.LogDebug(
            "UDP {LocalPort}: {RemoteAddress} asked to be placed in the room; the run it is owed "
            + "follows its entry and not its report",
            context.LocalPort,
            context.Remote);
    }

    /// <summary>
    /// Answers the peer's entry with the lobby-slot confirmation and the pair
    /// under the next sequence, which is what a briefing ends on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not in the pass that heard the echo.</b> Every capture writes the
    /// confirmation far behind the entry that asked for it and never beside it:
    /// `mgo2-started-briefing-then-solo-game` echoes <c>82 7e</c> at t+55.312 and
    /// is confirmed <c>81 7e</c> at t+167.992, four seconds before its round;
    /// `mgo2-game-started-on-briefing` echoes at t+56.360 and is confirmed at
    /// t+193.621; `mgo2-game-joined-when-red-team-then-briefing` echoes at t+57.983
    /// and is confirmed at t+72.560. `mgo2-game1` and `mgo2-started-on-solo-game`
    /// are confirmed nowhere in their captures at all.
    /// </para>
    /// <para>
    /// <b>A confirmation in the entry pass stops the client where it stands.</b>
    /// `myserver20.pcapng` is that: the echo at t+61.265 was answered 37 ms later
    /// with the pair, <c>81 f3</c>, the group and the pair at <c>f4</c> (twice, once
    /// per echo record), the briefing's run followed 164 ms after it, and the
    /// client acknowledged every record and then sent nothing but its own heartbeat
    /// for two minutes — never the <c>1d</c> stage records every reference client
    /// writes two seconds after its entry, never a ready, and never an echo of the
    /// <c>f4</c> it had just been handed.
    /// </para>
    /// </remarks>
    /// <param name="context">Context of the peer whose entry is answered.</param>
    private async Task ConfirmEntryAsync(PeerContext context)
    {
        byte sequence;
        lock (gate)
        {
            if (!entrySequences.TryGetValue(context.Session, out sequence))
            {
                return;
            }
        }

        await teamArrays.ConfirmAsync(context, sequence);
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

    /// <summary>
    /// Forgets a peer's entry: a session that is gone has no sequence left to
    /// confirm, and the next peer to enter starts its own.
    /// </summary>
    /// <param name="session">Session that ended.</param>
    private void ForgetEntry(PeerSession session)
    {
        lock (gate)
        {
            entrySequences.Remove(session);
        }
    }
}
