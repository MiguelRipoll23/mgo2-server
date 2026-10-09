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
/// byte — the same family <see cref="Stream.HostStreamFramesUtils"/> replays for
/// the host's sparse phase and that
/// <see cref="Rooms.PostJoinBurstService.OpeningControlByte"/> measures at the
/// join. The measured join-time value is used here and nothing is claimed to
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
    /// Control byte an answer to a <see cref="PayloadClassBit"/> record carries.
    /// </summary>
    /// <remarks>
    /// The value the reference round's host wrote when it answered the joiner's
    /// first <c>0x9001</c>, which is the same byte
    /// <see cref="Rooms.PostJoinBurstService.OpeningControlByte"/> measures on
    /// the burst that follows it. One round's value rather than a rule.
    /// </remarks>
    public const byte HostControlByte = Rooms.PostJoinBurstService.OpeningControlByte;

    /// <summary>
    /// Whether a record waits for this host to answer it.
    /// </summary>
    /// <param name="message">Decoded record to test.</param>
    /// <returns><c>true</c> when the record is a session record carrying no answer bit yet.</returns>
    public static bool WaitsForAcknowledgement(UdpMessage message) =>
        message.Type >= UdpCommandConstants.TickRecordThreshold &&
        (message.Type & AcknowledgementBit) == 0;

    /// <summary>
    /// Builds the record that answers one of the peer's.
    /// </summary>
    /// <param name="message">Record being answered.</param>
    /// <returns>The answer to send.</returns>
    public static UdpMessage AcknowledgementOf(UdpMessage message)
    {
        byte[] body = (message.Type & PayloadClassBit) != 0 ? [HostControlByte] : [];

        return FrameBuilderUtility.MessageOf(
            (ushort)(message.Type | AcknowledgementBit),
            body,
            message.Flags);
    }
}
