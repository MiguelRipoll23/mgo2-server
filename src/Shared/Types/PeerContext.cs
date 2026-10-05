using System.Net;

namespace Mgo2Server.Shared.Types;

/// <summary>
/// Context handed to a peer command handler for one inbound message.
/// </summary>
/// <param name="Session">Negotiated session the message arrived on.</param>
/// <param name="Message">Decoded message to handle.</param>
/// <param name="Remote">Endpoint the datagram came from.</param>
/// <param name="LocalPort">UDP port this host is listening on, used for endpoint advertisement.</param>
/// <param name="Send">
/// Writes a message back to the peer through the session's shared outbound
/// counter. Frames are keyed with the pre-handshake key or with the session
/// key according to <see cref="PeerSession.Established"/>. The last argument
/// is the record's fourth byte, which the recorded host fills on a roster
/// run and leaves at zero elsewhere.
/// </param>
/// <param name="Broadcast">
/// Writes a message to every other peer in the room, each through its own
/// session and counter. The peer this message came from is not included: a
/// handler that has to answer it as well uses <paramref name="Send"/> for that.
/// </param>
/// <param name="SendRecords">
/// Writes several messages to the peer as one datagram on one outbound
/// counter. The recorded host sends a whole roster run in a single frame, and
/// the run's records only mean anything together: a handler that writes them
/// one per datagram spends an outbound sequence on each and hands the peer a
/// roster it never saw assembled. Null where the context was built without it,
/// and a handler falls back to sending one message at a time.
/// </param>
/// <param name="SendRecordsCompressed">
/// The same run, written LZSS-compressed with the header's compression marker
/// set. The recorded host sends the roster run, the roster repeat and the
/// post-join burst this way and the short records that close them plain, so a
/// handler picks per run. Null where the context was built without it, and a
/// handler falls back to <see cref="SendRecords"/> rather than sending the run
/// in a format the recorded host never writes.
/// </param>
public sealed record PeerContext(
    PeerSession Session,
    UdpMessage Message,
    IPEndPoint Remote,
    int LocalPort,
    Func<ushort, byte[], byte, Task> Send,
    Func<ushort, byte[], Task> Broadcast,
    Func<IReadOnlyList<UdpMessage>, Task>? SendRecords = null,
    Func<IReadOnlyList<UdpMessage>, Task>? SendRecordsCompressed = null);
