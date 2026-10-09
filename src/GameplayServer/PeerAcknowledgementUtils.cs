using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// Reads which of a peer's records wait to be acknowledged, and builds the
/// record that acknowledges each one.
/// </summary>
/// <remarks>
/// <para>
/// The peer-to-peer stream is selective-repeat over <em>records</em>, not frames:
/// every session record carries its own sequence in its fourth byte, and the
/// sender holds it in a window until the host says it arrived. Its join driver
/// promotes the session to its data phase only once the record it built shows as
/// many sent as acknowledged — the two cursors at <c>+0xb</c> and <c>+0xc</c> of
/// that record, compared by <c>0x26ab58</c> and the gate the live join stalls on
/// (<c>docs/protocol/P2P_CONNECT_FSM.md</c> §7.1b). A host that answers the
/// handshake, the profile and the roster but acknowledges nothing leaves the
/// joiner in exactly that state, sending the same records on a loop, until the
/// connect FSM's state 2 counts both deadlines out and raises <c>0B09</c>.
/// </para>
/// <para>
/// <b>The answer is the same identifier with the acknowledgement bit set.</b>
/// Every record identifier in the reliable class is <c>0x1000 | class | id</c>
/// with two class bits, <c>0x4000</c> and <c>0x8000</c>, and <c>0x4000</c> is
/// the bit a reply carries. Measured over the reference round
/// (<c>mgo2-game.pcapng</c>) and independently over two more captured rounds
/// (<c>mgo2-game2.pcapng</c>, <c>mgo2-game3.pcapng</c>), every one of the
/// joiner's <c>0x9001</c> records is answered by a <c>0xd001</c> carrying the
/// same fourth byte (<c>0x9001 | 0x4000</c>), its <c>0x1001</c> records by the
/// host's <c>0x5001</c>, its <c>0x5001</c> by <c>0x9001</c>, and so on for the
/// slot and state records. One answer goes out per inbound record, not one per
/// sequence: the joiner re-sends a record until it is covered, and a host that
/// answered a sequence only once would be silent on every re-send after a lost
/// answer, which is the stall this exists to break.
/// </para>
/// <para>
/// The keep-alive <c>0x5000</c> and the replies themselves already carry the
/// bit, so they are not answered and no answer answers an answer. Records below
/// <see cref="UdpCommandConstants.TickRecordThreshold"/> are in-game tick
/// records: they travel under a length byte that counts their attribute class,
/// they are not retransmitted, and neither recorded host answers them.
/// </para>
/// </remarks>
public static class PeerAcknowledgementUtils
{
    /// <summary>
    /// Class bit that marks a record as an acknowledgement of the peer's own.
    /// </summary>
    /// <remarks>
    /// It sits beside <see cref="UdpCommandConstants.CompressionMarker"/> in the
    /// identifier, not in the frame header: the header's fifteen-bit counter has
    /// no flag but the compression marker, and this bit never appears there.
    /// </remarks>
    public const ushort AcknowledgementBit = 0x4000;

    /// <summary>
    /// Whether a record waits for this host to acknowledge it.
    /// </summary>
    /// <param name="message">Decoded record to test.</param>
    /// <returns><c>true</c> when the record is a session record carrying no answer bit yet.</returns>
    public static bool WaitsForAcknowledgement(UdpMessage message) =>
        message.Type >= UdpCommandConstants.TickRecordThreshold &&
        (message.Type & AcknowledgementBit) == 0;

    /// <summary>
    /// Builds the record that acknowledges one of the peer's.
    /// </summary>
    /// <remarks>
    /// The fourth byte is the acknowledged record's own, which is its sequence,
    /// and the body is empty: every answer in the captures is a one-record frame
    /// whose only moved field is that byte.
    /// </remarks>
    /// <param name="message">Record being acknowledged.</param>
    /// <returns>The answer to send.</returns>
    public static UdpMessage AcknowledgementOf(UdpMessage message) =>
        FrameBuilderUtility.MessageOf(
            (ushort)(message.Type | AcknowledgementBit),
            [],
            message.Flags);
}
