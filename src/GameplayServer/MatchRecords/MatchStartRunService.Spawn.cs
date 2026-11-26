using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The spawn half of the round opener: the report a client sends from the weapon
/// select it is shown, which is what the round's phase and clock wait for.
/// </summary>
/// <remarks>
/// <para>
/// <b>The host answers that report; it does not start the round on a clock of its
/// own.</b> <c>briefing-then-solo-game</c> opens its round at t+146.007, its
/// client reports at t+146.831 and its run lands at t+147.650 carrying the clock
/// whole — <c>01 60 09</c>, 2 400 units. Written at the fixed gap alone, the run
/// reaches a client still choosing a weapon, and the countdown it carries runs
/// over that screen.
/// </para>
/// <para>
/// <b>The wait ends on a report or on the round's own lifetime.</b> A client that
/// never reports is not held out of a round it has already been told about, so
/// the wait gives up and the run is written anyway; a round that closes under the
/// wait writes nothing, because its records are its own.
/// </para>
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// The longest the run waits for the client's report before writing anyway: a
    /// client that stops reporting still gets the round it was shown, and a room
    /// held on a screen that never advances is the stall this replaces.
    /// </summary>
    private const int SpawnReportWaitMilliseconds = 10_000;

    /// <summary>
    /// Join reports the room has heard. A run written for a round waits for this to
    /// move past the value it was opened at.
    /// </summary>
    private int joinReportCount;

    /// <summary>Join reports the room has heard, counted since the process started.</summary>
    public int JoinReportCount
    {
        get
        {
            lock (gate)
            {
                return joinReportCount;
            }
        }
    }

    /// <summary>
    /// Reports heard while the screens that follow a round ran. Those reports are
    /// already the ones the next round opens on, so a spawn wait that only counted
    /// `joinReportCount` waited for a second report the client had already sent.
    /// </summary>
    private int resultScreenReports;

    /// <summary>
    /// Join reports the room has heard, counting the ones heard behind a round's
    /// result screens as well: every report the round that follows them opens on.
    /// </summary>
    public int ReportsHeard
    {
        get
        {
            lock (gate)
            {
                return joinReportCount + resultScreenReports;
            }
        }
    }

    /// <summary>
    /// Waits for the report the round's clock starts on: the one a client sends once
    /// it has left the weapon select, which is the screen that reads "please wait"
    /// until the host answers it with the round's run.
    /// </summary>
    /// <param name="context">Context the run is written through.</param>
    /// <param name="reportsHeard">
    /// <see cref="ReportsHeard"/> as it stood before the round opened: the run waits
    /// for a report behind that count, which is the one the client sends after the
    /// round opened rather than one it already sent behind the result screens.
    /// </param>
    private async Task WaitForSpawnReportAsync(PeerContext context, int reportsHeard)
    {
        var session = context.Session;
        var waited = 0;

        while (MatchRecordPeerUtils.IsAlive(session)
            && roundState.IsOpen
            && ReportsHeard <= reportsHeard
            && waited < SpawnReportWaitMilliseconds)
        {
            await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
            waited += MatchRoundStateService.BriefingRunPollMilliseconds;
        }

        if (ReportsHeard <= reportsHeard)
        {
            logger.LogDebug(
                "UDP {LocalPort}: no report came from {RemoteAddress} {Seconds} s after the round opened; "
                + "writing its run anyway",
                context.LocalPort,
                context.Remote,
                waited / 1000);
        }
    }
}
