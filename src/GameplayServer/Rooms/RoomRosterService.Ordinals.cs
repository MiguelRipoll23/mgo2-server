namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The roster exchange's counter: the fourth byte each of its records takes, and
/// the seeding a room does when a join has just spent the values a run carries.
/// </summary>
/// <remarks>
/// Split from <see cref="RoomRosterService"/> because the counter is the room's own
/// stream bookkeeping rather than who is in it, and because both files then stay
/// inside the size a source file is allowed.
/// </remarks>
public sealed partial class RoomRosterService
{
    /// <summary>
    /// Takes the fourth byte the next record of the roster exchange carries, and
    /// advances the exchange's counter past it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counter is one per identifier and every record under it consumes a
    /// value, which is why there is a take and no way to read it without taking
    /// one: a caller that pinned a value would write one a record before it has
    /// already taken it. The counter is the room's and not this file's — the same
    /// sequence numbers the <c>0xd001</c> controls the sparse phase and the beat
    /// insert, which the captures write between the exchange's own records
    /// (<see cref="MatchRecords.MatchRecordSequenceService"/>). Measured on
    /// `mgo2-game-joined-when-red-team-then-briefing`, whose live rounds carry
    /// every record of the roster identifier (<c>0x1001</c>) in one ascending
    /// sequence — the lobby-slot arrays take two consecutive values (23 and 24,
    /// then 28 and 29) and the scoreboard deltas take the values between them (25,
    /// 26 and 27).
    /// </para>
    /// <para>
    /// The run numbers its positions from zero: the head and the host's own entry
    /// both read zero, the joining players read one upwards, and the close reads
    /// <see cref="JoinerCount"/> + 1. What follows the run keeps counting rather
    /// than restarting — the burst the recorded host sends 2.52 s later reads 3,
    /// 4, 5 and 6 under a one-player roster, which is why the counter is seeded at
    /// <see cref="JoinerCount"/> + 2 when a member registers
    /// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4) — so a sender that restarts
    /// the count writes numbers the peer has already seen.
    /// </para>
    /// <para>
    /// A record that carries an index byte takes one; a record that does not — an
    /// opening control byte, the head — takes none, which is why this is called
    /// per record rather than once per frame.
    /// </para>
    /// </remarks>
    /// <returns>The fourth byte the record takes.</returns>
    public byte TakeRunOrdinal() => sequences.Take(ExchangeIdentifier);

    /// <summary>
    /// Opens the exchange's counter at a value, for a room whose join has just
    /// taken the values it carries. A stream already counting is left alone
    /// (<see cref="MatchRecords.MatchRecordSequenceService.Seed"/>); an empty
    /// room restarts it before the next join.
    /// </summary>
    /// <param name="value">Value the exchange's next record should take.</param>
    public void SeedRunOrdinal(byte value) => sequences.Seed(ExchangeIdentifier, value);

    /// <summary>
    /// Takes the next values of the roster exchange's counter, in order, for a
    /// caller that writes several records under it.
    /// </summary>
    /// <remarks>
    /// The lobby-slot pair is written this way: the replay parser tells a pair by
    /// its two records being consecutive, and consecutive values are exactly what
    /// the counter hands out (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2).
    /// </remarks>
    /// <param name="count">How many records the caller is about to write.</param>
    /// <returns>The values, in the order the records take them.</returns>
    public byte[] TakeRunOrdinals(int count) => sequences.Take(ExchangeIdentifier, count);
}
