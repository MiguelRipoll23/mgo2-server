using System.Net;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The half of the gameplay server that keeps a peer's session together when the
/// endpoint it answers from changes.
/// </summary>
/// <remarks>
/// <para>
/// <b>A peer is not its endpoint.</b> A session is filed under the endpoint its
/// handshake arrived from, and dials back to the endpoint that handshake
/// advertised; neither is necessarily where the peer's next frame comes from. A
/// client that re-binds its socket, a router that starts or stops hairpinning its
/// own address back into the LAN, and a relay in front of this host that has closed
/// all move a peer without telling it, and the two endpoint lookups then miss a
/// peer that is still there.
/// </para>
/// <para>
/// <b>The key is what names it.</b> Every keyed frame verifies against a tail
/// digest derived from the session's key, so trying each key this host holds
/// identifies the peer without a word from the client: one frame that verifies is
/// that peer, and the endpoint it just used becomes its own. A client that has
/// been dropped does not handshake again either — it keeps writing the frames it
/// was writing — which is why the reaped sessions keep their keys
/// (<see cref="Mgo2Server.Shared.Udp.PeerSessionService.SnapshotRetired"/>).
/// </para>
/// </remarks>
public sealed partial class GameplayServerService
{
    /// <summary>
    /// Tries every key this host holds against a frame whose endpoint named no
    /// session, and hands the frame on under the one that verifies.
    /// </summary>
    /// <remarks>
    /// The session the endpoint lookup matched was already tried with its own key
    /// by the caller, so it is the one candidate not worth repeating. What the room
    /// held for a reaped peer is not restored here: the reaper's callback has
    /// dropped it, and the join the peer is in the middle of re-states it.
    /// </remarks>
    /// <param name="work">Frame to unscramble and dispatch, header already read.</param>
    /// <param name="remote">Endpoint the datagram arrived from.</param>
    /// <param name="remoteAddress">That endpoint formatted as "address:port".</param>
    /// <param name="matched">Session the endpoint lookup found, when it found one.</param>
    /// <returns><c>true</c> when a key verified and the frame was dispatched.</returns>
    private async Task<bool> TryDispatchFromMovedEndpointAsync(
        byte[] work,
        IPEndPoint remote,
        string remoteAddress,
        PeerSession? matched)
    {
        foreach (var candidate in sessions.Snapshot().Concat(sessions.SnapshotRetired()))
        {
            if (ReferenceEquals(candidate, matched))
            {
                continue;
            }

            var candidateKey = candidate.CounterBase ^ hostIdentity.CounterBase;
            var candidateCounter = TryUnscrambleWith(
                work,
                candidateKey ^ UdpCryptoKeyConstants.TailDigestKey);
            if (candidateCounter is not { } counter)
            {
                continue;
            }

            var previousEndpoint = candidate.DialBack;
            sessions.Revive(candidate, remote);
            logger.LogInformation(
                "[{LogPrefix}] IN {RemoteAddress} is peer {PeerIdentifier} answering from a new endpoint "
                + "(was {PreviousEndpoint}); that endpoint is its own from now on",
                LogPrefix,
                remoteAddress,
                candidate.PeerIdentifier,
                previousEndpoint);

            FrameCryptoUtility.RemoveChainInPlace(work, counter, candidateKey);
            await DispatchKeyedAsync(work, counter, candidateKey, candidate, remote);
            return true;
        }

        return false;
    }
}
