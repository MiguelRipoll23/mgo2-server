using Mgo2Server.GameplayServer.MatchRecords;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Tracks the room's players and the briefing ready state each of them has
/// reported, and answers the one question the round opener asks: is everyone
/// ready?
/// </summary>
/// <remarks>
/// <para>
/// <b>Who is in the room is not kept here.</b> The roster already owns that
/// (<see cref="RoomRosterService"/>), and a second population would be a second
/// thing to keep true: a ready state for a player who has left, or a player who
/// never appears because one of the two lists was not told. This service holds
/// only the values, and reads the room's population from the roster.
/// </para>
/// <para>
/// <b>What is measured, and what is not.</b> The client reports its briefing
/// state as <c>e1 &lt;byte&gt;</c> on the round's payload identifier, and the host
/// publishes one <c>e0 &lt;byte&gt;</c> per tracked player on the same
/// identifier, each under its own fourth byte. That pairing holds across three
/// captures, which is what makes this a room-wide publication rather than a
/// per-peer answer.
/// </para>
/// <para>
/// The <em>fourth byte</em> is read as the player's place in the publication:
/// the recorded host numbers them from zero and keeps a player's number while
/// the briefing lasts, so the set reads as a list that grows as players report
/// (<c>docs/protocol/BRIEFING_FLOW.md</c>). Nothing on the wire names it, so the
/// assignment is **[I]**; what is **[V]** is that each record carries one
/// player's own byte and that the set is rewritten on every report.
/// </para>
/// </remarks>
/// <param name="roster">Roster of the room, which owns who is in it.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class PlayerReadyService(RoomRosterService roster, ILogger<PlayerReadyService> logger)
{
    /// <summary>
    /// One player's briefing state: the byte it reported and its place in the
    /// publication.
    /// </summary>
    /// <param name="RemoteAddress">Endpoint the player is reached at.</param>
    /// <param name="RosterIndex">
    /// Slot the player fills on the room roster, or <c>null</c> when the report
    /// arrived from an endpoint the roster has no entry for.
    /// </param>
    /// <param name="Value">Byte the player reported.</param>
    /// <param name="PublicationIndex">Fourth byte its record travels under.</param>
    public sealed record ReadyState(
        string RemoteAddress,
        sbyte? RosterIndex,
        byte Value,
        int PublicationIndex);

    private readonly Lock gate = new();
    private readonly Dictionary<string, ReadyState> states = [];
    private int nextPublicationIndex;

    /// <summary>
    /// Whether every player in the room has reported itself ready, and the room
    /// has a player to be ready.
    /// </summary>
    /// <remarks>
    /// An empty room is not a ready room: the round opens for players, and a
    /// room whose last player has left has nobody to open it for. That is what
    /// keeps a departing peer from starting a round on its way out.
    /// </remarks>
    public bool AllReady
    {
        get
        {
            var members = roster.Snapshot();
            if (members.Count == 0)
            {
                return false;
            }

            lock (gate)
            {
                return members.All(member => states.ContainsKey(member.RemoteAddress));
            }
        }
    }

    /// <summary>Players in the room who have reported themselves ready.</summary>
    public int ReadyCount
    {
        get
        {
            lock (gate)
            {
                return states.Count;
            }
        }
    }

    /// <summary>Players in the room who have not reported themselves ready yet.</summary>
    public int PendingCount => Math.Max(0, roster.JoinerCount - ReadyCount);

    /// <summary>
    /// Records what a player reported, and reports whether that is news to the
    /// room.
    /// </summary>
    /// <remarks>
    /// A client re-reports until it is answered, so the same state arrives more
    /// than once and a re-report is not a second player. It is news only when
    /// the byte it carries has changed, which is what keeps the publication from
    /// being rewritten for a reply the room has already been given.
    /// </remarks>
    /// <param name="remoteAddress">Endpoint the report came from.</param>
    /// <param name="value">Byte the report carried.</param>
    /// <returns><c>true</c> when the room has something it has not been told.</returns>
    public bool Note(string remoteAddress, byte value)
    {
        lock (gate)
        {
            if (states.TryGetValue(remoteAddress, out var existing))
            {
                if (existing.Value == value)
                {
                    logger.LogDebug(
                        "{RemoteAddress} re-reported ready state {Value}; the room already has it",
                        remoteAddress,
                        value);
                    return false;
                }

                states[remoteAddress] = existing with { Value = value };
                logger.LogInformation(
                    "{RemoteAddress} changed its ready state from {PreviousValue} to {Value}",
                    remoteAddress,
                    existing.Value,
                    value);
                return true;
            }

            var rosterIndex = FindRosterIndex(remoteAddress);
            var state = new ReadyState(remoteAddress, rosterIndex, value, nextPublicationIndex++);
            states[remoteAddress] = state;
            logger.LogInformation(
                "{RemoteAddress} reports itself ready with {Value}: {ReadyCount} of {PlayerCount} players "
                + "are ready, at publication index {PublicationIndex}",
                remoteAddress,
                value,
                states.Count,
                roster.JoinerCount,
                state.PublicationIndex);
            return true;
        }
    }

    /// <summary>Drops a player's state, because the player has left the room.</summary>
    /// <param name="remoteAddress">Endpoint the player was filed under.</param>
    /// <returns><c>true</c> when the player had reported a state.</returns>
    public bool Forget(string remoteAddress)
    {
        lock (gate)
        {
            if (!states.Remove(remoteAddress))
            {
                return false;
            }

            logger.LogInformation(
                "{RemoteAddress} left the briefing with a ready state recorded; {ReadyCount} player(s) ready",
                remoteAddress,
                states.Count);
            return true;
        }
    }

    /// <summary>
    /// Starts the room's next briefing: nobody is ready, and the publication
    /// numbers its players from the beginning again.
    /// </summary>
    /// <remarks>
    /// A round is preceded by its own briefing — the captures open a round, play
    /// it, then rewrite the ready set for the round after it — so the states
    /// belong to one briefing and not to the room.
    /// </remarks>
    public void BeginNextBriefing()
    {
        lock (gate)
        {
            if (states.Count == 0)
            {
                nextPublicationIndex = 0;
                return;
            }

            logger.LogInformation(
                "Beginning the next briefing; {ReadyCount} ready state(s) from the last one are cleared",
                states.Count);
            states.Clear();
            nextPublicationIndex = 0;
        }
    }

    /// <summary>
    /// The ready set as it goes on the wire: one record per player that has
    /// reported, in publication order, each carrying that player's own byte.
    /// </summary>
    /// <param name="generation">Identifier set the publication travels under.</param>
    /// <returns>The records in send order.</returns>
    public List<RosterRecord> BuildPublication(MatchRecordGenerationUtils generation)
    {
        List<ReadyState> ordered;
        lock (gate)
        {
            ordered = [.. states.Values.OrderBy(state => state.PublicationIndex)];
        }

        return
        [
            .. ordered.Select(state => new RosterRecord(
                generation.MatchStartPayload,
                MatchStartBodiesUtils.MatchStartPayload(state.Value),
                (byte)state.PublicationIndex)),
        ];
    }

    /// <summary>Whether a player has reported itself ready.</summary>
    /// <param name="remoteAddress">Endpoint the player is filed under.</param>
    /// <returns><c>true</c> when the player has a state recorded.</returns>
    public bool HasReported(string remoteAddress)
    {
        lock (gate)
        {
            return states.ContainsKey(remoteAddress);
        }
    }

    /// <summary>
    /// The byte a player reported, which is the byte the round's payload record
    /// echoes back to it.
    /// </summary>
    /// <param name="remoteAddress">Endpoint the player is filed under.</param>
    /// <param name="value">The byte the player reported.</param>
    /// <returns><c>true</c> when the player has a state recorded.</returns>
    public bool TryGetReported(string remoteAddress, out byte value)
    {
        lock (gate)
        {
            if (states.TryGetValue(remoteAddress, out var state))
            {
                value = state.Value;
                return true;
            }
        }

        value = MatchStartBodiesUtils.DefaultReadinessReportValue;
        return false;
    }

    /// <summary>
    /// The roster slot a report's endpoint fills, or <c>null</c> when the roster
    /// has no entry for it.
    /// </summary>
    /// <param name="remoteAddress">Endpoint the report came from.</param>
    /// <returns>The slot, or <c>null</c>.</returns>
    private sbyte? FindRosterIndex(string remoteAddress)
    {
        var member = roster.Find(remoteAddress);
        if (member is not null)
        {
            return member.RosterIndex;
        }

        // A report from an endpoint the roster never registered is still
        // counted: the client is in the room's channel, and dropping its state
        // would leave the room waiting on a player that has already answered.
        logger.LogWarning(
            "Ready state from {RemoteAddress} has no roster entry; it is counted but its slot is unknown",
            remoteAddress);
        return null;
    }
}
