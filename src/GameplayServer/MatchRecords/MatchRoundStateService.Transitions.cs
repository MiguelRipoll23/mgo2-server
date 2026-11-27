using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The round's transitions that are not a round opening: its close and the
/// results it leaves, and the reset an emptied room takes.
/// </summary>
/// <remarks>
/// It is split from <c>MatchRoundStateService.cs</c> for the size rule and
/// because the members here answer a different question from the rest: the main
/// file is the round now playing, and this is what the room does around it — the
/// close and the results the writers that publish them read, and the reset when
/// nobody is left in it (<see cref="MatchStartRunService.ResetWhenEmpty"/>).
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
    /// briefing <c>mgo2-started-on-game-round1</c> runs after a played round writes
    /// <c>01 00 00</c> through its whole length, and only the next round's open writes
    /// a whole clock again. The rounds of one game need no such stretch: they run
    /// back to back and the room is never held on zero between them
    /// (<c>docs/protocol/MATCH_FLOW.md</c> §5). So the value is the round in front of
    /// the room for a room that has one, and zero for a room whose round is over
    /// has one, and zero for a room whose round is over
    /// (<see cref="MatchStartRunService.BeginBriefing"/>).
    /// </remarks>
    private int briefingClockUnits;

    /// <summary>Whether a round has ended in this process, which a briefing's clock reads.</summary>
    /// <remarks>
    /// It is "a round ended here" and not "the count is above zero": a room staged
    /// four rounds into its rotation has played none of them here, so its first
    /// briefing holds the whole clock of the round it precedes
    /// (<see cref="MatchStartRunService.BeginBriefing"/>).
    /// </remarks>
    private bool hasRoundEnded;

    /// <summary>Whether the round that just ended was its game's last.</summary>
    private bool lastRoundClosedGame;

    /// <summary>
    /// When the stage now ending releases the next briefing, or <c>null</c> while
    /// no stage end runs.
    /// </summary>
    private long? stageEndAt;

    /// <summary>
    /// Units the last round's clock stopped at, held on the wire while the stage's
    /// result screens run.
    /// </summary>
    private int stageEndClockUnits;

    /// <summary>
    /// Identity of the stage end: a monotonic version, so a release armed for one
    /// stage cannot act on a later one, and a reset or a new open invalidates it.
    /// </summary>
    private int stageEndVersion;

    /// <summary>
    /// Rounds this room has closed, counted from one and reset only when the room
    /// empties: it is the identity a writer uses to award a round's win exactly once,
    /// which a round number cannot be, because an emptied room returns to its first
    /// round.
    /// </summary>
    private int roundsClosed;

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
    /// Whether the stage's result screens are running: the round is closed, the
    /// next briefing is armed, and its count is held until those screens are done.
    /// </summary>
    public bool IsStageEnding
    {
        get
        {
            lock (gate)
            {
                return stageEndAt is not null;
            }
        }
    }

    /// <summary>
    /// When the stage end releases the next briefing, or <c>null</c> while no stage
    /// end runs.
    /// </summary>
    public DateTimeOffset? StageEndAt
    {
        get
        {
            lock (gate)
            {
                return stageEndAt is { } milliseconds
                    ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
                    : null;
            }
        }
    }

    /// <summary>
    /// Units of the last round's clock while the stage's result screens run: the
    /// capture keeps writing the value the round stopped at, and not a zero, until
    /// the next briefing begins. Zero once no stage end runs.
    /// </summary>
    public int StageEndClockUnits
    {
        get
        {
            lock (gate)
            {
                return stageEndClockUnits;
            }
        }
    }

    /// <summary>
    /// The stage end now armed, for the release that belongs to it.
    /// </summary>
    public int StageEndVersion
    {
        get
        {
            lock (gate)
            {
                return stageEndVersion;
            }
        }
    }

    /// <summary>
    /// Releases the next briefing after the stage's result screens have run: the
    /// clock the room held goes to zero and the briefing's count may move.
    /// </summary>
    /// <param name="version">
    /// The stage end's version, as <see cref="StageEndVersion"/> numbered it. A room
    /// that emptied, a round that closed afterwards, or a new open is not this stage
    /// end and is left alone.
    /// </param>
    /// <returns><c>true</c> when this stage end was still the room's and is now over.</returns>
    public bool EndStageEnd(int version)
    {
        lock (gate)
        {
            if (stageEndVersion != version || stageEndAt is null)
            {
                return false;
            }

            stageEndAt = null;
            stageEndClockUnits = 0;
            stageEndVersion++;
            briefingClockUnits = 0;
            screensClockUnits = 0;
            return true;
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
            lastRoundOverVerdict = RoundOverVerdict.None;
            lastRoundClosedGame = false;
            stageEndAt = null;
            stageEndClockUnits = 0;
            stageEndVersion++;
            ClearRoundScreens();
            roundsClosed = 0;
            roundSettings = null;
            openedAt = null;
            clockStartsAt = 0;
            isSolo = false;
            clockFrozenAt = null;
            // Back to the rotation's first entry: a room nobody is in has no round
            // of its own, so the next client into it arrives at the first one.
            roundNumber = 0;
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

    private RoundOverVerdict lastRoundOverVerdict;

    /// <summary>
    /// The round-over verdict the round that just ended was classified with for
    /// the protocol reading. The dedicated host does not write the <c>0x0e</c>
    /// record (<see cref="MatchResultService.WriteResultAsync"/>).
    /// </summary>
    public RoundOverVerdict LastRoundOverVerdict
    {
        get
        {
            lock (gate)
            {
                return lastRoundOverVerdict;
            }
        }
    }

    /// <summary>
    /// Ends the round now playing and keeps the results it ended on. Nothing is
    /// written here: the opener that owns the transition does that, and the
    /// writers that publish the results read them from here.
    /// </summary>
    /// <param name="roundResult">
    /// The result code the round ended on, in the operations state block's own
    /// coding (<see cref="MatchRecordBodiesUtils.StateResultTeamZero"/>), or
    /// <c>null</c> when this host has no result to write for it.
    /// </param>
    /// <param name="closesGame">
    /// Whether this round was its game's last. The game's own result is decided by
    /// the writer that publishes the round's, from the rounds-won counters
    /// (<see cref="MatchResultService.DecideMatchResultAsync"/>), because that decision
    /// needs the side each player holds and the counters this service does not keep.
    /// </param>
    /// <param name="roundOverVerdict">
    /// Verdict the close is classified with (<see cref="MatchRoundOverBodiesUtils"/>);
    /// the dedicated host keeps it off the wire.
    /// </param>
    /// <returns>The round that ended, or <c>null</c> when none was playing.</returns>
    public int? Close(byte? roundResult, bool closesGame = false, RoundOverVerdict roundOverVerdict = RoundOverVerdict.None)
    {
        lock (gate)
        {
            if (!isOpen)
            {
                return null;
            }

            var closedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            stageEndVersion++;
            var clockAtClose = roundSettings is { } settings
                ? RoundClockUtils.RemainingUnits(
                    settings,
                    clockStartsAt,
                    clockFrozenAt ?? closedAt)
                : 0;

            isOpen = false;
            hasRoundEnded = true;
            lastRoundResult = roundResult;
            lastRoundOverVerdict = roundOverVerdict;
            lastRoundClosedGame = closesGame;
            roundsClosed++;
            roundSettings = null;
            openedAt = null;
            clockStartsAt = 0;
            clockFrozenAt = null;

            // A round inside a stage writes its clock as zero and holds it there
            // until the next round opens: `joined-when-red-team` writes `01 00 00`
            // at t+423.051, one millisecond before its result write, and the round
            // that follows opens on its whole `01 60 09` again (BRIEFING_FLOW.md §4).
            // A round that ends its stage is followed by the stage's own result
            // screens, and the played clock stays where it stopped through them:
            // `rounded-ended-by-reaching-tickets-limit` writes `01 8c 07` (1 932
            // units) from t+164.479 to t+204.480, where the round ended at t+163.597.
            // Only the stage end writes the zero.
            ArmScreensEnd(closedAt, clockAtClose, closesGame);

            logger.LogInformation(
                "Round {RoundNumber} ended{Result}{StageEnd}",
                roundNumber,
                roundResult is { } result ? $" with result code {result}" : " with no result to write",
                closesGame
                    ? $"; its stage's result screens hold the next briefing for "
                        + $"{MatchScreenTimingsUtils.StageEndMilliseconds} ms"
                    : string.Empty);
            return roundNumber;
        }
    }

    /// <summary>
    /// Whether the round that just ended was its game's last, and so whether the
    /// game's rounds-won counters are owed a fresh start after the screen that
    /// states them (<see cref="MatchScoreService.ResetRoundWins"/>).
    /// </summary>
    public bool LastRoundClosedGame
    {
        get
        {
            lock (gate)
            {
                return lastRoundClosedGame;
            }
        }
    }

    /// <summary>
    /// Rounds this room has closed, counted from one and never reset — the identity
    /// a writer uses to award a round's win exactly once, where a round number
    /// cannot be one because an emptied room returns to its first round.
    /// </summary>
    public int RoundsClosed
    {
        get
        {
            lock (gate)
            {
                return roundsClosed;
            }
        }
    }
}
