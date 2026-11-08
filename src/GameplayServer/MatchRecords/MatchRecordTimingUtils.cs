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

    /// <summary>
    /// The gap between a round's open and the run behind it: the state write at
    /// t+146.007 and the run at t+147.650 in <c>briefing-then-solo-game</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is counted from the open, and the client's own report sits inside
    /// it.</b> That capture's client reports ready at t+146.831 — 0.82 s into the
    /// gap — and the run is the 0.82 s after it. A host that waits for the report
    /// and then spends the whole gap pays it twice and opens every round 0.82 s
    /// late, with the opening state write late by the same. The state write is
    /// what the gap is measured from, so it is written at the open, not behind
    /// the report (<c>MatchStartRunService.Runs</c>).
    /// </para>
    /// <para>
    /// <b>They are not one pass.</b> Every capture of an open writes the operations
    /// stream's state write before its run and reads them as the round's first two
    /// instants — `working-server-log` writes one of its own at t+530.066 — and the
    /// client uses the distance to load the map the run's clock is for. Written in
    /// one pass the run's clock reaches the client before the state it belongs to,
    /// which is the order the capture never has
    /// (<c>docs/protocol/BRIEFING_FLOW.md</c> §2).
    /// </para>
    /// </remarks>
    public const int RoundStartRunGapMilliseconds = 1643;

}
