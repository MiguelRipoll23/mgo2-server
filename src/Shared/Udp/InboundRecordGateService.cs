using System.Collections.Concurrent;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Udp;

/// <summary>
/// The receive-side gate: a sequenced record is taken only when it carries the byte its stream
/// is waiting for, and a record that carries any other byte is dropped without advancing.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is <c>read_queued_p2p_message</c>, and it is the whole of the order guarantee.</b>
/// The reader walks the queued entries and, for an entry whose state byte has both bit 0 and
/// bit 2 set (<c>(state &amp; 5) == 5</c>, which is what "sequenced" means on the entry), it
/// compares the entry's ordinal — offset <c>10</c> — with the byte the stream holds at
/// <c>descriptor + 0xe</c>. Equal: the byte is advanced by one and the record's body is handed
/// out. Not equal: the record is dropped, the expected byte is <b>not</b> touched, and the walk
/// moves on. There is no buffer for the early record and no negative answer, so a value that
/// arrives out of order is simply gone.
/// </para>
/// <para>
/// <b>An unsequenced record carries no ordinal and is never compared.</b> The entry's state
/// byte is zero for one of those — the queue zeroes the ordinal when the sequenced bit is
/// clear — so <c>(state &amp; 5) == 5</c> is false and the body is handed out whatever the
/// stream is waiting for. That bit is the identifier's own high byte, not a separate flag:
/// <c>0x1001</c> carries <c>0x10</c> and is sequenced, <c>0x0001</c> carries <c>0x00</c> and is
/// not (<see cref="RecordEntryFramingUtils.DeclaresSequenced"/>).
/// </para>
/// <para>
/// <b>The expectation is per container, not per peer.</b> The byte lives in the record
/// descriptor the entry's handle resolves to, so <c>0x1001</c>, <c>0x1250</c> and <c>0xd001</c>
/// each keep their own, and the identifier's low twelve bits is what names it.
/// </para>
/// <para>
/// A stream first seen is seeded from the record that opened it. The image's descriptors are
/// created before any record arrives and start where its sender starts; this host can only
/// observe, so the first record of a container defines the expectation and every later one is
/// measured against it. A gap after that is a gap, exactly as it is in the image: the record
/// that follows it is refused until the missing one is written again.
/// </para>
/// </remarks>
public sealed class InboundRecordGateService
{
    /// <summary>The byte each container is waiting for, per peer.</summary>
    private readonly ConcurrentDictionary<(string Channel, ushort Container), byte> expected = [];

    /// <summary>
    /// Whether one record may be taken, advancing the container when it is.
    /// </summary>
    /// <param name="channel">Peer the record arrived from.</param>
    /// <param name="identifier">Identifier the record travelled under.</param>
    /// <param name="ordinal">Fourth byte the record carries.</param>
    /// <returns><c>true</c> when the record is the one its stream waits for, or when it is unsequenced.</returns>
    public bool TryTake(string channel, ushort identifier, byte ordinal)
    {
        // **An answer has no stream to be measured against.** The image reads a record whose
        // identifier carries the answer class in `decode_p2p_datagram`'s own `0x4000` branch,
        // before anything reaches `P2P_QueueInboundRecord`, and its fourth byte is the byte of
        // the record it answers — a value this host chose, not one the peer counts. Comparing it
        // against a received sequence refused every answer a client sent (`0x5001` at 0x47, 0x48,
        // 0x49 …, 2026-11-16) and left the join waiting on acknowledgements the peer had already
        // written.
        if (((identifier >> 8) & (byte)RecordClassBits.Answer) != 0)
        {
            return true;
        }

        // The class is the identifier's high byte, which is the test the reader makes on the
        // entry's state byte: no sequenced bit, nothing to compare.
        if (!RecordEntryFramingUtils.DeclaresSequenced(identifier))
        {
            return true;
        }

        // **The expectation belongs to a stream, and a stream is the identifier's low twelve
        // bits.** The working capture names its own: `tools/udp_ordinal_rules.py` reports the
        // streams of `pcapng1` as `0x000`, `0x001`, `0x250`, `0x254`, `0x2de`, `0xa54` …, and a
        // live client writes one counter across every identifier that shares those bits. Its
        // profile arrives as `0x1001` at `0x00` with its `0x9001` polls at `0x01 0x02 0x03 0x04`
        // behind it; `0x12de` and `0x92de` run `0x00`–`0x06` together; `0x1254` runs `0x00`–`0x05`
        // (2026-11-16). Keyed by the class nibble instead — `(id >> 12) & 0xfff`, which is what
        // this did — `0x1001`, `0x1254` and `0x12de` collapse into one expectation and every
        // record behind the first is refused: that client's report `0x12de 0x00` was dropped on
        // arrival and re-sent once a second for the rest of the session, which is a join that
        // never leaves its briefing.
        var container = (ushort)(identifier & 0x0fff);
        var key = (channel, container);
        while (true)
        {
            if (!expected.TryGetValue(key, out var waiting))
            {
                // The record that opens a stream defines what that stream is waiting for next.
                if (expected.TryAdd(key, (byte)(ordinal + 1)))
                {
                    return true;
                }

                continue;
            }

            if (waiting != ordinal)
            {
                return false;
            }

            if (expected.TryUpdate(key, (byte)(ordinal + 1), waiting))
            {
                return true;
            }
        }
    }

    /// <summary>Drops a peer's expectations, which is what a departure leaves behind.</summary>
    /// <param name="channel">Peer to forget.</param>
    public void Forget(string channel)
    {
        foreach (var key in expected.Keys.Where(key => key.Channel == channel).ToList())
        {
            expected.TryRemove(key, out _);
        }
    }
}
