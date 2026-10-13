using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The fourth byte of a session record: one counter per twelve-bit identifier,
/// handed out to the writers as the records are written.
/// </summary>
/// <remarks>
/// <para>
/// <b>The counter belongs to the identifier's low twelve bits, and the class bits
/// do not get counters of their own.</b> Measured on <c>mgo2-game1</c>'s host flow
/// with <c>tools/udp_session_records.py</c>: the records one frame writes under
/// <c>0x9001</c> and <c>0x1001</c> take consecutive values — <c>0x9001</c> takes
/// <c>0x07</c> and the four <c>0x1001</c> records take <c>0x08 09 0a 0b</c> at
/// t+44.747, and a frame at t+5.019 takes <c>0xd001</c> <c>01</c>, <c>0x9001</c>
/// <c>03</c> and three <c>0x1001</c> <c>04 05 06</c> — and over the session the
/// id-<c>1</c> family (<c>0x1001</c>, <c>0x5001</c>, <c>0x9001</c>, <c>0xd001</c>)
/// writes 576 records whose fourth bytes take 193 distinct values spanning
/// <c>00</c>–<c>c9</c> with nine holes in <c>01</c>–<c>c9</c> and 475 of 575
/// ascending steps. Four private counters would number each stream <c>00 01 02 …</c>
/// and leave the other three's values unwritten
/// (<c>docs/analysis/replay-findings.md</c> §5.7).
/// </para>
/// <para>
/// <b>A record's fourth byte is a sequence byte only where the identifier is a
/// session record.</b> A tick record — an identifier below <c>0x1000</c>, the
/// state blob <c>0x0002</c>, the vitals <c>0x090c</c>, the rules pair
/// <c>0x0a61</c> — carries its attribute class in that byte and takes no value
/// here; a caller that stamped one would be writing a class the client reads.
/// The countdown and the readiness publication read the same rule from the other
/// side, which is why neither takes a counter: they are the operations clock and
/// the payload record, and their fourth bytes are a clock and a per-player place.
/// </para>
/// <para>
/// <b>What is keyed, and for how long.</b> A stream is keyed by the identifier's
/// low twelve bits. The room's own exchange (id <c>0x001</c>) counts for the
/// room's whole life: the join's records, its burst, the round's arrays and the
/// controls the beat and the sparse phase insert are one ascending sequence in
/// every capture that holds them. A round's operations stream is keyed by its own
/// generation's id and is restarted where the round opens, because the recorded
/// host's run opens the operations stream on <c>00 01 02</c> and its beat's clocks
/// go on from <c>03</c> (game1's round-1 run and beat, in
/// <c>docs/analysis/replay-findings.md</c> §5.7).
/// </para>
/// <para>
/// The two generations sit a <c>0x800</c> bit apart, and that bit is <em>inside</em>
/// the twelve keyed bits: <c>0x1a54</c> is id <c>0xa54</c> and <c>0x1254</c> is id
/// <c>0x254</c>. So a room that alternates its rounds between the two sets numbers
/// each set from where that set was left, which is what the captures show and is
/// why the restart at a round's open is a restart of one generation's stream and
/// not of both.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class MatchRecordSequenceService(ILogger<MatchRecordSequenceService> logger)
{
    /// <summary>
    /// Bits of an identifier that key its stream: the low twelve. The class bits
    /// (<c>0x1000</c>, <c>0x4000</c>, <c>0x8000</c>) are above them.
    /// </summary>
    public const ushort IdentifierMask = 0x0FFF;

    private readonly Lock gate = new();
    private readonly Dictionary<ushort, byte> nextOrdinals = [];

    /// <summary>
    /// The stream an identifier's records are numbered on: its low twelve bits.
    /// </summary>
    /// <param name="identifier">Identifier a record travels under.</param>
    /// <returns>The key its counter is held under.</returns>
    public static ushort StreamOf(ushort identifier) => (ushort)(identifier & IdentifierMask);

    /// <summary>
    /// Takes the fourth byte the next record of an identifier's stream carries,
    /// and advances the stream past it.
    /// </summary>
    /// <remarks>
    /// There is a take and no way to read a value without taking one, as in
    /// <see cref="Rooms.RoomRosterService"/>: a caller that read the counter and
    /// then wrote would write a value the record before it has already taken.
    /// </remarks>
    /// <param name="identifier">Identifier the record travels under.</param>
    /// <returns>The fourth byte the record takes.</returns>
    public byte Take(ushort identifier)
    {
        var stream = StreamOf(identifier);
        lock (gate)
        {
            nextOrdinals.TryGetValue(stream, out var value);
            nextOrdinals[stream] = unchecked((byte)(value + 1));
            return value;
        }
    }

    /// <summary>
    /// Takes the next values of an identifier's stream, in order, for a caller
    /// about to write several records under it.
    /// </summary>
    /// <param name="identifier">Identifier the records travel under.</param>
    /// <param name="count">How many records the caller is about to write.</param>
    /// <returns>The values, in the order the records take them.</returns>
    public byte[] Take(ushort identifier, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var values = new byte[count];
        for (var index = 0; index < count; index++)
        {
            values[index] = Take(identifier);
        }

        return values;
    }

    /// <summary>
    /// Restarts a stream: its next record takes zero.
    /// </summary>
    /// <remarks>
    /// Called where the recorded host opens a stream of its own rather than
    /// continuing one — a round opening its operations stream on the values its
    /// run takes, and a briefing opening the clock its peers are sent. The room's
    /// exchange is never restarted: it is one sequence from the join on.
    /// </remarks>
    /// <param name="identifier">Identifier whose stream restarts.</param>
    public void Restart(ushort identifier)
    {
        var stream = StreamOf(identifier);
        lock (gate)
        {
            nextOrdinals.Remove(stream);
        }

        logger.LogDebug(
            "The 0x{Stream:x3} record stream restarts; its next record takes 0x00",
            stream);
    }

    /// <summary>
    /// Opens a stream at a value, when nothing has taken from it yet.
    /// </summary>
    /// <remarks>
    /// <b>A stream already in use keeps counting.</b> The roster opens the room's
    /// exchange at the value the join's own records leave behind
    /// (<see cref="Rooms.RoomRosterService.Register"/>), which is a seed of a
    /// stream nothing has written yet. A second player's registration seeds the
    /// same stream again, and there the seed must not take the counter backwards:
    /// by then the join's run, the burst and possibly a round's controls have taken
    /// values from it, and a counter moved back would hand a peer a number its
    /// stream has already carried.
    /// </remarks>
    /// <param name="identifier">Identifier whose stream is opened.</param>
    /// <param name="value">Value its next record should take.</param>
    public void Seed(ushort identifier, byte value)
    {
        var stream = StreamOf(identifier);
        lock (gate)
        {
            if (nextOrdinals.ContainsKey(stream))
            {
                logger.LogDebug(
                    "The 0x{Stream:x3} record stream is already at 0x{Value:x2}; the seed of 0x{Seed:x2} "
                    + "is left alone so no value is handed out twice",
                    stream,
                    nextOrdinals[stream],
                    value);
                return;
            }

            nextOrdinals[stream] = value;
        }
    }
}
