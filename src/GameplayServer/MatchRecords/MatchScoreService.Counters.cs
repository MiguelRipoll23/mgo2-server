namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The objective and knock-out counters a player's score reads: what the host
/// counts from the records the room actually writes.
/// </summary>
/// <remarks>
/// <para>
/// <b>The counters are the client's table's, and each one is an event this host
/// saw.</b> A knock-out is an applied stun-family damage record
/// (<c>DamageRecordUtils.KindOf</c>); a hold-up is a CQC hold
/// (<c>CqcHoldUtils.HeldSlotOf</c>); a dog tag is the hold-up on a
/// knocked-out enemy in Sneaking, because a body search itself writes no record
/// this channel carries. The coefficients and the clamping live in
/// <see cref="MatchScoreCounterUtils"/>.
/// </para>
/// <para>
/// <b>A hold is a record per beat, not an event.</b> The grabber's client writes
/// its hold every beat for as long as it holds, so the same pair would count
/// once per beat without a window. The window is the hold's own measured length
/// with room for a dropped record; a second hold on the same body after it is a
/// second hold.
/// </para>
/// </remarks>
public sealed partial class MatchScoreService
{
    /// <summary>How long one knock-out on the same victim is the same blow.</summary>
    /// <remarks>
    /// A damage record is applied once, but a client can repeat one across beats
    /// while it is being acknowledged. The window is the client's own hit cadence,
    /// not a guess at the stun's length: two darts a second apart are two
    /// knock-outs.
    /// </remarks>
    private const int KnockoutDedupMilliseconds = 500;

    /// <summary>How long one hold-up, dog tag or checkpoint on the same actor is the same event.</summary>
    private const int ObjectiveDedupMilliseconds = 5_000;

    /// <summary>The objective a player has just performed.</summary>
    private enum ObjectiveKind
    {
        /// <summary>A CQC hold-up.</summary>
        HoldUp,

        /// <summary>A dog tag collected from a held enemy.</summary>
        DogTag,

        /// <summary>A checkpoint passed by a target carrier.</summary>
        Checkpoint,
    }

    private readonly Dictionary<(byte Attacker, byte Victim), long> recentKnockouts = [];
    private readonly Dictionary<(byte Slot, byte Victim, ObjectiveKind Kind), long> recentObjectives = [];

    /// <summary>
    /// Records a knock-out the host applied: the attacker's dealt count and the
    /// victim's received count, and the score each one changes.
    /// </summary>
    /// <remarks>
    /// <b>The received count is the stun deduction the score table had no term
    /// for.</b> The client's table pays knock-outs dealt and charges knock-outs
    /// received — <c>−2</c> in DM, <c>−1</c> in TDM and Sneaking, zero elsewhere
    /// (<c>docs/PROTOCOL.md</c>, "SETTLED 2026-07-27") — so both sides of one blow
    /// move. A blow a slot lands on itself is not a knock-out anybody is credited
    /// for and is refused here.
    /// </remarks>
    /// <param name="attackerSlot">Slot the reporting peer holds, or zero when it names none.</param>
    /// <param name="victimSlot">Slot of the knocked-out player or bot.</param>
    /// <returns><c>true</c> when this blow was the first report of the knock-out.</returns>
    public bool NoteKnockout(byte attackerSlot, byte victimSlot)
    {
        if (attackerSlot == 0 || attackerSlot == victimSlot)
        {
            return false;
        }

        lock (gate)
        {
            var now = Environment.TickCount64;
            if (recentKnockouts.TryGetValue((attackerSlot, victimSlot), out var knockedAt)
                && now - knockedAt < KnockoutDedupMilliseconds)
            {
                return false;
            }

            recentKnockouts[(attackerSlot, victimSlot)] = now;
            var attacker = RowOf(attackerSlot);
            var victim = RowOf(victimSlot);
            attacker.KnockoutsDealt++;
            victim.KnockoutsReceived++;
            QueueScore(attackerSlot, attacker);
            QueueScore(victimSlot, victim);
            logger.LogDebug(
                "Player in slot {AttackerSlot} knocked out the player in slot {VictimSlot}; "
                + "{Dealt} knock-out(s) dealt, {Received} received",
                attackerSlot,
                victimSlot,
                attacker.KnockoutsDealt,
                victim.KnockoutsReceived);
            return true;
        }
    }

    /// <summary>
    /// Records a CQC hold-up, which is the hold's first record and not every beat
    /// of it.
    /// </summary>
    /// <param name="holderSlot">Slot of the player holding.</param>
    /// <param name="heldSlot">Slot of the player or bot held.</param>
    /// <returns><c>true</c> when this hold was not already counted.</returns>
    public bool NoteHoldUp(byte holderSlot, byte heldSlot) =>
        NoteObjective(holderSlot, heldSlot, ObjectiveKind.HoldUp);

    /// <summary>
    /// Records a dog tag collected from a held enemy.
    /// </summary>
    /// <remarks>
    /// <b>A body search is the counter's event and it writes no record this host
    /// reads.</b> The mode's own rule is to hold up or take down a character and
    /// collect a tag, so the hold-up on a knocked-out enemy is the moment the host
    /// can see; a tag from a body that was searched without a hold is not counted
    /// rather than guessed at. The client's table pays body searches and tags at
    /// different rates, so both counters move: the search found the body, and the
    /// tag is what it yielded.
    /// </remarks>
    /// <param name="collectorSlot">Slot of the player collecting.</param>
    /// <param name="victimSlot">Slot of the enemy the tag came from.</param>
    /// <returns><c>true</c> when this tag was not already counted.</returns>
    public bool NoteDogTag(byte collectorSlot, byte victimSlot) =>
        NoteObjective(collectorSlot, victimSlot, ObjectiveKind.DogTag);

    /// <summary>
    /// Records a checkpoint a target carrier reached.
    /// </summary>
    /// <remarks>
    /// <b>The delivery is the objective feed's.</b> A checkpoint rule's delivery
    /// is the operations record <c>04 &lt;kind&gt; &lt;slot&gt;</c>, read by
    /// <see cref="ObjectiveFeedBodiesUtils"/>: kind <c>0x02</c> is a target put
    /// in a Capture goal and kind <c>0x12</c> is a Race goal, and the slot is the
    /// carrier. The counter is column 27 of the rule's row (B46
    /// <c>capture_put_count</c>, which CAP pays at 1), and SCAP scores its
    /// checkpoints at the same one point.
    /// </remarks>
    /// <param name="carrierSlot">Slot of the player carrying the target.</param>
    /// <param name="checkpointSlot">The checkpoint's identifier, or zero when it has none.</param>
    /// <returns><c>true</c> when this checkpoint was not already counted.</returns>
    public bool NoteCheckpoint(byte carrierSlot, byte checkpointSlot = 0) =>
        NoteObjective(carrierSlot, checkpointSlot, ObjectiveKind.Checkpoint);

    /// <summary>The dog tags a player has collected this round.</summary>
    /// <param name="slot">Key in the room's 24-entry player table.</param>
    /// <returns>The count, or zero when the slot has none.</returns>
    public int DogTags(byte slot)
    {
        lock (gate)
        {
            return rows.TryGetValue(slot, out var row) ? row.DogTags : 0;
        }
    }

    /// <summary>The checkpoints a player has passed this round.</summary>
    /// <param name="slot">Key in the room's 24-entry player table.</param>
    /// <returns>The count, or zero when the slot has none.</returns>
    public int Checkpoints(byte slot)
    {
        lock (gate)
        {
            return rows.TryGetValue(slot, out var row) ? row.Checkpoints : 0;
        }
    }

    /// <summary>
    /// Counts one objective event once per actor and window, and queues the score
    /// it changes.
    /// </summary>
    /// <param name="slot">Slot the event is credited to.</param>
    /// <param name="victimSlot">Slot or object the event names, for the dedup key.</param>
    /// <param name="kind">Which counter to move.</param>
    /// <returns><c>true</c> when the event was not already counted.</returns>
    private bool NoteObjective(byte slot, byte victimSlot, ObjectiveKind kind)
    {
        if (slot == 0)
        {
            return false;
        }

        lock (gate)
        {
            var now = Environment.TickCount64;
            var key = (slot, victimSlot, kind);
            if (recentObjectives.TryGetValue(key, out var countedAt)
                && now - countedAt < ObjectiveDedupMilliseconds)
            {
                return false;
            }

            recentObjectives[key] = now;
            var row = RowOf(slot);
            switch (kind)
            {
                case ObjectiveKind.HoldUp:
                    row.HoldUps++;
                    break;
                case ObjectiveKind.DogTag:
                    row.BodySearches++;
                    row.DogTags++;
                    break;
                case ObjectiveKind.Checkpoint:
                    row.Checkpoints++;
                    break;
            }

            QueueScore(slot, row);
            logger.LogDebug(
                "Player in slot {Slot} recorded {Kind} against slot {VictimSlot}; {HoldUps} hold-up(s), "
                + "{DogTags} dog tag(s), {Checkpoints} checkpoint(s), score {Score}",
                slot,
                kind,
                victimSlot,
                row.HoldUps,
                row.DogTags,
                row.Checkpoints,
                row.Score);
            return true;
        }
    }
}
