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
/// The body <em>value</em> is not derivable. Every sample in every round is a
/// different byte, they do not track the record being answered (the joiner's
/// <c>0x9001</c> byte 1 with body <c>0x0a</c> is answered by <c>0x24</c> in one
/// round and <c>0x0b</c> in another), and the shape is the acker's own control
/// byte. The measured join-time value is used here and nothing is claimed to
/// read it.
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
    /// Control byte an answer carries while the room has not briefed yet — the
    /// join pass, and the pass that writes a run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value the reference hosts write in their joining passes. Measured on
    /// host side, one byte, over the three captures that carry the answer:
    /// <c>mgo2-game3</c> writes <c>24</c> on both answers of its join pass at
    /// t+8.595 and again on the single answer issued in the pass that writes its
    /// briefing's run (t+59.596, beside the <c>0x5ade</c> marker);
    /// <c>mgo2-game1</c> writes <c>24</c> at t+6.687 and <c>25</c> and <c>24</c>
    /// beside its two <c>0x52de</c> runs; <c>mgo2-game-started-on-briefing</c>
    /// writes <c>0c</c> and <c>0a</c> there, so the value itself is the host's own
    /// and only its <em>place</em> is general — the pass a run is written in.
    /// </para>
    /// <para>
    /// The byte is a leaf of the client's session state (state <c>0x00</c>, byte
    /// <c>99</c>, one byte, no flags), and the only C++ reader of it raises the
    /// script event <c>0x16002C</c> with it, so what a value <em>means</em> is not
    /// in the image (<c>docs/analysis/join-stall-after-mode2.md</c> §30.7, §56).
    /// The three constants here are the phases the captures separate and nothing
    /// finer.
    /// </para>
    /// </remarks>
    public const byte HostControlByte = 0x24;

    /// <summary>
    /// Control byte an answer carries while a briefing is waiting on its players.
    /// </summary>
    /// <remarks>
    /// <b>Measured, and it is not the joining value.</b> <c>mgo2-game3</c> holds
    /// <c>03</c> for the whole of its briefing — from t+10.967, the answer behind
    /// its join, to t+118.218, the pass its round opens in — and
    /// <c>mgo2-game1</c> alternates <c>02</c> and <c>03</c> over the same stretch,
    /// so a host that wrote its joining byte through a briefing would be telling
    /// the client it is still on the screen the join was answered on.
    /// </remarks>
    public const byte BriefingControlByte = 0x03;

    /// <summary>
    /// Control byte an answer carries while a round is playing.
    /// </summary>
    /// <remarks>
    /// Both hosts that hold a briefing step to <c>04</c> when their round opens
    /// (<c>mgo2-game3</c> at t+118.218, where its run precedes the answer,
    /// <c>mgo2-game1</c> at t+88.035) and stay in <c>04</c>–<c>07</c> for the rest
    /// of the capture.
    /// </remarks>
    public const byte RoundControlByte = 0x04;

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
        message.Type >= UdpCommandConstants.TickRecordThreshold &&
        (message.Type & AcknowledgementBit) != 0 &&
        message.Type != UdpCommandConstants.KeepAlive &&
        !IsFrameAcknowledgement(message);

    /// <summary>
    /// Whether a record waits for this host to answer it.
    /// </summary>
    /// <param name="message">Decoded record to test.</param>
    /// <returns><c>true</c> when the record is a session record carrying no answer bit yet.</returns>
    public static bool WaitsForAcknowledgement(UdpMessage message) =>
        message.Type >= UdpCommandConstants.TickRecordThreshold &&
        (message.Type & AcknowledgementBit) == 0 &&
        !IsFrameAcknowledgement(message);

    /// <summary>
    /// Builds the record that answers one of the peer's.
    /// </summary>
    /// <param name="message">Record being answered.</param>
    /// <param name="controlByte">
    /// Control byte the answer carries, which is the phase the answering host is
    /// in: <see cref="HostControlByte"/> on the join pass,
    /// <see cref="BriefingControlByte"/> while a briefing waits and
    /// <see cref="RoundControlByte"/> while a round plays.
    /// </param>
    /// <returns>The answer to send.</returns>
    public static UdpMessage AcknowledgementOf(UdpMessage message, byte controlByte = HostControlByte)
    {
        byte[] body = (message.Type & PayloadClassBit) != 0 ? [controlByte] : [];

        return FrameBuilderUtility.MessageOf(
            (ushort)(message.Type | AcknowledgementBit),
            body,
            message.Flags);
    }
}
