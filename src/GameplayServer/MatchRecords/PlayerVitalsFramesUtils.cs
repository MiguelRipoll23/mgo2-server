namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The beat frame's vitals record: the identifier it travels under, per player.
/// </summary>
/// <remarks>
/// <para>
/// <b>The identifier is the player's own key in the room's 24-entry player
/// table, and not a constant of the record.</b> Every capture writes the beat's
/// vitals under a value ten apart from the next slot's, and the value the slot
/// holds matches the slot that capture's own arrays name:
/// </para>
/// <list type="bullet">
/// <item>
/// <c>mgo2-game1</c> names slot <c>0x14</c> in its run and writes <c>0x090c</c>
/// as its beat's vitals — and that is the identifier of slot 20.
/// </item>
/// <item>
/// <c>mgo2-game-started-on-briefing</c> names slots 1 to 6 — its occupancy
/// array marks <c>07</c> at six consecutive bytes — and writes <c>0x084e</c>,
/// <c>0x0858</c>, <c>0x0862</c>, <c>0x086c</c>, <c>0x0876</c> and <c>0x0880</c>:
/// the six slots' own identifiers, one per player.
/// </item>
/// <item>
/// <c>mgo2-game-joined-when-red-team-then-briefing</c> names slots 1 to 14 and
/// writes all fourteen values, <c>0x084e</c> to <c>0x08d0</c>.
/// </item>
/// <item>
/// <c>mgo2-started-briefing-then-solo-game</c> names slot 3 on its pair
/// (<c>85 7f fe ff ff 01 …</c>) and writes <c>0x0862</c>, slot 3's value, into
/// its beat.
/// </item>
/// </list>
/// <para>
/// <b>A live room is the same rule from the other side.</b>
/// <c>myserver4.pcapng</c> (2026-10-15) joins a client whose own join state record
/// carries slot <c>01</c>, whose occupancy array marks index 1, and whose run
/// payload echoes <c>01</c>; the beat this host wrote that client carried the
/// recorded <c>0x090c</c> — slot 20, a player the room does not hold
/// (<c>docs/analysis/join-stall-after-mode2.md</c> §7).
/// </para>
/// <para>
/// So <see cref="MatchRecordFramesUtils.VitalsRecordIdentifier"/> is not a
/// constant of the beat but the recorded room's own slot read out: a host that
/// replayed it into a room whose players hold other slots wrote every one of
/// them as slot 20.
/// </para>
/// <para>
/// The player's own entity records sit one below it —
/// <c>mgo2-started-briefing-then-solo-game</c>'s client writes <c>0861</c>,
/// <c>0863</c>, <c>0865</c> and <c>0867</c> for the player whose vitals its host
/// writes as <c>0862</c>, and <c>mgo2-game-started-on-briefing</c>'s is the same
/// group under <c>084d</c>/<c>084f</c>/<c>0851</c>/<c>0853</c> for slot 1 — which
/// is why the identifier is read here from the slot rather than derived from the
/// vitals record's body.
/// </para>
/// </remarks>
public static class PlayerVitalsFramesUtils
{
    /// <summary>
    /// Identifier the vitals record of the room's own slot 0 carries. No captured
    /// room writes slot 0's player, so the base is the one the measured slots
    /// imply rather than a measured record.
    /// </summary>
    public const ushort SlotZeroIdentifier = 0x0844;

    /// <summary>
    /// Steps between one slot's vitals identifier and the next: <c>0x0a</c>,
    /// measured over slots 1…4 of `mgo2-game-started-on-briefing` and slot 20 of
    /// `mgo2-game1`.
    /// </summary>
    public const int SlotStride = 0x0a;

    /// <summary>
    /// Identifier of one player's vitals record, from the key it holds in the
    /// room's 24-entry player table.
    /// </summary>
    /// <param name="slot">The player's key in the room's player table.</param>
    /// <returns>The identifier its vitals record travels under in a first round.</returns>
    public static ushort IdentifierFor(byte slot) =>
        (ushort)(SlotZeroIdentifier + (SlotStride * slot));

    /// <summary>
    /// Identifier of one player's vitals record in the round an identifier set
    /// plays.
    /// </summary>
    /// <remarks>
    /// <b>The round flips the slot's own value's top bit, exactly as it flips the
    /// recorded identifier.</b> `mgo2-game1` writes slot 20's vitals as
    /// <c>0x090c</c> in its first round and <c>0x010c</c> in its second — the same
    /// value with bit <see cref="MatchRecordGenerationUtils.SiblingBit"/> cleared,
    /// which is what the set's own <see cref="MatchRecordGenerationUtils.Vitals"/>
    /// does between those rounds. So the round is read off the set rather than
    /// assumed: the set's recorded value and the first round's differ by the
    /// sibling bit or they do not, and the slot's value is written the same way.
    /// </remarks>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="slot">The player's key in the room's player table.</param>
    /// <returns>The identifier its vitals record travels under.</returns>
    public static ushort IdentifierFor(MatchRecordGenerationUtils generation, byte slot) =>
        (ushort)(IdentifierFor(slot) ^ SiblingBitOf(generation));

    /// <summary>
    /// The sibling bit a round's identifier set carries, read from the set's own
    /// recorded vitals value against the first round's.
    /// </summary>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <returns>
    /// <see cref="MatchRecordGenerationUtils.SiblingBit"/> for a sibling round,
    /// and zero for a first round.
    /// </returns>
    private static ushort SiblingBitOf(MatchRecordGenerationUtils generation) =>
        (ushort)((generation.Vitals ^ MatchRecordGenerationUtils.First.Vitals)
            & MatchRecordGenerationUtils.SiblingBit);
}
