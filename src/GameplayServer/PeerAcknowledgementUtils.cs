using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// Reads which of a peer's records wait to be answered, and builds the record
/// that answers each one.
/// </summary>
/// <remarks>
/// <para>
/// Both peers number their session records and answer each other's. Measured
/// over the reference round (<c>mgo2-game.pcapng</c>, joiner
/// <c>10.2.0.2:5730</c>): the host sends its roster as <c>0x5001</c> byte 0,
/// <c>0x9001</c> byte 0, <c>0x1001</c> bytes 1 and 2, and the joiner answers
/// <c>0xd001</c> byte 0, <c>0x5001</c> byte 1 and <c>0x5001</c> byte 2 — one
/// answer per record, of the same identifier with bit <c>0x4000</c> set and the
/// same fourth byte. The host's post-join burst is answered the same way:
/// its <c>0x9001</c> byte 3 by <c>0xd001</c> byte 3, its three <c>0x1001</c>
/// bytes 4, 5 and 6 by three <c>0x5001</c> with those bytes. A record that
/// already carries <c>0x4000</c> is not answered again, and tick records (below
/// <c>0x1000</c>) are not numbered at all.
/// </para>
/// <para>
/// <b>The body length is part of the answer.</b> An answer to a record carrying
/// bit <c>0x8000</c> carries <em>one</em> body byte, and an answer to one
/// without it carries none. That is what the captures show in both directions
/// without exception: the joiner's <c>0x9001</c> records draw <c>0xd001</c>
/// answers with a single byte (<c>0x03</c>, <c>0x23</c> from the host; a
/// reference round's host sent <c>0x24</c> to the joiner's first <c>0x9001</c>),
/// and its <c>0x1001</c> records draw empty <c>0x5001</c> answers. Serving the
/// <c>0x8000</c> answer with an empty body instead — the shape that looks like
/// the <c>0x1001</c> case — was measured against a live join and left the
/// client in the connect FSM's state 2 until it raised <c>0B09</c>, which is
/// what a malformed answer buys.
/// </para>
/// <para>
/// <b><c>0xd001</c> is the answer form itself.</b> <c>0x9001 | 0x4000</c> and
/// <c>0x5001 | 0x8000</c> are both <c>0xd001</c>, which is why a host's answer to
/// either family arrives under one identifier: `briefing-then-solo-game`'s round
/// opens on <c>0xd001</c> because the peer had just sent
/// <c>0x9001 len 1 ordinal 0x21</c> and the answer carries the same ordinal.
/// </para>
/// <para>
/// <b>What the one byte <em>does</em> is measured in the image.</b> The record's
/// handler (<c>0x26DB78</c>) writes it to state <c>0x00</c>, byte <c>99</c> —
/// field 24 of that state's 55 (<c>docs/analysis/join-stall-after-mode2.md</c>
/// §30.5, §30.7). Two functions in the image read that field, and both do the
/// same thing with it: at <c>0x77E6A0</c> and <c>0x77F878</c> the screen copies
/// it to its own <c>+0xd0</c>, returns if it is <c>0xff</c>, returns if its step
/// (<c>+0xb8</c>) is already <c>12</c>, and otherwise sets the step to <c>12</c>,
/// sets <c>+0xc4 = 1</c> and emits script event <c>0x16002C</c> with the byte
/// (<c>0x7770F0</c>, which walks the runtime listener list). So the byte is a
/// **one-shot token that moves a screen at step nine or below to step twelve** —
/// the "advance to the next screen" argument the client's own screens consume —
/// and <c>0xff</c> is the declared no-value.
/// </para>
/// <para>
/// <b>The value is the acker's own, and functions ignore it.</b> It does not track
/// the record being answered — a peer's <c>0x9001</c> byte 1 with body <c>0x0a</c>
/// is answered <c>0x24</c> in one round and <c>0x0b</c> in another — and the two
/// readers above hand it to a listener nothing binds at compile time, so no value
/// is <em>required</em>: it names no sequence and answers no question, and only a
/// screen that has not advanced can observe it. What the corpus does show is that a
/// host picks it by a habit of its own — per phase in some sessions, per answer in
/// others — over a wide alphabet (`tools/udp_control_value_inventory.py` prints it,
/// and its ladder mode prints the screen step each value leaves behind;
/// `docs/analysis/control-byte-findings.md` reads the three habits out of the
/// sixteen sessions).
/// </para>
/// <para>
/// <b>The byte itself is chosen in one place.</b> Which value a pass carries is
/// the room's phase — <see cref="Rooms.PeerControlByteService"/> owns that
/// ladder, with the measurements behind each rung — and this utility only writes
/// the byte it is given into the answer's body.
/// </para>
/// </remarks>
public static class PeerAcknowledgementUtils
{
    /// <summary>
    /// Class bit that marks a record as an answer to the peer's own.
    /// </summary>
    /// <remarks>
    /// It sits in the record identifier beside
    /// <see cref="PayloadClassBit"/>, not in the frame header: the header has
    /// no flag but the fifteen-bit counter's compression marker.
    /// </remarks>
    public const ushort AcknowledgementBit = 0x4000;

    /// <summary>
    /// Class bit whose answers carry one body byte rather than none.
    /// </summary>
    /// <remarks>
    /// Both of the joiner's own numbered families carry it — <c>0x9001</c> is
    /// <c>0x1001 | 0x8000</c> — and both of its roster families are answered
    /// without it. What the bit <em>means</em> is not established; that the
    /// answer's body length follows it is what the captures show.
    /// </remarks>
    public const ushort PayloadClassBit = 0x8000;

    /// <summary>
    /// Whether a record is one of the numbered session records, which are the only
    /// ones either side answers.
    /// </summary>
    /// <remarks>
    /// <b>The payload bit is masked off before the threshold is tested.</b> A tick
    /// record can carry it too — a replay sends <c>0x890d</c>, which is
    /// <c>0x090d</c> with <see cref="PayloadClassBit"/>, and is a record about a
    /// player's state rather than a numbered session record. Testing the raw type
    /// reads it as one, and answers it with <c>0xc90d</c>: measured against the
    /// bench, no capture holds a <c>0xc90d</c> in either direction, and the
    /// reference hosts leave the record unanswered.
    /// </remarks>
    /// <param name="message">Decoded record to test.</param>
    /// <returns><c>true</c> when the record is one the two sides number.</returns>
    public static bool IsNumberedRecord(UdpMessage message) =>
        (ushort)(message.Type & ~PayloadClassBit) >= UdpCommandConstants.TickRecordThreshold;

    /// <summary>
    /// Whether a message is a frame acknowledgement rather than a record that
    /// needs a record-level answer.
    /// </summary>
    /// <param name="message">Decoded message to test.</param>
    /// <returns><c>true</c> when this is a one-byte zero acknowledgement entry.</returns>
    public static bool IsFrameAcknowledgement(UdpMessage message) =>
        (message.Type & 0xf000) == UdpCommandConstants.AcknowledgementClass &&
        message.Length == 1 &&
        message.Body.Length == 1 &&
        message.Body[0] == 0;

    /// <summary>
    /// Whether a message is a record-level answer to one this host sent.
    /// </summary>
    /// <remarks>
    /// The other direction from <see cref="WaitsForAcknowledgement"/>: this is
    /// the peer saying it has one of ours, which is the only evidence there is
    /// that a roster was accepted. The captured client answers the opening run
    /// 56 ms after receiving it (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §8.6),
    /// so a join whose roster draws no answer is a join the client rejected --
    /// and nothing else on the wire distinguishes the two.
    /// </remarks>
    /// <remarks>
    /// The keep-alive is excluded by identifier, not by class. It is id 0 of
    /// the <c>0x5x..</c> class, so it and the answer to a <c>0x1001</c> record
    /// — <c>0x5001</c> — are the same four bits apart, and masking the answer
    /// bit off <c>0x5000</c> leaves the handshake, which is not a numbered
    /// record and is never answered this way. Reading the keep-alive as an
    /// answer makes every join look acknowledged the moment the client says
    /// hello, which is the opposite of what this is for.
    /// </remarks>
    /// <param name="message">Decoded message to test.</param>
    /// <returns>
    /// <c>true</c> for an answer naming a record, and <c>false</c> for a frame
    /// acknowledgement or a keep-alive.
    /// </returns>
    public static bool IsRecordAcknowledgement(UdpMessage message) =>
        IsNumberedRecord(message) &&
        (message.Type & AcknowledgementBit) != 0 &&
        message.Type != UdpCommandConstants.KeepAlive &&
        !IsFrameAcknowledgement(message);

    /// <summary>
    /// Whether a record waits for this host to answer it.
    /// </summary>
    /// <param name="message">Decoded record to test.</param>
    /// <returns><c>true</c> when the record is a session record carrying no answer bit yet.</returns>
    public static bool WaitsForAcknowledgement(UdpMessage message) =>
        IsNumberedRecord(message) &&
        (message.Type & AcknowledgementBit) == 0 &&
        !IsFrameAcknowledgement(message);

    /// <summary>
    /// Builds the record that answers one of the peer's.
    /// </summary>
    /// <param name="message">Record being answered.</param>
    /// <param name="controlByte">
    /// Control byte the answer carries, which is the phase the room is in
    /// (<see cref="Rooms.PeerControlByteService"/>). It reaches the wire only for a
    /// record that carries <see cref="PayloadClassBit"/>: the other family's
    /// answer has no body at all, so the value is read for nothing there.
    /// </param>
    /// <returns>The answer to send.</returns>
    public static UdpMessage AcknowledgementOf(UdpMessage message, byte controlByte)
    {
        byte[] body = (message.Type & PayloadClassBit) != 0 ? [controlByte] : [];

        return FrameBuilderUtility.MessageOf(
            (ushort)(message.Type | AcknowledgementBit),
            body,
            message.Flags);
    }
}
