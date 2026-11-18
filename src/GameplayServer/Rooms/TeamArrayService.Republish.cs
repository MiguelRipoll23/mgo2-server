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

    /// <summary>
    /// Copies of one pair before a new sequence — and so a new fourth byte — is spent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured on the largest capture</b> (<c>.pcapngs/pcapng2/pcapng2.pcapng</c>,
    /// 526 s, 384 890 datagrams): the roster stream <c>0x001</c> writes 1 424 own
    /// records under 256 distinct fourth bytes, so each value is written about 5.6
    /// times and only 0.49 values a second are spent. This host took two new values on
    /// every tick — 3.1 a second, six times the reference — which turns the byte inside
    /// a round of a session the capture runs for 526 s.
    /// </para>
    /// <para>
    /// Six writes per pair, which is two values every 3.9 s at the 650 ms tick — 0.51
    /// values a second against the reference's 0.49, and six writes per value against
    /// its 5.6. The safe direction is more copies and not fewer: a peer that is behind
    /// waits for a value, while a peer handed a byte it has already passed refuses the
    /// record.
    /// </para>
    /// </remarks>
    private const int RepublishCopies = 6;

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
    /// Sequence each peer's next pair carries, advanced with
    /// <see cref="TeamArrayFramesUtils.NextSequence"/>: the capture's own runs read
    /// <c>85 00</c>, <c>85 01</c>, <c>85 02</c>, so the repeats move the loop rather
    /// than repeat one value.
    /// </summary>
    /// <remarks>
    /// Held per peer for the reason the clock is: the sequence is what one client answers,
    /// and a room of two where the peers share it hands the second client a number the
    /// first has already answered.
    /// </remarks>
    private readonly Dictionary<string, byte> nextSequence = [];

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
    /// itself while its copies last, and a freshly numbered one when they do not.
    /// </summary>
    /// <remarks>
    /// It is per peer and not fanned out: the sequence is what a client answers, and every
    /// peer holds its own. The room's rows are read on every tick, so the pair is rebuilt
    /// the moment a player's side changes or the room's membership moves — a new value
    /// for a record whose body is news is what the capture spends its values on.
    /// </remarks>
    /// <param name="context">Context of the peer the pair is written to.</param>
    public async Task RepublishAsync(PeerContext context)
    {
        var now = Environment.TickCount64;
        var key = context.Remote.ToString();
        var slots = await lobbySlots.ReadAsync();

        IReadOnlyList<RosterRecord>? repeat = null;
        byte sequence = 0;
        lock (republishGate)
        {
            if (nextRepublish.TryGetValue(key, out var due) && now < due)
            {
                return;
            }

            nextRepublish[key] = now + RepublishIntervalMilliseconds;

            if (republished.TryGetValue(key, out var held) && held.Writes < RepublishCopies)
            {
                var (teams, occupancy) = TeamArrayFramesUtils.BuildPair(held.Sequence, slots);
                if (teams.AsSpan().SequenceEqual(held.Pair[0].Body)
                    && occupancy.AsSpan().SequenceEqual(held.Pair[1].Body))
                {
                    republished[key] = held with { Writes = held.Writes + 1 };
                    repeat = held.Pair;
                }
            }

            if (repeat is null)
            {
                sequence = nextSequence.TryGetValue(key, out var next)
                    ? next
                    : TeamArrayFramesUtils.OpeningSequence;
                nextSequence[key] = TeamArrayFramesUtils.NextSequence(sequence);
            }
        }

        if (repeat is not null)
        {
            logger.LogDebug(
                "UDP {LocalPort}: repeating the lobby-slot arrays under the byte they already carry to "
                + "{RemoteAddress}",
                context.LocalPort,
                context.Remote);
            await RosterRunSendUtils.SendAsync(context, repeat);
            return;
        }

        var (newTeams, newOccupancy) = TeamArrayFramesUtils.BuildPair(sequence, slots);
        var ordinals = roster.TakeRunOrdinals(2);
        List<RosterRecord> pair =
        [
            new(UdpCommandConstants.PlayerProfile, newTeams, ordinals[0]),
            new(UdpCommandConstants.PlayerProfile, newOccupancy, ordinals[1]),
        ];

        logger.LogDebug(
            "UDP {LocalPort}: republishing the lobby-slot arrays at sequence {Sequence:x2} to {RemoteAddress}",
            context.LocalPort,
            sequence,
            context.Remote);

        lock (republishGate)
        {
            republished[key] = new HeldPair(sequence, pair, 1);
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
            nextSequence.Clear();
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
            nextSequence.Remove(remoteAddress);
        }
    }

    /// <summary>A pair written to a peer, and how many times it has been written.</summary>
    /// <param name="Sequence">Sequence the pair carries.</param>
    /// <param name="Pair">The two records, as they went out.</param>
    /// <param name="Writes">Copies written so far, the first included.</param>
    private sealed record HeldPair(byte Sequence, IReadOnlyList<RosterRecord> Pair, int Writes);
}
