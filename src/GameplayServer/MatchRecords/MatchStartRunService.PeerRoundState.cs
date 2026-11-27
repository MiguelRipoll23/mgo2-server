using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The peer's own round state: the three round fields of its slot state block,
/// re-stated for as long as the peer is in the room.
/// </summary>
/// <remarks>
/// <see cref="Rooms.PeerRoundStateBodiesUtils"/> reads the record out of the image —
/// the state index is the slot's key plus one, the map names fields 19, 20 and 55,
/// and the three values are the room's closed-round count. What lives here is the
/// beat it runs on and the peer it belongs to.
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// The gap the reference leaves between two copies of a peer's round state:
    /// 1 140 ms. <c>rounded-ended-by-reaching-tickets-limit.pcapng</c> writes it
    /// forty-eight times on <c>0x1001</c>; the gaps between copies have a median of
    /// 1.139 s and a shortest of 1.097 s.
    /// </summary>
    public const int PeerRoundStatePeriodMilliseconds = 1_140;

    /// <summary>
    /// The peers whose round state is already being re-stated, by remote address,
    /// or <c>null</c> until the first one starts.
    /// </summary>
    /// <remarks>
    /// <b>The beat outlives the round, so it is started once and not once per
    /// round.</b> The reference writes the record through a round, through the result
    /// screens behind it and through the briefing that follows — one loop per peer
    /// session, not one per round, or a room on its third round would have three of
    /// them writing the same field.
    /// <para>
    /// <b>And it is made on first use, not by an initializer.</b> A field added to a
    /// process that is already running under <c>dotnet watch</c> is added without its
    /// initializer, so an initialized set is <c>null</c> in exactly the session that
    /// hot-reloaded the change — which took the whole run down in the live session of
    /// 2026-11-27 11:53, a <c>NullReferenceException</c> in
    /// <see cref="StartPeerRoundState"/> that ended the run before the briefing could
    /// open its round.
    /// </para>
    /// </remarks>
    private HashSet<string>? peerRoundStateWriters;

    /// <summary>
    /// Starts the peer's round state, if it is not already running.
    /// </summary>
    /// <remarks>
    /// <b>A round's open starts it, and a briefing does not.</b> The record is the
    /// round's own state: <c>rounded-ended-by-reaching-tickets-limit.pcapng</c> first
    /// writes it at t+74.571, inside a round, and its count steps only where a round
    /// closes. Written into a room's first briefing it hides that briefing instead of
    /// carrying it — the live run of 2026-11-27 11:53 answered a joining client with
    /// the briefing's run and state and the client drew no briefing at all, and the
    /// same record behind a round has been live since — so the beat begins where a
    /// round does. Once begun it outlives the round and covers the screens and the
    /// next briefing behind it.
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    private void StartPeerRoundState(PeerContext context)
    {
        var key = context.Remote.ToString();
        lock (gate)
        {
            if (!(peerRoundStateWriters ??= []).Add(key))
            {
                return;
            }
        }

        _ = PublishPeerRoundStateAsync(context, key);
    }

    /// <summary>
    /// Re-states one peer's round state every
    /// <see cref="PeerRoundStatePeriodMilliseconds"/> until the peer leaves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is not a round's record and it does not stop at a round's end.</b> It
    /// starts behind a round's run and keeps going through the close, the result
    /// screens, the next round and a stage end's briefing:
    /// <c>rounded-ended-by-reaching-tickets-limit.pcapng</c> writes it from t+74.571
    /// to the end of its capture, straight through both boundaries, and the only
    /// byte that ever changes is the closed-round count the three fields carry,
    /// which steps at a round's close and nowhere else.
    /// </para>
    /// <para>
    /// <b>A peer with no roster member gets nothing.</b> The record's state index is
    /// the peer's slot, so without a member there is no slot to name; the loop ends
    /// there, and a rejoin starts a fresh one.
    /// </para>
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="key">The remote address the peer was started under.</param>
    private async Task PublishPeerRoundStateAsync(PeerContext context, string key)
    {
        try
        {
            while (MatchRecordPeerUtils.IsAlive(context.Session))
            {
                await Task.Delay(PeerRoundStatePeriodMilliseconds);

                if (!MatchRecordPeerUtils.IsAlive(context.Session))
                {
                    return;
                }

                // The state index is the peer's own slot key plus one, which is how
                // the client's dispatcher picks the state block the fields belong
                // to (Rooms.PeerRoundStateBodiesUtils).
                var member = roster.Find(context.Remote.ToString())
                    ?? roster.Find(context.Session.DialBack.ToString());
                if (member is null)
                {
                    return;
                }

                try
                {
                    await context.Send(
                        UdpCommandConstants.RoomRecord,
                        Rooms.PeerRoundStateBodiesUtils.Body(
                            (byte)(member.SessionSlot + 1),
                            roundState.RoundsClosed),
                        roster.TakeRunOrdinal());
                }
                catch (Exception exception)
                {
                    logger.LogDebug(
                        exception,
                        "UDP {LocalPort}: the peer round state to {RemoteAddress} was not sent",
                        context.LocalPort,
                        context.Remote);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the peer round state to {RemoteAddress} ended",
                context.LocalPort,
                context.Remote);
        }
        finally
        {
            lock (gate)
            {
                peerRoundStateWriters?.Remove(key);
            }
        }
    }
}
