using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Writes the room itself to a peer for as long as that peer is in a briefing:
/// the rules-roster pair, which the room's stream carries in every phase, and the
/// room's own count, which is written when a peer is answered and again whenever
/// the room's population moves.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this closes.</b> The beat writes the rules pair once a round is
/// playing, and a briefing is not a round: a peer waiting to play was sent the
/// recorded host's sparse controls and state blobs and the room's ready set, and
/// nothing at all on the stream the room's own membership travels under. The
/// recorded host does not stop: <c>mgo2-game-started-on-briefing</c> writes
/// <c>0x0261 fe</c> under both classes 12 to 26 times per ten seconds from its
/// roster answer through its 198 s briefing and on into its round, and the two
/// captures that hold a join write it in the roster run itself
/// (<see cref="MatchRecordFramesUtils.RulesPair"/>).
/// </para>
/// <para>
/// <b>The count is the room's and not the host's.</b> Every captured one carries
/// the same number the Lobby's own details state for that room — one player of a
/// limit of seventeen in <c>started-on-briefing</c>, five joiners in
/// <c>joined-when-red-team</c> — and it is written again when that number moves
/// and not on every tick, which is why a peer that joins into an unchanged room
/// sees it once (<see cref="MatchRecordBodiesUtils.RoomCount"/>).
/// </para>
/// <para>
/// <b>It does not fault.</b> It runs beside the waits for the round rather than
/// inside them, so a failed send says what ending it says anyway: the peer is
/// gone.
/// </para>
/// </remarks>
/// <param name="roster">The room's roster, whose count is what the count record carries.</param>
/// <param name="roundState">The room's round, whose opening ends the publication.</param>
/// <param name="sequences">
/// The stream counters a session record's fourth byte comes from: the room's
/// count is one of those and takes its own stream's next value.
/// </param>
/// <param name="logger">Logger of this service.</param>
public sealed class BriefingRoomPublishService(
    RoomRosterService roster,
    MatchRoundStateService roundState,
    MatchRecordSequenceService sequences,
    ILogger<BriefingRoomPublishService> logger)
{
    /// <summary>
    /// Spacing between rules pairs. The captures write the pair 2.0 to 2.5 s
    /// apart in a briefing, which is the operations tick the briefing's clock and
    /// its ready set are written on.
    /// </summary>
    public const int PublishPeriodMilliseconds = BriefingReadyPublishService.PublishPeriodMilliseconds;

    /// <summary>Sent count of a peer nothing has been told yet.</summary>
    private const int NoCountSent = -1;

    /// <summary>
    /// Writes the room to one peer, on the briefing's tick, until the round opens
    /// or the peer goes.
    /// </summary>
    /// <param name="context">Context the records are written through.</param>
    public async Task PublishAsync(PeerContext context)
    {
        var session = context.Session;
        var sentCount = NoCountSent;

        while (MatchRecordPeerUtils.IsAlive(session) && !roundState.IsOpen)
        {
            try
            {
                // The pair goes out on the first pass as well and not only on a
                // tick: the captures write it in the roster run itself, so a peer
                // answered its roster is told the room's stream at once.
                foreach (var record in MatchRecordFramesUtils.RulesPair(roundState.Generation))
                {
                    await context.Send(record.Type, record.Body, record.FourthByte);
                }

                sentCount = await PublishCountAsync(context, sentCount);
            }
            catch (Exception exception)
            {
                logger.LogDebug(
                    exception,
                    "UDP {LocalPort}: the room's rules and count to {RemoteAddress} stopped on a failed send",
                    context.LocalPort,
                    session.RemoteAddress);
                return;
            }

            // The tick ends the moment the round opens: this task is awaited
            // before the beat, and a period slept out after the run was written
            // would hold the beat's first frame behind it.
            await roundState.WaitBriefingTickAsync(session, PublishPeriodMilliseconds);
        }

        logger.LogDebug(
            "UDP {LocalPort}: the room's rules and count to {RemoteAddress} ended",
            context.LocalPort,
            session.RemoteAddress);
    }

    /// <summary>
    /// Writes the room's count when it has moved since this peer was last told,
    /// and returns the count the peer now holds.
    /// </summary>
    /// <remarks>
    /// The room's player table is 24 keys wide, so a room's count is one byte with
    /// room to spare (<see cref="RoomRosterService.SessionSlotCount"/>).
    /// </remarks>
    /// <param name="context">Context the record is written through.</param>
    /// <param name="sentCount">Count this peer was last told, or <see cref="NoCountSent"/>.</param>
    /// <returns>The count the peer has now been told.</returns>
    private async Task<int> PublishCountAsync(PeerContext context, int sentCount)
    {
        var count = roster.JoinerCount;
        if (count == sentCount)
        {
            return sentCount;
        }

        var generation = roundState.Generation;
        logger.LogDebug(
            "UDP {LocalPort}: telling {RemoteAddress} the room holds {PlayerCount} player(s) on 0x{Identifier:x4}",
            context.LocalPort,
            context.Remote,
            count,
            generation.RoomCount);

        await context.Send(
            generation.RoomCount,
            MatchRecordBodiesUtils.RoomCount((byte)count),
            sequences.Take(generation.RoomCount));
        return count;
    }
}
