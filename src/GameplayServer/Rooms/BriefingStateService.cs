using Mgo2Server.GameplayServer.MatchRecords;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Tracks the room's players and the two states a briefing hears from each of
/// them — the map-loaded report and the ready record — and answers the one
/// question the round opener asks: is everyone ready?
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
/// <b>Two records, and only the second one is the ready.</b> A client sends
/// <c>e1 &lt;slot&gt;</c> on the round's payload identifier when it has loaded the
/// room and its map — it is on the briefing screen, and nothing more is claimed
/// by it (<see cref="JoinReport"/>) — and <c>1f &lt;slot&gt; 01</c> on the
/// operations stream when the player in front of it <em>changes its briefing state
/// to ready</em> (<see cref="MatchRecords.BriefingReadyBodiesUtils"/>). The two
/// are not interchangeable: <c>mgo2-game-joined-when-red-team-then-briefing</c> reports
/// joins at t+57.656 and readies at t+112.750, and again reports at t+870.222
/// for a ready at t+844.858 — the ready before the report in that second
/// briefing, so a room gated on the reports would open its round from the wrong
/// record. What gates the round is
/// <see cref="AllReady"/>, which reads the ready records.
/// </para>
/// <para>
/// <b>What is measured, and what is not.</b> The client reports the map it has
/// loaded as <c>e1 &lt;byte&gt;</c> on the round's payload identifier, and the host
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
public sealed class BriefingStateService(RoomRosterService roster, ILogger<BriefingStateService> logger)
{
    /// <summary>
    /// One player's map-loaded report: the byte it reported and its place in the
    /// publication.
    /// </summary>
    /// <param name="RemoteAddress">Endpoint the player is reached at.</param>
    /// <param name="RosterIndex">
    /// Slot the player fills on the room roster, or <c>null</c> when the report
    /// arrived from an endpoint the roster has no entry for.
    /// </param>
    /// <param name="Value">Byte the player reported, which is its own slot.</param>
    /// <param name="PublicationIndex">Fourth byte its record travels under.</param>
    public sealed record JoinReport(
        string RemoteAddress,
        sbyte? RosterIndex,
        byte Value,
        int PublicationIndex);

    /// <summary>
    /// One player's ready record: the slot the record named and the state it
    /// carried. The slot is the player's own, and it is the bit this player
    /// contributes to <see cref="ReadyMask"/>.
    /// </summary>
    /// <param name="RemoteAddress">Endpoint the player is reached at.</param>
    /// <param name="Slot">Slot the record named, which is the player's own.</param>
    /// <param name="State">Briefing state the record carried.</param>
    public sealed record BriefingReady(string RemoteAddress, byte Slot, byte State);

    private readonly Lock gate = new();
    private readonly Dictionary<string, JoinReport> joinReports = [];
    private readonly Dictionary<string, BriefingReady> readies = [];
    private int nextPublicationIndex;

    /// <summary>
    /// Whether every player in the room has readied, and the room has a player to
    /// be ready.
    /// </summary>
    /// <remarks>
    /// <b>It reads the ready records and not the join reports.</b> A room is
    /// started when its players have changed their briefing state to ready, which
    /// is the record <c>OperationsStreamHandler</c> takes; the report a client
    /// sends when it has loaded says it is there and not that it is waiting to
    /// play. The capture separates the two by up to half a minute
    /// (<see cref="BriefingReady"/>), and only a record whose state is the ready
    /// counts (<see cref="IsReady"/>), so a state nothing has measured leaves the
    /// room waiting for the briefing's own deadline — or, in a room of one, waiting
    /// for the player in it, because a briefing with nobody else in it never ends
    /// (<see cref="MatchRecords.MatchStartRunService"/>).
    /// <para>
    /// An empty room is not a ready room: the round opens for players, and a
    /// room whose last player has left has nobody to open it for. That is what
    /// keeps a departing peer from starting a round on its way out.
    /// </para>
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
                return members.All(member => HasReadied(member.RemoteAddress));
            }
        }
    }

    /// <summary>
    /// The ready set as it goes on the countdown: one bit per player that has
    /// readied, at the slot that player's own record named.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is <c>1 &lt;&lt; slot</c> over the ready records, and the countdown is
    /// what it is read for.</b> Measured over
    /// <c>mgo2-game-joined-when-red-team-then-briefing</c>: the briefing's countdown
    /// carries <c>40 00</c>, <c>c0 00</c>, <c>c2 00</c>, <c>ca 00</c> and finally
    /// <c>ea 01</c> — bits 6, then 6 and 7, then 1 with them, then 3, then 8, and the
    /// closing zero with bit 5 — and the slots its roster entry for each of those
    /// bits names at the same instant are BOT-01, BOT-02, Aozori, haw9, BOT-03 and
    /// the reporting client at slot 5. The same room's second briefing
    /// (<c>0b 30 00 02 7c 00 00</c>, bits 1, 10, 11, 12, 13 and 14) is the same
    /// reading with the room's slots moved along, and its host's own bots are readied
    /// in the record that adds them.
    /// </para>
    /// <para>
    /// The reports are a different set and a narrower one:
    /// <see cref="JoinedMask"/> over the same room is <c>2a</c> — slots 1, 3 and 5,
    /// the clients that sent a report — because a bot the host adds is ready without
    /// ever reporting. Sixteen bits is the width the countdown record has, so a slot
    /// above 15 has no bit to set; the reference's rooms hold at most fourteen.
    /// </para>
    /// </remarks>
    public ushort ReadyMask
    {
        get
        {
            lock (gate)
            {
                var mask = 0;
                foreach (var ready in readies.Values)
                {
                    if (IsReady(ready) && ready.Slot is >= 1 and <= 15)
                    {
                        mask |= 1 << ready.Slot;
                    }
                }

                return (ushort)mask;
            }
        }
    }

    /// <summary>Players in the room that have readied.</summary>
    public int BriefingReadyCount
    {
        get
        {
            lock (gate)
            {
                return readies.Values.Count(IsReady);
            }
        }
    }

    /// <summary>Players in the room that have not readied yet.</summary>
    public int PendingBriefingReadyCount => Math.Max(0, roster.JoinerCount - BriefingReadyCount);

    /// <summary>
    /// Players in the room that have reported the map they loaded, counted over
    /// those reports.
    /// </summary>
    /// <remarks>
    /// <b>It is not a readiness count and does not open anything.</b> What gates a
    /// round is <see cref="AllReady"/>, which reads the ready records; this is the
    /// count of clients that have reached the room's briefing screen. The two
    /// callers that read it say so: the briefing's countdown starts moving once a
    /// second player has reported (<see cref="MatchRecords.MatchStartRunService"/>),
    /// and a briefing whose reporters have all left opens nothing.
    /// </remarks>
    public int JoinedCount
    {
        get
        {
            lock (gate)
            {
                return joinReports.Count;
            }
        }
    }

    /// <summary>Players in the room, counted over the roster that owns who is in it.</summary>
    public int PlayerCount => roster.JoinerCount;

    /// <summary>Players in the room that have not reported a map yet.</summary>
    public int PendingJoinCount => Math.Max(0, roster.JoinerCount - JoinedCount);

    /// <summary>
    /// Records a player's map-loaded report, and reports whether that is news to
    /// the room.
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
    public bool NoteJoinReport(string remoteAddress, byte value)
    {
        lock (gate)
        {
            if (joinReports.TryGetValue(remoteAddress, out var existing))
            {
                if (existing.Value == value)
                {
                    logger.LogDebug(
                        "{RemoteAddress} re-reported map-loaded state {Value}; the room already has it",
                        remoteAddress,
                        value);
                    return false;
                }

                joinReports[remoteAddress] = existing with { Value = value };
                logger.LogInformation(
                    "{RemoteAddress} changed its map-loaded state from {PreviousValue} to {Value}",
                    remoteAddress,
                    existing.Value,
                    value);
                return true;
            }

            var rosterIndex = FindRosterIndex(remoteAddress);
            var state = new JoinReport(remoteAddress, rosterIndex, value, nextPublicationIndex++);
            joinReports[remoteAddress] = state;
            logger.LogInformation(
                "{RemoteAddress} reports its map-loaded state with {Value}: {ReportedCount} of {PlayerCount} "
                + "players have reported, at publication index {PublicationIndex}",
                remoteAddress,
                value,
                joinReports.Count,
                roster.JoinerCount,
                state.PublicationIndex);
            return true;
        }
    }

    /// <summary>
    /// Records a player's ready record, and reports whether that is news to the
    /// room.
    /// </summary>
    /// <remarks>
    /// <b>A re-report is not a second player, and a state change is news.</b> The
    /// record is what the host publishes the room's ready set from, so a record
    /// that repeats what the room has already been told changes nothing
    /// (<see cref="ReadyMask"/>), and one that moves a player off
    /// <see cref="MatchRecords.BriefingReadyBodiesUtils.ReadyState"/> takes its bit
    /// back out of the set.
    /// </remarks>
    /// <param name="remoteAddress">Endpoint the record came from.</param>
    /// <param name="slot">Slot the record named, which is the player's own.</param>
    /// <param name="state">Briefing state the record carried.</param>
    /// <returns><c>true</c> when the room has something it has not been told.</returns>
    public bool NoteBriefingReady(string remoteAddress, byte slot, byte state)
    {
        lock (gate)
        {
            if (readies.TryGetValue(remoteAddress, out var existing))
            {
                if (existing.Slot == slot && existing.State == state)
                {
                    logger.LogDebug(
                        "{RemoteAddress} re-reported briefing state {State} for slot {Slot}; the room "
                        + "already has it",
                        remoteAddress,
                        state,
                        slot);
                    return false;
                }

                readies[remoteAddress] = existing with { Slot = slot, State = state };
                logger.LogInformation(
                    "{RemoteAddress} moved its briefing state from {PreviousState} to {State} for slot "
                    + "{Slot}",
                    remoteAddress,
                    existing.State,
                    state,
                    slot);
                return true;
            }

            readies[remoteAddress] = new BriefingReady(remoteAddress, slot, state);
            logger.LogInformation(
                "{RemoteAddress} reports briefing state {State} for slot {Slot}: {BriefingReadyCount} of "
                + "{PlayerCount} player(s) in the room have readied",
                remoteAddress,
                state,
                slot,
                BriefingReadyCount,
                roster.JoinerCount);
            return true;
        }
    }

    /// <summary>
    /// Drops both of a player's states, because the player has left the room.
    /// </summary>
    /// <remarks>
    /// The ready record goes with the report: a player that has left is not a
    /// player the room is waiting on, and a bit left in the ready set would keep
    /// <see cref="ReadyMask"/> naming a slot nobody fills — and, with the gate on
    /// the readies, hold the round open for a player that is gone.
    /// </remarks>
    /// <param name="remoteAddress">Endpoint the player was filed under.</param>
    /// <returns><c>true</c> when the player had reported a state.</returns>
    public bool Forget(string remoteAddress)
    {
        var readied = false;
        lock (gate)
        {
            readied = readies.Remove(remoteAddress);
            if (!joinReports.Remove(remoteAddress))
            {
                if (readied)
                {
                    logger.LogInformation(
                        "{RemoteAddress} left the briefing with a ready state recorded; {BriefingReadyCount} "
                        + "player(s) in the room have readied",
                        remoteAddress,
                        readies.Count);
                }

                return readied;
            }

            logger.LogInformation(
                "{RemoteAddress} left the briefing with a state recorded; {ReportedCount} player(s) have "
                + "reported",
                remoteAddress,
                joinReports.Count);
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
            if (joinReports.Count == 0 && readies.Count == 0)
            {
                nextPublicationIndex = 0;
                return;
            }

            logger.LogInformation(
                "Beginning the next briefing; {ReportedCount} report(s) and {BriefingReadyCount} ready state(s) "
                + "from the last one are cleared",
                joinReports.Count,
                readies.Count);
            joinReports.Clear();
            readies.Clear();
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
        List<JoinReport> ordered;
        lock (gate)
        {
            ordered = [.. joinReports.Values.OrderBy(state => state.PublicationIndex)];
        }

        return
        [
            .. ordered.Select(state => new RosterRecord(
                generation.MatchStartPayload,
                MatchStartBodiesUtils.MatchStartPayload(state.Value),
                (byte)state.PublicationIndex)),
        ];
    }

    /// <summary>
    /// The room's reporting players as the join state answer's bit set: one bit
    /// per player that has reported, at the slot the player's own report byte
    /// names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bit is the player's own reported byte, and the solo capture is what
    /// settles it.</b> A host answers the join state record in four bytes under
    /// class 6, and its first byte is the set of the room's reporters. Read as
    /// the publication order it fits the rooms whose reporters arrived in slot
    /// order — <c>mgo2-game-started-on-briefing</c> answers <c>02</c>, <c>06</c>,
    /// <c>0e</c> and <c>1e</c> as its players report, then <c>16</c> once the
    /// third has left — but it does not fit a room of one whose report carried
    /// <c>03</c>: <c>mgo2-started-briefing-then-solo-game</c> is exactly that
    /// room, and its host answers <c>08</c>, which is bit 3 and not bit 1.
    /// </para>
    /// <para>
    /// Read as <c>1 &lt;&lt; reportedByte</c> every briefing fits:
    /// <c>started-on-briefing</c>'s reports are slots 1, 2, 3 and 4
    /// (<c>02</c>, <c>06</c>, <c>0e</c>, <c>1e</c>, then <c>16</c> with slot 3
    /// gone), <c>joined-when-red-team</c>'s <c>2a</c> is slots 1, 3 and 5, and
    /// the solo capture's single reporter is slot 3. The slots the arrays name
    /// are the room's 24-entry table keys and are a different field; the byte
    /// the report carries is the player's own slot, which is what the round's
    /// run payload echoes back.
    /// </para>
    /// <para>
    /// So the value is derived from the room's own reports and is not a
    /// constant: <see cref="MatchStartBodiesUtils.JoinStateAnswer"/> took a
    /// hard-coded two for a room of one, which is only the first reporter's
    /// correct value by coincidence. A report outside 1…7 has no bit in the
    /// byte, and one that names no such slot contributes none.
    /// </para>
    /// </remarks>
    public byte JoinedMask
    {
        get
        {
            lock (gate)
            {
                var mask = 0;
                foreach (var state in joinReports.Values)
                {
                    // The report's own byte is the player's slot, and the set is
                    // one bit per slot. Eight players fill the byte; a slot above
                    // 7 has no bit to set, which is the width the record has.
                    if (state.Value is >= 1 and <= 7)
                    {
                        mask |= 1 << state.Value;
                    }
                }

                return (byte)mask;
            }
        }
    }

    /// <summary>Whether a player has reported the map it loaded.</summary>
    /// <param name="remoteAddress">Endpoint the player is filed under.</param>
    /// <returns><c>true</c> when the player has a report recorded.</returns>
    public bool HasJoined(string remoteAddress)
    {
        lock (gate)
        {
            return joinReports.ContainsKey(remoteAddress);
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
            if (joinReports.TryGetValue(remoteAddress, out var state))
            {
                value = state.Value;
                return true;
            }
        }

        value = MatchStartBodiesUtils.DefaultJoinReportValue;
        return false;
    }

    /// <summary>
    /// Whether a player's ready record says it is ready.
    /// </summary>
    /// <remarks>
    /// <b>A state this server has not measured is not a ready, and the player it
    /// belongs to is not counted as one.</b> The captures hold one state and one
    /// transition — the ready — so the gate reading any other value as a ready
    /// would open a round on a byte nothing measured, and reading it as a wait is
    /// the safe end of that: the room's briefing still ends on its own deadline
    /// (<see cref="MatchRecords.MatchStartRunService"/>).
    /// </remarks>
    /// <param name="ready">The player's ready record.</param>
    /// <returns><c>true</c> when the record reports the ready state.</returns>
    private static bool IsReady(BriefingReady ready) =>
        ready.State == MatchRecords.BriefingReadyBodiesUtils.ReadyState;

    /// <summary>
    /// Whether an endpoint has readied: a ready record filed under it whose state
    /// is the ready.
    /// </summary>
    /// <param name="remoteAddress">Endpoint the player is filed under.</param>
    /// <returns><c>true</c> when the player has readied.</returns>
    private bool HasReadied(string remoteAddress) =>
        readies.TryGetValue(remoteAddress, out var ready) && IsReady(ready);

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
            "Map-loaded state from {RemoteAddress} has no roster entry; it is counted but its slot is "
            + "unknown",
            remoteAddress);
        return null;
    }
}
