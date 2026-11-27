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
/// stage is released by the room's post-screen reports; in
/// <c>mgo2-started-on-game-round1</c> the first round's clock reaches zero at
/// t+191.599, the client reports at t+215.4 and the next round opens 0.19 s
/// later, 24.0 s after the zero (`docs/analysis/join-stall-after-mode2.md` §66).
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
    }

    /// <summary>
    /// Arms the screens that follow a round: the stage's longer hold for its last
    /// round, the round's own for one that has another behind it.
    /// </summary>
    /// <remarks>
    /// The two boundaries. A round inside a stage writes the zero clock and its
    /// successor opens on the room's post-screen reports; the nominal screen span
    /// is <see cref="MatchScreenTimingsUtils.RoundEndScreensMilliseconds"/>. A
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
