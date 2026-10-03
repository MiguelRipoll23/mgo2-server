using System.Net;
using System.Net.Sockets;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The outbound half of the gameplay server: it writes a message to one peer,
/// or to every peer but the one it came from, each through its own session
/// counter and dial-back endpoint.
/// </summary>
public sealed partial class GameplayServerService
{
    /// <summary>
    /// Writes a message to every established peer except the one it came from.
    /// Peers are skipped rather than removed: a peer that has not finished its
    /// handshake is not in the room yet, and the session is dropped when it goes
    /// quiet.
    /// </summary>
    /// <param name="origin">Session the message came from, which is left out.</param>
    /// <param name="messageType">Type of the message.</param>
    /// <param name="body">Body of the message.</param>
    private void SendToOthers(PeerSession origin, ushort messageType, byte[] body)
    {
        foreach (var session in sessions.Snapshot())
        {
            if (session.Established && !ReferenceEquals(session, origin))
            {
                SendMessage(session, messageType, body);
            }
        }
    }

    /// <summary>
    /// Writes a message to a peer through the session's shared counter, to the
    /// endpoint the session dials back rather than to the address a datagram
    /// happened to arrive from. The two differ whenever the host sits behind a
    /// load balancer, and only the first is a socket the peer is reading.
    /// </summary>
    /// <remarks>
    /// The counter is fifteen bits and the header's bit 15 is the compression
    /// marker, so the increment wraps at <c>0x7fff</c> rather than at
    /// <c>0xffff</c>. Wrapping at sixteen bits would let the counter reach
    /// <c>0x8000</c> and mark a plaintext frame as compressed, which the peer
    /// would then try to decompress.
    /// </remarks>
    /// <param name="session">Session to write through.</param>
    /// <param name="messageType">Type of the message.</param>
    /// <param name="body">Body of the message.</param>
    private void SendMessage(PeerSession session, ushort messageType, byte[] body)
    {
        var counter = session.OutboundCounter;
        session.OutboundCounter = FrameCounterUtility.Next(counter);

        var plain = FrameBuilderUtility.BuildMessageFrame(counter, [FrameBuilderUtility.MessageOf(messageType, body)]);
        var key = session.Established ? session.SessionKey : UdpCryptoKeyConstants.PreHandshakeKey;
        var digestKey = session.Established
            ? session.SessionKey ^ UdpCryptoKeyConstants.TailDigestKey
            : UdpCryptoKeyConstants.TailDigestKey;

        Send(FrameCryptoUtility.EncodeFrame(plain, counter, key, digestKey), session.DialBack);
        logger.LogDebug(
            "Outbound counter={Counter} type={MessageType:x4} to {DialBack}{PreKeyed}",
            counter,
            messageType,
            session.DialBack,
            session.Established ? string.Empty : " pre");
    }

    private void Send(byte[] data, IPEndPoint remote)
    {
        try
        {
            socket?.Send(data, data.Length, remote);
        }
        catch (SocketException exception)
        {
            logger.LogWarning("Send to {RemoteAddress} failed: {Message}", remote, exception.Message);
        }
    }
}
