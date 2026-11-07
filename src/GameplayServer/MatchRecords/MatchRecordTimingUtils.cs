namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The two gaps a round is opened across, as `briefing-then-solo-game` writes
/// them: its briefing's zero, the round that follows it, and the phase and run
/// the round is opened with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both are waited out, not merely written in order.</b> Written in the
/// zero's own tick the open took the live client from its briefing fade straight
/// into the map, and it crashed in its camera code (`cam_transparent.cc line
/// 455: !( ctsCT() == NULL )`, an access violation at `system.dll+0x108D82`)
/// thirty seconds later. The capture's own client spends the first gap fading
/// out and the second loading the map, which is what the two waits leave it.
/// </para>
/// <para>
/// The measurements are the capture's, and only a room of one is served by them:
/// a room of several opens on the report after its zero (`MatchStartRunService`),
/// and the run it is opened with follows behind that open.
/// </para>
/// </remarks>
public static class MatchRecordTimingUtils
{
    /// <summary>
    /// The gap a room of one leaves between its briefing's closing zero and the
    /// round that follows it: the zero at t+145.098, the round at t+146.007.
    /// </summary>
    public const int RoundOpenDwellMilliseconds = 900;

}
