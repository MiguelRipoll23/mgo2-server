using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The round's transitions that are not a round opening or closing: the reset
/// an emptied room takes, and the result the round before it ended on.
/// </summary>
/// <remarks>
/// It is split from <c>MatchRoundStateService.cs</c> for the size rule and
/// because the two members here answer a different question from the rest: the
/// main file is the round now playing, and this is what the room does around it
/// — the reset when nobody is left in it
/// (<see cref="MatchStartRunService.ResetWhenEmpty"/>), and the result a
/// closed round leaves for the writers that publish it.
/// </remarks>
public sealed partial class MatchRoundStateService
{
    /// <summary>
    /// The round clock a briefing is held on: the whole time of the round the
    /// briefing precedes, or zero for a room with nothing in front of it.
    /// </summary>
    /// <remarks>
    /// <b>A fresh room's briefing holds the whole clock; a room between rounds
    /// holds zero.</b> The reference stops a fresh room's briefing records where it
    /// starts the beat and goes on writing that round's whole clock, unchanged, for
    /// as long as the room waits: <c>mgo2-started-briefing-then-solo-game</c> writes
    /// <c>01 60 09</c> — 2 400 units of its five-minute round — from t+65.036, 7.4 s
    /// after its run, to t+170.366, four seconds before its round opens at
    /// t+174.371. A room that has played writes the round that just ended: every
    /// round end of <c>mgo2-started-on-game-round1</c> writes <c>01 00 00</c>
    /// through the briefing after it, and only the next round's open writes a whole
    /// clock again. So the value is the round in front of the room for a room that
    /// has one, and zero for a room whose round is over
    /// (<see cref="MatchStartRunService.BeginBriefing"/>).
    /// </remarks>
    private int briefingClockUnits;

    /// <summary>
    /// The rotation index of the round the room returns to when it starts or
    /// empties: <c>GAMEPLAY_SERVER_START_ROUND</c>, counted from zero.
    /// </summary>
    /// <remarks>
    /// It is the room's place in its rotation and not a round of it, so the rotation
    /// a client is handed is untouched (<see cref="ServerOptions.GameplayServerStartRound"/>).
    /// </remarks>
    private readonly int firstRoundIndex = StartRoundIndex(options.Value.GameplayServerStartRound);

    /// <summary>Whether a round has ended in this process, which a briefing's clock reads.</summary>
    /// <remarks>
    /// It is "a round ended here" and not "the count is above zero": a room staged
    /// four rounds into its rotation has played none of them here, so its first
    /// briefing holds the whole clock of the round it precedes
    /// (<see cref="MatchStartRunService.BeginBriefing"/>).
    /// </remarks>
    private bool hasRoundEnded;

    /// <summary>
    /// The rotation index a configured round number names: <c>GAMEPLAY_SERVER_START_ROUND</c>
    /// is the round counted from one, and a rotation is read from zero.
    /// </summary>
    /// <param name="startRound">The configured round, counted from one.</param>
    /// <returns>The rotation index it names.</returns>
    public static int StartRoundIndex(int startRound) => Math.Max(1, startRound) - 1;

    /// <summary>Whether a round has ended in this process.</summary>
    public bool HasRoundEnded
    {
        get
        {
            lock (gate)
            {
                return hasRoundEnded;
            }
        }
    }

    /// <summary>
    /// The round clock a briefing is held on, or zero while no briefing is running
    /// and no round plays.
    /// </summary>
    public int BriefingClockUnits
    {
        get
        {
            lock (gate)
            {
                return briefingClockUnits;
            }
        }
    }

    /// <summary>
    /// Notes the round clock a briefing is held on: the whole time of the round
    /// that briefing precedes.
    /// </summary>
    /// <param name="units">The round's whole time, in the stream's 125 ms ticks.</param>
    public void NoteBriefingClock(int units)
    {
        lock (gate)
        {
            briefingClockUnits = Math.Max(0, units);
        }
    }

    /// <summary>
    /// Returns an emptied room to its first round — the configured one — with the
    /// round now playing dropped without a result and the count, the clock and the
    /// settings started over.
    /// </summary>
    /// <remarks>
    /// <b>A room nobody is in has no round.</b> The count, the clock and the
    /// settings belong to a match, and the match whose clients have all gone is
    /// over: the next client into the room arrives at its first round and not into
    /// the one that was playing when the room emptied. It is the same reason the
    /// settings are resolved again at the next open. Nothing is written here — the
    /// opener that owns the transition is the one that publishes it
    /// (<see cref="MatchStartRunService.ResetWhenEmpty"/>).
    /// </remarks>
    /// <returns><c>true</c> when a round was playing and has been dropped.</returns>
    public bool Reset()
    {
        lock (gate)
        {
            var ended = isOpen;
            isOpen = false;
            lastRoundResult = null;
            roundSettings = null;
            openedAt = null;
            clockStartsAt = 0;
            // Back to the round this room starts on rather than to zero: a room
            // staged mid-rotation returns to the round it was staged on, which is
            // the round its clients were told about (GameplayServerStartRound).
            roundNumber = firstRoundIndex;
            hasRoundEnded = false;
            vitalsArrived = false;
            briefingClockUnits = 0;
            writtenRuns.Clear();
            logger.LogInformation(
                ended
                    ? "The room emptied; the round count, the round's clock and the round's tickets start over"
                    : "The room emptied between rounds; the round count, the round's clock and the round's "
                        + "tickets start over");
            return ended;
        }
    }

    /// <summary>
    /// The result the round that just ended was closed with, or <c>null</c> when
    /// this host had none for it.
    /// </summary>
    /// <remarks>
    /// It is kept past the round's close because the result is written behind it:
    /// a peer's round watcher sees the round close and writes the write it never
    /// had while the round was playing.
    /// </remarks>
    public byte? LastRoundResult
    {
        get
        {
            lock (gate)
            {
                return lastRoundResult;
            }
        }
    }
}
