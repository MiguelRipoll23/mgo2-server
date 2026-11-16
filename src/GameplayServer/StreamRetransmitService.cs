using System.Runtime.CompilerServices;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// Holds the numbered records a peer has not answered yet, and writes them again when
/// an answer shows the peer refused one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The peer takes a stream's records by exact byte and discards what it is not
/// expecting, so a value it never took is a hole and the stream stops at it.</b> The
/// reader is <c>read_queued_p2p_message</c>: it compares the record's fourth byte with
/// the byte the stream holds, and on a mismatch drops the record without advancing.
/// Nothing in the peer buffers or asks again, so the recovery is the sender's, and the
/// working host does it — <c>.pcapngs/pcapng1</c>'s stream <c>0x001</c> writes 1 319
/// records for 256 distinct values, and <c>pcapng2</c> 1 536 for 256. Those captures are
/// mostly re-sends.
/// </para>
/// <para>
/// <b>An answer is cumulative, and that is what makes a window enough.</b> A record is
/// accepted only when it is the one the stream is waiting for, so an answer naming byte
/// <c>W</c> proves that every byte before <c>W</c> on that stream was taken. The window
/// is therefore freed up to the answered byte rather than by the one record the answer
/// names, and whatever is left behind it is exactly the set the peer refused.
/// </para>
/// <para>
/// <b>A hole is written again from its oldest record, in order.</b> Re-sending only the
/// missing byte does not help: the records after it were refused too, so they are
/// written again behind it, which is the Go-Back-N shape a channel with no
/// out-of-order buffer needs. The window also re-sends on age, for the record whose
/// answer never comes at all — the peer's silence cannot otherwise be told from a lost
/// datagram.
/// </para>
/// <para>
/// A byte-wide sequence space makes all of this load-bearing: one hole that is never
/// filled costs a full wrap of 256 values before the stream moves again, which at a
/// round's rate is minutes (<c>docs/analysis/stream-sequencing.md</c>).
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
/// <param name="pacing">The age at which each peer's records are written again.</param>
public sealed class StreamRetransmitService(
    ILogger<StreamRetransmitService> logger,
    RetransmitPacingService pacing)
{
    /// <summary>
    /// Records held per stream, which is the size of the sender's own record table: the
    /// image walks twenty-four slots (`0x18`) per tick and its send guard is `0x17`, the
    /// last index of that table. A byte repeats only every 256 values, so a window this
    /// far under that holds no duplicate and an answer names at most one entry.
    /// </summary>
    private const int WindowLimit = 24;

    /// <summary>
    /// Writes one record may spend before its stream is abandoned: the image's own guard,
    /// <c>0xff</c>, and not a number of this host's choosing.
    /// </summary>
    /// <remarks>
    /// <b>The image keeps two bytes per record handle and refuses on their difference.</b>
    /// <c>P2P_SendRecord</c> (<c>0x26b778</c>) reads the writes at handle <c>+0xb</c> and the
    /// acknowledged base at handle <c>+0xc</c>; when <c>writes − acked == 0xff</c> it calls the
    /// channel's abort vector and returns without queueing the record, and otherwise it
    /// increments the writes and hands the record to <c>gate_p2p_record_send</c>
    /// (<c>0x263530</c>), which clears the entry's own retransmit byte, refuses while the
    /// channel is not connected, and appends — aborting the channel if the datagram is full.
    /// </remarks>
    /// <remarks>
    /// That difference is <see cref="Pending.Attempts"/> here: an entry leaves the window when
    /// the peer answers it (<see cref="NoteAnswered"/>), so the count on the oldest held record
    /// is the writes since the base. The guard is therefore checked on the oldest record before
    /// a write and the stream is dropped when it fires, rather than a peer drawing copies of a
    /// stream it stopped reading for the rest of the round.
    /// </remarks>
    private const int AttemptLimit = 0xff;



    /// <summary>
    /// One record of a stream's window: the record as written, so a re-send repeats the
    /// body and the byte and not just the number.
    /// </summary>
    /// <param name="type">Identifier the record travels under.</param>
    /// <param name="ordinal">Fourth byte it was given when it was first written.</param>
    /// <param name="body">Body it carries.</param>
    /// <param name="writtenAt">Instant of its last write, in milliseconds since boot.</param>
    private sealed class Pending(ushort type, byte ordinal, byte[] body, long writtenAt)
    {
        /// <summary>Identifier the record travels under.</summary>
        public ushort Type { get; } = type;

        /// <summary>Fourth byte it was given when it was first written.</summary>
        public byte Ordinal { get; } = ordinal;

        /// <summary>Body it carries.</summary>
        public byte[] Body { get; } = body;

        /// <summary>Instant of its last write, which is what the age is measured from.</summary>
        public long WrittenAt { get; set; } = writtenAt;

        /// <summary>Times it has been written, the first one included.</summary>
        public int Attempts { get; set; }
    }

    /// <summary>
    /// Stream windows by peer and by the identifier's low twelve bits. Held weakly, so a
    /// peer that is gone takes its window with it.
    /// </summary>
    private readonly ConditionalWeakTable<PeerSession, Dictionary<ushort, List<Pending>>> windows = [];

    private readonly Lock gate = new();

    /// <summary>
    /// Records the records of one write, and returns the re-sends that go in front of
    /// them.
    /// </summary>
    /// <remarks>
    /// Called from the write path before the frame is built, so the re-sends and the new
    /// records travel in one frame, in the order the peer has to read them. A record is
    /// held only if the peer answers it
    /// (<see cref="PeerAcknowledgementUtils.WaitsForAcknowledgement"/>): a tick record is
    /// numbered by its class rather than by a counter and is never answered.
    /// </remarks>
    /// <param name="session">Peer the records are being written to.</param>
    /// <param name="messages">Records of the write, in order.</param>
    /// <returns>The re-sends followed by the records, or the records alone.</returns>
    public IReadOnlyList<UdpMessage> NoteWriting(PeerSession session, IReadOnlyList<UdpMessage> messages)
    {
        var due = new List<UdpMessage>();
        var now = Environment.TickCount64;
        lock (gate)
        {
            var byStream = windows.GetOrCreateValue(session);
            foreach (var message in messages)
            {
                if (!PeerAcknowledgementUtils.WaitsForAcknowledgement(message))
                {
                    continue;
                }

                var stream = MatchRecordSequenceService.StreamOf(message.Type);
                if (!byStream.TryGetValue(stream, out var pending))
                {
                    pending = [];
                    byStream[stream] = pending;
                }

                // What is still held is what the peer has not taken, and the oldest of
                // them is the one it is waiting for.
                if (pending.Count > 0 && now - pending[0].WrittenAt >= pacing.IntervalFor(session))
                {
                    AddRetries(stream, pending, due, now);
                }

                // A copy of a held record keeps the byte it already carries rather than
                // entering the window a second time: the peer answers a byte, and two
                // entries under one byte would make that answer ambiguous. Its attempt
                // count is kept too, or a re-send would reset the budget it is spending
                // and a peer that stopped reading would draw copies forever.
                var held = pending.Find(entry => entry.Type == message.Type && entry.Ordinal == message.Flags);
                if (held is null)
                {
                    pending.Add(new Pending(message.Type, message.Flags, message.Body, now));
                }
                else
                {
                    held.WrittenAt = now;
                }

                if (pending.Count > WindowLimit)
                {
                    logger.LogDebug(
                        "Stream 0x{Stream:x3} drops {Count} unanswered record(s): the peer is more than "
                        + "{Limit} values behind and the window cannot hold them",
                        stream,
                        pending.Count - WindowLimit,
                        WindowLimit);
                    pending.RemoveRange(0, pending.Count - WindowLimit);
                }
            }
        }

        if (due.Count == 0)
        {
            return messages;
        }

        due.AddRange(messages);
        return due;
    }

    /// <summary>
    /// Frees a stream up to the byte the peer answered, and returns nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The removal is cumulative because the answer is: the peer accepts a stream's
    /// records only in order, so an answer naming a byte proves every earlier byte on
    /// that stream was taken. Nothing survives behind it that the peer refused — what is
    /// still held was written after the answered record and is simply unanswered, and
    /// the age paths write it again if the peer stays quiet.
    /// </para>
    /// <para>
    /// <b>Re-sending it here is what flooded a joining client.</b> Every answer used to
    /// return the whole remaining window, so one acknowledgement drew a fresh copy of
    /// every record still pending: the 2026-11-16 join wrote 549 roster records in four
    /// seconds from 72 answers, and a client reading a stream it had already been given
    /// never finished its join. The image's answer branch only marks the entry answered —
    /// its sends come from the receive path's container-order walk and from the beat — so
    /// this returns no records at all.
    /// </para>
    /// </remarks>
    /// <param name="session">Peer that answered.</param>
    /// <param name="recordType">Identifier the answered record travelled under.</param>
    /// <param name="ordinal">Fourth byte the answer echoes.</param>
    /// <returns>An empty list: an answer frees a stream, it does not re-send one.</returns>
    public IReadOnlyList<UdpMessage> NoteAnswered(PeerSession session, ushort recordType, byte ordinal)
    {
        lock (gate)
        {
            if (!windows.TryGetValue(session, out var byStream))
            {
                return [];
            }

            var stream = MatchRecordSequenceService.StreamOf(recordType);
            if (!byStream.TryGetValue(stream, out var pending))
            {
                return [];
            }

            var answered = pending.FindIndex(entry => entry.Ordinal == ordinal);
            if (answered < 0)
            {
                return [];
            }

            pending.RemoveRange(0, answered + 1);
        }

        return [];
    }

    /// <summary>
    /// Returns the records a peer has left unanswered, for the write the receive path
    /// makes — the way the game's own host re-sends.
    /// </summary>
    /// <remarks>
    /// Called once per inbound frame, before anything that frame makes this host write:
    /// the game's phase tick calls <c>receive_p2p_datagrams()</c> first for the same
    /// reason. Without it, a stream that has lost a value and then goes quiet keeps the
    /// peer waiting until this host happens to write to that stream again, which on the
    /// scoreboard stream can be seconds.
    /// <para>
    /// <b>The age it waits is the peer's paced interval, not a frame's worth.</b> At 50 ms
    /// every inbound datagram wrote the whole window again: the 2026-11-16 join produced 549
    /// roster records in four seconds, most of them duplicates of records the client had
    /// already taken but not yet answered, and a client reading that flood never sent another
    /// record of its own. The recorded host's copies of a delta are 0.6 s apart, which is what
    /// the default interval is.
    /// </para>
    /// </remarks>
    /// <param name="session">Peer a frame just arrived from.</param>
    /// <returns>The records to write again, oldest stream first.</returns>
    public IReadOnlyList<UdpMessage> NoteReceived(PeerSession session)
    {
        var due = new List<UdpMessage>();
        var now = Environment.TickCount64;
        lock (gate)
        {
            if (!windows.TryGetValue(session, out var byStream))
            {
                return due;
            }

            foreach (var (stream, pending) in byStream)
            {
                if (pending.Count == 0 || now - pending[0].WrittenAt < pacing.IntervalFor(session))
                {
                    continue;
                }

                AddRetries(stream, pending, due, now);
            }
        }

        return due;
    }

    /// <summary>
    /// Adds a stream's held records to a write, in order, and counts the attempt.
    /// </summary>
    /// <remarks>
    /// Every record behind the hole is written again and not only the hole: the peer
    /// refuses them while it waits, so they are as lost as the one it is waiting for. A
    /// record that has spent its attempts is left out, so a peer that has stopped reading
    /// does not draw a copy of the stream a frame.
    /// <para>
    /// <b>The guard is the image's, and it ends the stream rather than the record.</b> When the
    /// oldest record's writes have reached <c>0xff</c> the image aborts the channel
    /// (<c>P2P_SendRecord</c>, <c>0x26b778</c>); here the window of that stream is dropped, which
    /// is the same decision without tearing down a session the host is holding for other
    /// reasons. Every record of the stream goes with it, because the peer cannot be moved past
    /// the hole without the hole.
    /// </para>
    /// </remarks>
    /// <param name="stream">Stream being recovered.</param>
    /// <param name="pending">Records still held, oldest first.</param>
    /// <param name="due">The write the copies are added to.</param>
    /// <param name="now">Instant of this write, in milliseconds since boot.</param>
    private void AddRetries(ushort stream, List<Pending> pending, List<UdpMessage> due, long now)
    {
        // The image's own check, on the value before this write: writes − acked == 0xff ends
        // the stream. It is tested on the oldest record because that is the one the base is
        // measured from.
        if (pending.Count > 0 && pending[0].Attempts >= AttemptLimit)
        {
            logger.LogError(
                "Stream 0x{Stream:x3} is abandoned after {Attempts} unanswered write(s) of ordinal "
                + "0x{Ordinal:x2}: the peer never took the byte everything behind it waits for",
                stream,
                pending[0].Attempts,
                pending[0].Ordinal);
            pending.Clear();
            return;
        }

        var attempts = 0;
        foreach (var entry in pending)
        {
            if (entry.Attempts >= AttemptLimit)
            {
                continue;
            }

            entry.Attempts++;
            entry.WrittenAt = now;
            due.Add(FrameBuilderUtility.MessageOf(entry.Type, entry.Body, entry.Ordinal));
            attempts++;
        }

        if (attempts == 0)
        {
            return;
        }

        logger.LogDebug(
            "Stream 0x{Stream:x3} writes {Count} unanswered record(s) again, from 0x{Ordinal:x2}: "
            + "the peer did not take them and cannot move past them",
            stream,
            attempts,
            pending[0].Ordinal);
    }
}
