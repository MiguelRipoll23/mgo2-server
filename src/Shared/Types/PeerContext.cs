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
/// key according to <see cref="PeerSession.Established"/>.
/// </param>
/// <param name="Broadcast">
/// Writes a message to every other peer in the room, each through its own
/// session and counter. The peer this message came from is not included: a
/// handler that has to answer it as well uses <paramref name="Send"/> for that.
/// </param>
public sealed record PeerContext(
    PeerSession Session,
    UdpMessage Message,
    IPEndPoint Remote,
    int LocalPort,
    Func<ushort, byte[], Task> Send,
    Func<ushort, byte[], Task> Broadcast);
