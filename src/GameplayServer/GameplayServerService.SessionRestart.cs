using System.Net;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The half of the gameplay server that lets a peer start its session over: a
/// client that has been dropped — or that left and came back — is a client with
/// a new handshake, and a new handshake is a new session.
/// </summary>
/// <remarks>
/// <para>
/// <b>The client's counter base is what tells the two handshakes apart.</b> A
/// client re-sends its handshake every two seconds until the reply lands, and
/// every copy carries the base the first one did — `mgo2-started-on-solo-game`
/// sends four of them over six seconds with <c>0x8557bdb7</c> in all four. A
/// client that has restarted draws a new base, because the value is chosen per
/// run. So the base, and not the arrival, is the discriminator: a copy is the
/// same peer waiting, and a new base is the same peer joining again.
/// </para>
/// <para>
/// <b>A copy of a handshake that has already been answered reopens the
/// transport instead.</b> Re-sending is what a client does while it is still
/// waiting for the reply, so a copy on a session that is not yet established is
/// the same wait and is served as one. Once the session <i>is</i> established,
/// the same base means the opposite: the peer either never read the answer it
/// was given or has come back on the same endpoint. That answer cannot be
/// repeated as it stands, because every frame after the handshake is keyed and
/// the client derives the key from the reply's own base — a keyed reply is one
/// the client has no key to open. The session therefore keeps its peer state and
/// takes its transport back to the opening state, so the answer travels pre-keyed
/// with counters <c>0</c> and <c>1</c> as a first join's does. Retiring it would
/// drop a roster entry and a briefing report for a peer that is still the same
/// player, which is why the two cases are not one.
/// </para>
/// <para>
/// <b>The old session is not repaired, it is retired.</b> Its key, counter and
/// acknowledgement window belong to a conversation the client has forgotten, and
/// every worker of this host is watching that object — the run watcher, the
/// briefing's countdown, the ready set, the beat. Retiring it ends them all
/// (<see cref="Mgo2Server.GameplayServer.MatchRecords.MatchRecordPeerUtils.IsAlive"/>),
/// and the handshake provisions a fresh session under the same endpoint, which
/// the peer's own join then fills in from the profile it sends. What the room
/// held for it is dropped on the way out, for the reason the reaper drops it: a
/// ready state, a vitals value or a roster entry kept for a session that no
/// longer exists is state about nobody.
/// </para>
/// <para>
/// <b>A dropped peer rejoins the same way.</b> The reaper lets a peer go after
/// <see cref="Mgo2Server.Shared.Udp.PeerSessionService.DefaultIdleTimeout"/> and
/// the client's next handshake finds no session at all, which is the branch that
/// was always there. This one is for the peer that comes back sooner than that,
/// or whose session the reaper has not reached yet.
/// </para>
/// </remarks>
public sealed partial class GameplayServerService
{
    /// <summary>
    /// Ends the session a peer has left, when the handshake that has just
    /// arrived is for a new one.
    /// </summary>
    /// <param name="message">The message the frame opened with.</param>
    /// <param name="remote">Endpoint the datagram arrived from.</param>
    /// <param name="remoteAddress">That endpoint formatted as "address:port".</param>
    /// <param name="session">Session already held for that endpoint, when there is one.</param>
    /// <returns>
    /// The session the frame belongs to: the one passed in when the handshake is
    /// a re-send, and <c>null</c> when the peer has started over and the caller is
    /// to provision a new one.
    /// </returns>
    private PeerSession? RestartPeerIfHandshakeStartsOver(
        UdpMessage message,
        IPEndPoint remote,
        string remoteAddress,
        PeerSession? session)
    {
        if (session is null ||
            message.Type != UdpCommandConstants.Handshake ||
            message.Body.Length < 8)
        {
            return session;
        }

        var counterBase = BinaryUtility.ReadUInt32LittleEndian(message.Body, 4);
        if (counterBase == session.CounterBase)
        {
            if (!session.Established)
            {
                logger.LogDebug(
                    "[{LogPrefix}] IN {RemoteAddress} re-sent its handshake (base 0x{CounterBase:x8}); "
                    + "the session it belongs to is the one already held",
                    LogPrefix,
                    remoteAddress,
                    counterBase);
                return session;
            }

            logger.LogInformation(
                "[{LogPrefix}] IN {RemoteAddress} sent its handshake again on an established session "
                + "(base 0x{CounterBase:x8}); answering it pre-keyed again, with the peer's room state kept",
                LogPrefix,
                remoteAddress,
                counterBase);
            ReopenHandshake(session);
            return session;
        }

        logger.LogInformation(
            "[{LogPrefix}] {RemoteAddress} started its session over: handshake base 0x{PreviousCounterBase:x8} "
            + "-> 0x{CounterBase:x8}, answered on {DialBack}; its room state is dropped and it joins again",
            LogPrefix,
            remoteAddress,
            session.CounterBase,
            counterBase,
            remote == session.DialBack ? remoteAddress : $"{remote} (was {session.DialBack})");

        // Retired first: every worker of this host tests it, and a worker that
        // wakes between this line and the removal must not write to a session the
        // peer has left.
        session.Retired = true;
        sessions.Remove(session.RemoteAddress);
        ForgetPeerState(session);
        return null;
    }

    /// <summary>
    /// Returns a session's transport to the state its handshake opened it in,
    /// so the answer goes out pre-keyed.
    /// </summary>
    /// <remarks>
    /// Only the transport is touched. The key, the peer identifier and the pair
    /// list are what the arriving handshake is about to write again, and the room
    /// state filed against the session — a roster entry, a briefing report, the
    /// records the peer has answered — belongs to the player rather than to the
    /// conversation, so it stays.
    /// </remarks>
    /// <param name="session">Session whose handshake is being answered again.</param>
    private static void ReopenHandshake(PeerSession session)
    {
        session.Established = false;
        session.OutboundCounter = 0;
        session.LastInboundSequence = 0;
    }
}
