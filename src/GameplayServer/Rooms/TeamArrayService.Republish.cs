using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The pair's own clock: the periodic republish of the room's side assignment to one
/// peer, and the copies of it that cost the exchange nothing.
/// </summary>
/// <remarks>
/// Split from <see cref="TeamArrayService"/> so neither file exceeds the size a source
/// file is allowed. It is the same service: this half is the pair written on a timer,
/// the other is its answers to a client's own records.
/// </remarks>
public sealed partial class TeamArrayService
{
    /// <summary>
    /// How often a peer is sent the pair again while a round plays: the working capture
    /// repeats it about every two thirds of a second, and this repository's largest
    /// capture writes the pair 1 424 times over its session.
    /// </summary>
    /// <remarks>
    /// <b>Measured.</b> <c>actions.pcapng</c> writes the two twenty-six byte records
    /// 395 times over a round, in pairs whose gap is 0.0 s and whose publication gap
    /// clusters at 0.60–0.67 s. This host wrote 33, all of them answers to something a
    /// client sent, so a peer that stopped asking stopped being told which side the
    /// room's bots are on.
    /// </remarks>
    public const int RepublishIntervalMilliseconds = 650;

    /// <summary>Guards the clock, the sequence and the pairs already written.</summary>
    private readonly object republishGate = new();

    /// <summary>
    /// When each peer's next pair is due, in monotonic milliseconds.
    /// </summary>
    /// <remarks>
    /// <b>Per peer, because the pair is per peer.</b> A clock the whole service shares
    /// lets the first peer that asks take the tick and leaves the second unanswered for
    /// as long as both are in the room — the sequence the pair carries is the peer's own,
    /// so the pair has to be published on the peer's own clock.
    /// </remarks>
    private readonly Dictionary<string, long> nextRepublish = [];

    /// <summary>
    /// The pair last written to a peer, so the copies behind it re-send those records
    /// rather than build and number new ones.
    /// </summary>
    /// <remarks>
    /// <b>A copy is the same record, not a new one with the same body.</b> The fourth
    /// byte belongs to the record: a record written again carries the byte it was first
    /// given, which is what the reference's 484 repeated transitions on the roster
    /// stream are. Rebuilding the pair and numbering it again spends a value per tick
    /// and turns the stream inside the round
    /// (<see cref="MatchRecordSequenceService"/>: a value is taken once, where the record
    /// is built, and the copy carries it).
    /// </remarks>
    private readonly Dictionary<string, HeldPair> republished = [];

    /// <summary>
    /// Republishes the room's side assignment to one peer on its own clock: the pair
    /// it has not answered is repeated under its own sequence, and the pair at the
    /// sequence the client now holds is written once it has moved past it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A tick repeats an unanswered record; it does not number a new one.</b> The
    /// image re-sends a record under the byte it already carries until the peer
    /// answers it (<c>P2P_FlushChannelQueues</c>, <c>0x285b58</c>). A tick that
    /// advanced the sequence every 650 ms walked the loop from <c>1f</c> to
    /// <c>4e</c> while the client was still answering <c>1f</c>; the pair behind it
    /// was never confirmed, the client's echoes were refused, and the operations
    /// stream it stopped acknowledging at that boundary never carried the round's
    /// result to the screen. The round's open confirms the peer's answer and writes
    /// the next sequence (<see cref="MatchRecords.MatchStartRunService.ConfirmEntryAsync"/>),
    /// which is where the capture puts it.
    /// </para>
    /// <para>
    /// It is per peer and not fanned out: the sequence is what a client answers, and
    /// every peer holds its own. The room's rows are read only when a pair is built,
    /// not on every repeat, so a repeat costs the room's rows nothing.
    /// </para>
    /// </remarks>
    /// <param name="context">Context of the peer the pair is written to.</param>
    public async Task RepublishAsync(PeerContext context)
    {
        var now = Environment.TickCount64;
        var key = context.Remote.ToString();
        var clientSequence = roster.PairSequenceOf(key);

        lock (republishGate)
        {
            if (nextRepublish.TryGetValue(key, out var due) && now < due)
            {
                return;
            }

            nextRepublish[key] = now + RepublishIntervalMilliseconds;
        }

        List<RosterRecord>? heldPair = null;
        byte heldSequence = 0;
        lock (republishGate)
        {
            if (republished.TryGetValue(key, out var held))
            {
                if (clientSequence == held.Sequence)
                {
                    heldPair = [.. held.Pair];
                    heldSequence = held.Sequence;
                }
                else
                {
                    // The client has moved past this pair — the open confirmed it — so
                    // the next tick builds the pair at the sequence it now holds rather
                    // than repeating a sequence it has already answered.
                    republished.Remove(key);
                    return;
                }
            }
        }

        if (heldPair is not null)
        {
            logger.LogDebug(
                "UDP {LocalPort}: repeating the lobby-slot arrays at sequence {Sequence:x2} to {RemoteAddress}",
                context.LocalPort,
                heldSequence,
                context.Remote);
            await RosterRunSendUtils.SendAsync(context, heldPair);
            return;
        }

        var slots = await lobbySlots.ReadAsync();
        var sequence = clientSequence;
        var (newTeams, newOccupancy) = TeamArrayFramesUtils.BuildPair(sequence, slots);
        var ordinals = roster.TakeRunOrdinals(2);
        List<RosterRecord> pair =
        [
            new(UdpCommandConstants.RoomRecord, newTeams, ordinals[0]),
            new(UdpCommandConstants.RoomRecord, newOccupancy, ordinals[1]),
        ];

        logger.LogDebug(
            "UDP {LocalPort}: republishing the lobby-slot arrays at sequence {Sequence:x2} to {RemoteAddress}",
            context.LocalPort,
            sequence,
            context.Remote);

        lock (republishGate)
        {
            republished[key] = new HeldPair(sequence, pair);
        }

        await RosterRunSendUtils.SendAsync(context, pair);
    }

    /// <summary>Drops every pair held for a peer, because the room has emptied.</summary>
    private void ResetRepublished()
    {
        lock (republishGate)
        {
            republished.Clear();
            nextRepublish.Clear();
        }
    }

    /// <summary>Forgets the pair held for a peer that is starting over.</summary>
    /// <param name="remoteAddress">Endpoint the peer was filed under.</param>
    private void ForgetRepublished(string remoteAddress)
    {
        lock (republishGate)
        {
            republished.Remove(remoteAddress);
            nextRepublish.Remove(remoteAddress);
        }
    }

    /// <summary>A pair written to a peer, held until the peer answers it.</summary>
    /// <param name="Sequence">Sequence the pair carries.</param>
    /// <param name="Pair">The two records, as they went out.</param>
    private sealed record HeldPair(byte Sequence, IReadOnlyList<RosterRecord> Pair);
}
