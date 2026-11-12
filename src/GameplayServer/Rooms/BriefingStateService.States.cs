using Mgo2Server.GameplayServer.MatchRecords;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The states a briefing is made of: the map-loaded report each client sends,
/// the ready record each player sends, and the publication the room answers
/// both with.
/// </summary>
/// <remarks>
/// Split out of <c>BriefingStateService.cs</c> to keep both files under the
/// 400-line limit; the room's own counts and its ready set are there.
/// </remarks>
public sealed partial class BriefingStateService
{
    /// <summary>
    /// Drops every report and ready state for an emptied room, keeping the ready
    /// of a client that is still on the briefing screen.
    /// </summary>
    /// <param name="keepingReady">
    /// Endpoint whose ready survives, or <c>null</c> when the room emptied for good.
    /// </param>
    public void Reset(string? keepingReady = null)
    {
        lock (gate)
        {
            BriefingReady? kept = null;
            if (keepingReady is not null)
            {
                readies.TryGetValue(keepingReady, out kept);
            }

            joinReports.Clear();
            readies.Clear();
            botPublicationIndexes.Clear();
            nextPublicationIndex = 0;
            if (kept is not null)
            {
                readies[keepingReady!] = kept;
            }
        }
    }

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
    /// <para>
    /// The ready record goes with the report: a player that has left is not a
    /// player the room is waiting on, and a bit left in the ready set would keep
    /// <see cref="ReadyMask"/> naming a slot nobody fills — and, with the gate on
    /// the readies, hold the round open for a player that is gone.
    /// </para>
    /// <para>
    /// <b>The one exception is a client that is joining again.</b> Its session is
    /// retired and its handshake starts over, but its briefing screen is the one it
    /// readied on, and a client that has readied there does not ready a second time:
    /// in the rejoin of 2026-11-09 the report arrives at 14:20:25 and no ready
    /// record follows it, because the client is already showing its ready. The
    /// report still goes, so the room hears it from the session that fills the peer
    /// now; the ready stays, and the room the client returns to is one whose ready
    /// set is already complete rather than one waiting on a record nobody sends.
    /// </para>
    /// </remarks>
    /// <param name="remoteAddress">Endpoint the player was filed under.</param>
    /// <param name="keepReady">
    /// Whether the player is joining again, so its ready state is to stay with the
    /// briefing it belongs to.
    /// </param>
    /// <returns><c>true</c> when the player had reported a state.</returns>
    public bool Forget(string remoteAddress, bool keepReady = false)
    {
        var readied = false;
        lock (gate)
        {
            if (keepReady)
            {
                readied = readies.ContainsKey(remoteAddress);
                var reported = joinReports.Remove(remoteAddress);
                if (reported || readied)
                {
                    logger.LogInformation(
                        "{RemoteAddress} left the briefing and is joining again; its ready state is kept "
                        + "with the briefing it is still in",
                        remoteAddress);
                }

                return reported || readied;
            }

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
    /// <para>
    /// A round is preceded by its own briefing — the captures open a round, play
    /// it, then rewrite the ready set for the round after it — so the states
    /// belong to one briefing and not to the room.
    /// </para>
    /// <para>
    /// <b>One player's ready can outlive the clear, and it is the player whose
    /// session emptied the room.</b> A room whose last client started its session
    /// over begins its briefing for that same client, still on the screen it readied
    /// on (<see cref="Forget"/>). Dropping that ready here left the rejoin of
    /// 2026-11-09 waiting on a record it had already been sent: a held count of
    /// sixty seconds, seven bots ready, and its only client counted as the one that
    /// had not readied, so the closing zero never came and the round never opened.
    /// </para>
    /// </remarks>
    /// <param name="keepReadyFor">
    /// Endpoint of a client whose ready belongs to the briefing that is beginning
    /// too, or <c>null</c> when every state goes.
    /// </param>
    public void BeginNextBriefing(string? keepReadyFor = null)
    {
        lock (gate)
        {
            var kept = keepReadyFor is not null && readies.ContainsKey(keepReadyFor) ? 1 : 0;
            if (joinReports.Count == 0 && readies.Count == kept)
            {
                nextPublicationIndex = 0;
                botPublicationIndexes.Clear();
                return;
            }

            logger.LogInformation(
                "Beginning the next briefing; {ReportedCount} report(s) and {BriefingReadyCount} ready state(s) "
                + "from the last one are cleared",
                joinReports.Count,
                readies.Count - kept);
            joinReports.Clear();
            foreach (var address in readies.Keys.Where(key => key != keepReadyFor).ToList())
            {
                readies.Remove(address);
            }

            nextPublicationIndex = 0;
            botPublicationIndexes.Clear();
        }
    }

    /// <summary>
    /// The ready set as it goes on the wire: for each player that has reported, in
    /// publication order, its player-state record and then its publication, both at
    /// that player's own fourth byte.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two records per player, and the state record comes first.</b> A working
    /// server's own briefing writes <c>0x1b06</c> immediately ahead of the
    /// <c>0x1ade e0 &lt;slot&gt; …</c> that answers the report, both at the same
    /// ordinal — <c>0x1b06</c> ordinal 9 beside <c>0x1ade e0 0f …</c> ordinal 9, at
    /// t+512.10 of <c>working-server-log.pcapng</c> — and it repeats with them on
    /// every publication after it (eleven copies, t+508.7 to t+513.5). This host
    /// wrote the publication alone, which was the last record of the join missing
    /// from its briefing (its capture, `working-server-log.pcapng`).
    /// </para>
    /// <para>
    /// **The state record is per player and not per room**: its slot byte is the
    /// byte that player's own report carried, which is why it is built here where
    /// the reports are ordered rather than beside the countdown.
    /// </para>
    /// </remarks>
    /// <param name="generation">Identifier set the publication travels under.</param>
    /// <param name="roundUnits">
    /// The round clock the briefing is held on, in the stream's 125 ms units
    /// (<see cref="MatchRecords.MatchStartRunService.BriefingRoundUnits"/>), which
    /// the state record carries.
    /// </param>
    /// <returns>The records in send order.</returns>
    public List<RosterRecord> BuildPublication(
        MatchRecordGenerationUtils generation,
        int roundUnits)
    {
        List<JoinReport> ordered;
        lock (gate)
        {
            ordered = [.. joinReports.Values.OrderBy(state => state.PublicationIndex)];
        }

        var records = new List<RosterRecord>(ordered.Count * 2);
        foreach (var state in ordered)
        {
            var ordinal = (byte)state.PublicationIndex;

            // The player's state record first, at the publication's own fourth
            // byte: that is the order the one capture holding both writes them in.
            records.Add(new RosterRecord(
                generation.ReadyState,
                MatchRecords.BriefingReadyStateBodiesUtils.Body(state.Value, roundUnits),
                ordinal));
        }

        // One round-start payload per publication, and not one per player.
        // `.pcapngs/actions/actions.pcapng` writes `0x1ade`/`0x12de` **twice in
        // 592 s** — once per round open — where this host wrote one beside every
        // ready-state record: 86 of them in 51 s, twelve of them identical in the
        // same instant. A client handed a round start sixteen times a publication
        // rebuilds the round and its actor table every second, and a body rebuilt
        // every second is one that takes no damage and never dies on screen — the
        // report of "the bots cannot be killed". The body is the reported byte
        // echoed, so the report this publication answers is the one it carries.
        if (ordered.Count > 0)
        {
            records.Add(new RosterRecord(
                generation.MatchStartPayload,
                MatchStartBodiesUtils.MatchStartPayload(ordered[0].Value),
                (byte)ordered[0].PublicationIndex));
        }

        // The bots are in the publication too, and the room a client is briefed in
        // is not its reporters alone: a bot holds a roster entry and a ready state
        // and never sends a report, so a publication built from the reports alone
        // named one row per client and left every bot's roster entry with no row of
        // its own — the briefing that showed one of eight. The value byte is the
        // seat the bot holds, which is the byte a client's own report carries for
        // itself, and the fourth byte is a place in this publication handed out
        // once per briefing so a repeat repeats it rather than numbering a second
        // set.
        lock (gate)
        {
            foreach (var member in roster.Snapshot().Where(member => member.IsBot))
            {
                if (!readies.TryGetValue(member.RemoteAddress, out var ready))
                {
                    continue;
                }

                if (!botPublicationIndexes.TryGetValue(member.RemoteAddress, out var index))
                {
                    index = nextPublicationIndex++;
                    botPublicationIndexes[member.RemoteAddress] = index;
                }

                records.Add(new RosterRecord(
                    generation.ReadyState,
                    MatchRecords.BriefingReadyStateBodiesUtils.Body(ready.Slot, roundUnits),
                    (byte)index));
            }
        }

        return records;
    }
}
