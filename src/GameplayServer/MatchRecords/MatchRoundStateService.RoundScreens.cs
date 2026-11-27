using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The screens that follow a round: the round's own result and score graph, and
/// the stage's result after its last one, and the hold that keeps the next round
/// or the next briefing from starting over them.
/// </summary>
/// <remarks>
/// <para>
/// <b>A round that ends is not a round that is gone.</b> The client spends time
/// on its result screens before it can play again, and the room must not open
/// the next round or arm the next briefing underneath it. The reference keeps
/// writing the closed round's clock — `01 00 00` after a round inside a stage,
/// its stopped value after a stage's last — for that stretch. A round inside a
/// stage is released by the screens' own measured pace: in
/// <c>rounded-ended-by-reaching-tickets-limit.pcapng</c> the first round's result
/// is written at t+65.218 and the next round's vector at t+88.163, 22.9 s later
/// and with no client report in between. A post-screen report that lands inside
/// the stretch opens the round at once (`docs/protocol/MATCH_FLOW.md` §6).
/// </para>
/// <para>
/// <b>A stage's last round has the longer hold.</b> Its screens are the round
/// result and then the stage result, and the reference's next stage does not
/// show a count until t+208.389, 44.8 s after the final round's result at
/// t+163.597 (<c>rounded-ended-by-reaching-tickets-limit.pcapng</c>). The stage
/// end and its clock live in <c>MatchRoundStateService.Transitions.cs</c>; this
/// file is the shorter hold a round inside a stage takes, and the state both
/// holds share.
/// </para>
/// </remarks>
public sealed partial class MatchRoundStateService
{
    /// <summary>
    /// When the screens a round inside a stage left behind may be left, or
    /// <c>null</c> while no such screens run.
    /// </summary>
    private long? roundScreensAt;

    /// <summary>
    /// Identity of the round-end screens: a monotonic version, so a release armed
    /// for one round cannot open a later one, and a reset or a new open
    /// invalidates it.
    /// </summary>
    private int roundScreensVersion;

    /// <summary>
    /// Units the closed round's clock stopped at: the value its screens are held
    /// on, and zero once they are over.
    /// </summary>
    /// <remarks>
    /// <b>The screens hold the clock the round stopped on and do not rewind it.</b>
    /// <c>rounded-ended-by-reaching-tickets-limit.pcapng</c> closes its first round
    /// with the clock at <c>c7 07</c> (1 991 units) and re-states exactly that,
    /// every four seconds from t+65.497 to t+85.495, until the next round's clock
    /// replaces it. A round that ended on its clock therefore holds <c>01 00 00</c>,
    /// which is why the zero is a stopped clock and not a value the screens set.
    /// </remarks>
    private int screensClockUnits;

    /// <summary>
    /// The clock the round's screens are held on, as
    /// <see cref="screensClockUnits"/> holds it.
    /// </summary>
    public int ScreensClockUnits
    {
        get
        {
            lock (gate)
            {
                return screensClockUnits;
            }
        }
    }

    /// <summary>
    /// Whether a round inside a stage has ended and its result screens are still
    /// running: no round may open and no briefing may begin until they are done.
    /// </summary>
    public bool IsRoundEnding
    {
        get
        {
            lock (gate)
            {
                return roundScreensAt is not null;
            }
        }
    }

    /// <summary>The round-end screens now armed, for the release that belongs to them.</summary>
    public int RoundScreensVersion
    {
        get
        {
            lock (gate)
            {
                return roundScreensVersion;
            }
        }
    }

    /// <summary>
    /// Releases the round's result screens, after which the next round of the same
    /// stage may open.
    /// </summary>
    /// <remarks>
    /// The clock a room between rounds carries is already zero
    /// (<see cref="Close"/>), so nothing rewinds here; the release only says the
    /// stretch is over. A room that emptied, or a round that closed after this
    /// one, is not this release and is left alone.
    /// </remarks>
    /// <param name="version">
    /// The screens' version, as <see cref="RoundScreensVersion"/> numbered it.
    /// </param>
    /// <returns><c>true</c> when these screens were still the room's and are now over.</returns>
    public bool EndRoundScreens(int version)
    {
        lock (gate)
        {
            if (roundScreensAt is null || roundScreensVersion != version)
            {
                return false;
            }

            roundScreensAt = null;
            roundScreensVersion++;
            briefingClockUnits = 0;
            screensClockUnits = 0;
            logger.LogInformation(
                "The screens that followed the round are over; the room may open its next round");
            return true;
        }
    }

    /// <summary>
    /// Clears the round-end screens, because a round opened or the room emptied.
    /// </summary>
    /// <remarks>It is called with <see cref="gate"/> held by the open and the reset.</remarks>
    private void ClearRoundScreens()
    {
        roundScreensAt = null;
        roundScreensVersion++;
        screensClockUnits = 0;
    }

    /// <summary>
    /// Arms the screens that follow a round: the stage's longer hold for its last
    /// round, the round's own for one that has another behind it.
    /// </summary>
    /// <remarks>
    /// The two boundaries. A round inside a stage writes the zero clock and its
    /// successor opens on the screens' own measured pace,
    /// <see cref="MatchScreenTimingsUtils.RoundEndScreensMilliseconds"/>; a report
    /// that lands inside it opens the round at once. A
    /// stage's last round keeps the clock it stopped on and is followed by the
    /// stage result, so its briefing is armed only after
    /// <see cref="MatchScreenTimingsUtils.StageEndMilliseconds"/> and its clock is
    /// written as zero there (<see cref="EndStageEnd"/>).
    /// </remarks>
    /// <param name="closedAt">When the round closed, in Unix milliseconds.</param>
    /// <param name="clockAtClose">Units the played clock stopped at.</param>
    /// <param name="closesGame">Whether the closed round was its stage's last.</param>
    private void ArmScreensEnd(long closedAt, int clockAtClose, bool closesGame)
    {
        // Both boundaries are held on the clock the round stopped on; only the
        // stage end rewinds it, when it writes the zero its next briefing runs on.
        screensClockUnits = clockAtClose;
        if (closesGame)
        {
            roundScreensAt = null;
            roundScreensVersion++;
            stageEndAt = closedAt + MatchScreenTimingsUtils.StageEndMilliseconds;
            stageEndClockUnits = clockAtClose;
            briefingClockUnits = clockAtClose;
            return;
        }

        stageEndAt = null;
        stageEndClockUnits = 0;
        briefingClockUnits = 0;
        roundScreensAt = closedAt + MatchScreenTimingsUtils.RoundEndScreensMilliseconds;
        roundScreensVersion++;
    }
}
