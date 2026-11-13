using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;    /// <summary>
    /// The two orderings a beat's records obey: how often an actor's state is written,
    /// and where a death's feed sits inside its victim's turn.
    /// </summary>
/// <remarks>
/// <b>Both are measured against `.pcapngs/actions/actions.pcapng` and both differ
/// from a per-beat writer.</b> The reference writes an idle actor's standing state
/// once a second and an acting one's on every beat; it writes a death's vitals
/// zero, then the feed, then the victim's block. This host wrote the state on every
/// beat and put the feed in front of the whole block, and the two are what the
/// client's actor table and its kill log are built from.
/// </remarks>
public sealed partial class BotActorPublishService
{
    /// <summary>
    /// Interval between an idle actor's standing state records: the reference's
    /// cadence for a body doing nothing.
    /// </summary>
    private const int StateIntervalMilliseconds = 1_000;

    /// <summary>
    /// Whether this beat writes the actor's state record, advancing the deadline it
    /// stores.
    /// </summary>
    /// <remarks>
    /// <b>Measured.</b> `actions.pcapng` writes the three-byte `00 ff 00` 7,974
    /// times against 39,693 acting records across 592 s — about one a second per
    /// idle actor — where this host wrote the standing form on every beat (16,770
    /// against 11,947). An actor with a target, or one a client is holding, keeps
    /// the per-beat cadence; everything else waits for its second.
    /// </remarks>
    /// <param name="body">The actor whose state is due.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns><c>true</c> when the state record rides this beat.</returns>
    private static bool PublishesState(BotBodyState body, long now)
    {
        // **A body taking a hit is acting, and the gate must say so.** The flinch is
        // the `c1 81` head the position record carries for the 200 ms after a blow
        // (`BotLocomotionService.NoteHit`), and a bot standing idle holds no target —
        // so gating its records to one a second left the blow written on the beat
        // after the head had already expired, which is a bot that takes the damage and
        // never flinches. `myserver.pcapng` of 2026-11-12 is that report: health drops
        // and nothing on the body moves.
        var acting = body.TargetSlot != BotBodyState.NoTarget
            || body.HeldBySlot != BotBodyState.NoTarget
            || body.Sleeping
            || body.HitReacting;
        if (!acting && now < body.NextStateMilliseconds)
        {
            return false;
        }

        body.NextStateMilliseconds = now + StateIntervalMilliseconds;
        return true;
    }

    /// <summary>
    /// Writes a death's records in the reference's order inside its victim's turn:
    /// the vitals zero, the feed, and then the victim's block.
    /// </summary>
    /// <remarks>
    /// `.pcapngs/actions/actions.pcapng` at t+43.29 writes the vitals zero at record
    /// 8, `09 16 04` at 10 and `00 ff 00` at 11; at t+49.71 the zero at 13,
    /// `09 08 07` at 15 and the state at 16. This host wrote the feed in front of the
    /// whole block, so the client handled a kill feed for a death it had not been
    /// told about yet, and the log entry that names the killer is only built while
    /// the victim is still standing.
    /// </remarks>
    /// <param name="records">The beat's records, appended to.</param>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="body">The victim's body.</param>
    /// <param name="feed">The feed due for this victim, or a default when none is.</param>
    /// <param name="placed">Victims this beat has already written a turn for.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns>
    /// <c>true</c> when the zero above is this beat's vitals record and the block
    /// must not repeat it, which is what the reference does.
    /// </returns>
    private static bool WriteDeathTurn(
        List<UdpMessage> records,
        MatchRecordGenerationUtils generation,
        BotBodyState body,
        (byte Victim, UdpMessage Record) feed,
        HashSet<byte> placed,
        long now)
    {
        if (feed.Record is null)
        {
            return false;
        }

        records.Add(new UdpMessage(
            (ushort)(BotActorRecordUtils.StateIdentifierOf(generation, body.SessionSlot) + 1),
            3,
            BotActorRecordUtils.VitalsAttributeClass,
            [
                (byte)Math.Clamp((int)body.Health, 0, (int)BotBodyState.MaximumVitals),
                (byte)body.Stamina,
            ]));
        records.Add(feed.Record);
        placed.Add(body.SessionSlot);

        body.LastPublishedHealth = body.Health;
        body.NextVitalsMilliseconds = now + VitalsIntervalMilliseconds;
        return true;
    }
}
