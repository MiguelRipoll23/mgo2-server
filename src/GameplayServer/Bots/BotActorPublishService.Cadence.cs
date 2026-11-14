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
        // **A hit's own beat is forced, not the whole flinch.** `NoteHit` zeroes
        // `NextStateMilliseconds`, so the blow's beat carries a state even though a
        // sleeping or stunned body otherwise keeps the reference's once-a-second
        // standing record: `sleep-by-sgmines` writes 25 states against 983 vitals
        // records for the same 24 seconds.
        var acting = body.TargetSlot != BotBodyState.NoTarget
            || body.HeldBySlot != BotBodyState.NoTarget
            || body.ReloadUntilMilliseconds > now
            || body.RecoverUntilMilliseconds > now;
        if (!acting && now < body.NextStateMilliseconds)
        {
            return false;
        }

        body.NextStateMilliseconds = now + StateIntervalMilliseconds;
        return true;
    }

    /// <summary>
    /// Whether this beat writes a body's vitals, and advances their deadline.
    /// </summary>
    /// <remarks>
    /// <b>A knocked-out body's vitals are continuous.</b> The reference writes the
    /// victim's two-byte vitals as stamina climbs: <c>sleep-by-sgmines</c> writes 983
    /// of them for slot 2 between t+121 and t+146, and <c>stun-by-knife</c> writes
    /// 828 for slot 1 between t+76 and t+93, against one state record a second. This
    /// host wrote one vitals record a second, so the client saw the stun bar move
    /// only once a second while the body was down.
    /// </remarks>
    /// <param name="body">The actor whose vitals are due.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns><c>true</c> when the vitals record rides this beat.</returns>
    private static bool PublishesVitals(BotBodyState body, long now)
    {
        var knockedOut = body.Sleeping || body.StunnedUntilMilliseconds > now;
        if (!knockedOut
            && body.Health == body.LastPublishedHealth
            && now < body.NextVitalsMilliseconds)
        {
            return false;
        }

        body.LastPublishedHealth = body.Health;
        body.NextVitalsMilliseconds = knockedOut ? now : now + VitalsIntervalMilliseconds;
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
