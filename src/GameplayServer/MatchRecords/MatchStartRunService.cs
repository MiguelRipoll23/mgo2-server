using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Opens a round for the room and ends it again.
/// </summary>
/// <remarks>
/// <para>
/// <b>A round opens on the room's ready set and never on a clock.</b> Measured
/// over every capture that holds a round, each one begins within 0.6–5.5 s of a
/// client's readiness report and none begins without one
/// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2), and the round of
/// <c>mgo2-game-joined-when-red-team-then-briefing</c> opens one second after the
/// ready record of the room's last player. A host that opened its round on the
/// measured interval from the roster got the timing right and the behaviour wrong:
/// the client sat in its ready state and reported twenty-three times over two
/// minutes while the run it had already been sent went unrepeated. The gate here is
/// therefore <see cref="OpenWhenEveryPlayerIsReady"/>, which opens the round on the
/// ready that completes the room's readied players, or its briefing running out
/// first, which is the next paragraph.
/// </para>
/// <para>
/// <b>A room's clients are briefed before they are started, and the briefing is
/// answered on its own set.</b> A client in a briefing reports itself ready and
/// waits for the host; what the host writes while it waits is the briefing's own
/// run, under the set that first report arrived on, and not a round. The round
/// opens on the next report, which arrives on the other set once the briefing's
/// time is out — the two reports per round the reference shows
/// (<c>docs/protocol/BRIEFING_FLOW.md</c> §1–§2) — or on the briefing's deadline
/// for a room whose client never sends that second report. The earlier reading
/// that held a sole client's first report back was withdrawn for the wrong
/// reason: the report *is* answered, but with the briefing's run, and a report a
/// capture answers with a round is the second one.
/// </para>
/// <para>
/// <b>How many players are needed is a deadline, not a rule.</b> A client waits
/// in its briefing for the host's round either until every player is ready or
/// until its own briefing time is up, so a room that waited for a player who never
/// readies leaves the others past the end of their timer with no round at all
/// (<c>docs/GAMEPLAY_SERVER.md</c> §4.2). The first report of a briefing therefore
/// arms a deadline of the room's own briefing time, and a briefing that runs out
/// opens the round for the players it did hear from — under the set the last of
/// their reports arrived on. A briefing no player has answered opens nothing: a
/// round is still something a report asks for, and a room nothing has answered has
/// no set to open one under — which is why the ready gate above opens nothing
/// without one either (<see cref="BriefingGeneration"/>).
/// </para>
/// <para>
/// <b>What ends a round.</b> The round's own clock, which is the length its
/// operations record carries, and that length is the room's own: the round the
/// rotation names, timed by this instance's settings for it
/// (<see cref="MatchSettingsService"/>, and
/// <c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2 for the measurement). A goal is
/// not visible on this channel — the objective is scored in the round's own state
/// block and the end-of-round report travels over the lobby's TCP channel — so
/// <see cref="End"/> is the seam a goal would use, and no round ends early for
/// one here.
/// </para>
/// <para>
/// <b>The room's settings are this instance's own.</b> A round carries a rule, a
/// map and a clock, and the dedicated host is the one that chose all three: they
/// are its configuration (<see cref="MatchSettingsService"/>), the same settings
/// its room row is written from, so a round is opened on the very rotation the
/// client was handed. There is nothing to read to open one, and a host whose room
/// is not published yet opens no round at all — the client goes on reporting,
/// which is the state a server with nothing to serve should leave it in.
/// </para>
/// </remarks>
/// <param name="roundState">The room's round, which this service opens and closes.</param>
/// <param name="ready">The room's briefing states, which a round's end re-arms.</param>
/// <param name="matchSettings">Resolver of the room's own rule, map and clock.</param>
/// <param name="lobbySlots">
/// Reader of the room's own players by their slot in its 24-entry player table
/// and the side each is on: the run's lobby-slot pair names them, so a room whose
/// players were never read is a run that tells its clients nobody is here.
/// </param>
/// <param name="score">
/// The host's own score for the round, cleared when a round opens.
/// </param>
/// <param name="results">
/// The round's result: the winner a clock's end is written with, and the state
/// write that publishes it once the round has closed.
/// </param>
/// <param name="roster">
/// The room's roster exchange, whose counter numbers the run's records: the
/// lobby-slot pair takes the two values after the join's own records and the
/// burst's, which is what keeps the pair consecutive and every record of that
/// identifier in one ascending sequence.
/// </param>
/// <param name="logger">Logger of this service.</param>
public sealed partial class MatchStartRunService(
    MatchRoundStateService roundState,
    Rooms.PlayerReadyService ready,
    MatchSettingsService matchSettings,
    Rooms.LobbySlotService lobbySlots,
    Rooms.RoomRosterService roster,
    MatchScoreService score,
    MatchResultService results,
    BriefingCountdownService countdown,
    MatchRecordSequenceService sequences,
    ILogger<MatchStartRunService> logger)
{
    /// <summary>
    /// How many players must have reported before the briefing countdown moves:
    /// the second player is what ends a room of one's frozen briefing.
    /// </summary>
    private const int CountingReportCount = 2;

    private readonly Lock gate = new();

    private int briefingNumber;
    private ushort? lastReportIdentifier;
    private MatchRecordGenerationUtils? briefingGeneration;

    /// <summary>
    /// Identifier the room's briefing last heard a report on, or <c>null</c> while
    /// the briefing current has heard from nobody. It is the set the ready set is
    /// repeated under while the room waits (<see cref="BriefingReadyPublishService"/>),
    /// and, for a briefing that runs out before a second report arrives, the set
    /// the deadline opens the round under.
    /// </summary>
    public ushort? LastReportIdentifier
    {
        get
        {
            lock (gate)
            {
                return lastReportIdentifier;
            }
        }
    }

    /// <summary>
    /// The set the room's briefing is being written under: the one its first
    /// report named, or <c>null</c> while the briefing has heard from nobody.
    /// </summary>
    /// <remarks>
    /// <b>The briefing has its own generation and the round has the next one.</b>
    /// The reference writes the run the first report asks for under that report's
    /// own set, and only opens the round — under the set the next report arrives
    /// on — once the briefing's time is out
    /// (<c>docs/protocol/BRIEFING_FLOW.md</c> §1–§2). The run worker reads this to
    /// write the briefing's run (<see cref="MatchStartRunService.Runs"/>).
    /// </remarks>
    public MatchRecordGenerationUtils? BriefingGeneration
    {
        get
        {
            lock (gate)
            {
                return briefingGeneration;
            }
        }
    }

    /// <summary>
    /// Takes a readiness report and, when it is the one the room's round waits
    /// for, opens the round under the set that report arrived on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The briefing is answered first and the round second.</b> A briefing
    /// hears two reports a round. The first names the set the briefing itself is
    /// written under — the client is still being briefed, so what it is served is
    /// that briefing's run and not a round (<see cref="MatchStartRunService.Runs"/>)
    /// — and the second, arriving on the other set once the briefing's time is
    /// out, asks for the round the captures show playing after it
    /// (<c>docs/protocol/BRIEFING_FLOW.md</c> §1–§2). **That round opens on the
    /// room's ready records and not on the report**
    /// (<see cref="OpenWhenEveryPlayerIsReady"/>): the report is what a run is
    /// written from, and a room whose players have not all readied stays closed
    /// here. A report on the set the
    /// briefing is already on is the first one repeated: a client re-reports until
    /// it is answered, and that is the same report and not a round asked for.
    /// </para>
    /// <para>
    /// <b>The gate is here and not only in the handler.</b> A round opens for a
    /// room, so "is the room ready" is a condition of opening one rather than a
    /// step a caller is asked to remember: the handler that takes a report is one
    /// caller today, and a gate kept there would let any other caller start a
    /// round on a room that is still being briefed. It is also the invariant a
    /// second round depends on, because a round's end clears the states and the
    /// next report to arrive is a single player's.
    /// </para>
    /// </remarks>
    /// <param name="reportIdentifier">Identifier the readiness report arrived on.</param>
    /// <returns><c>true</c> when this call opened the round.</returns>
    public bool Open(ushort reportIdentifier)
    {
        var generation = MatchRecordGenerationUtils.OfReadinessReport(reportIdentifier);

        MatchRecordGenerationUtils? briefing;
        int armed;
        lock (gate)
        {
            briefing = briefingGeneration;
            lastReportIdentifier = reportIdentifier;
            armed = briefingNumber;
            if (briefing is null)
            {
                // The briefing's own set: the first report names it, and the run
                // the worker writes for the briefing is written under it.
                briefingGeneration = generation;
            }
        }

        // The briefing's clock is the report's own: it is armed here and not where
        // the briefing was prepared, because the join that prepared it can be a
        // minute ahead of the client that has to read the briefing
        // (ArmBriefingClock). Only a briefing's first report arms it — a report on
        // a briefing whose clock is already running is the same report repeated,
        // and the clock it was armed with is still the one it is on.
        if (briefing is null)
        {
            ArmBriefingClock(armed);
        }

        // A room with a second reporting player is a room whose briefing counts:
        // the countdown is held at the room's whole briefing time while one
        // player is in the room, and starts from the moment the second report
        // arrives (BriefingCountdownService.StartCounting). The call is
        // idempotent, so the reports after the second cost nothing.
        if (ready.ReadyCount >= CountingReportCount)
        {
            countdown.StartCounting(DateTimeOffset.UtcNow);
        }

        if (briefing is null)
        {
            logger.LogInformation(
                "The room's briefing heard its first report on 0x{Identifier:x4}; the briefing is written "
                + "under that set and the round waits for a report on the other one, or for the briefing "
                + "time, before it opens",
                reportIdentifier);
            return false;
        }

        if (generation == briefing)
        {
            logger.LogDebug(
                "A report arrived on 0x{Identifier:x4}, the set the room's briefing is already on; the "
                + "round waits for a report on the other one",
                reportIdentifier);
            return false;
        }

        // The other set: the report the captures show opening the round once the
        // briefing is done.
        return OpenRound(
            generation,
            $"the report arrived on 0x{reportIdentifier:x4}",
            waitForEveryone: true);
    }

    /// <summary>
    /// Opens the room's round because every player in it has readied, which is
    /// what the reference host does at the ready of the room's last player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a ready record and not a report that starts the round.</b> In
    /// <c>mgo2-game-joined-when-red-team-then-briefing</c> the room readies
    /// <c>1f 05 01</c> at t+112.750 and its round is open at t+113.758, one second
    /// later; the reports of the same briefing are at t+57.656 and later, and the
    /// room's other players readied between t+76.6 and t+111.2 while it waited
    /// (<see cref="Rooms.PlayerReadyService.ReadyMask"/>). A gate on the reports
    /// would open that round at the first pair of them.
    /// </para>
    /// <para>
    /// <b>The round plays under the other set.</b> The briefing's own set is the one
    /// its first report named and the one its run was written under
    /// (<see cref="BriefingGeneration"/>); the round that follows it plays under
    /// <see cref="MatchRecordGenerationUtils.Sibling"/>, which is the set its run,
    /// its beat and its result are written on. A briefing that has heard no report
    /// has no set of its own and opens nothing.
    /// </para>
    /// </remarks>
    /// <returns><c>true</c> when this call opened the round.</returns>
    public bool OpenWhenEveryPlayerIsReady()
    {
        MatchRecordGenerationUtils? briefing;
        lock (gate)
        {
            briefing = briefingGeneration;
        }

        if (briefing is null)
        {
            // Only a report names a briefing's set, so a room whose players have
            // readied without one has no set for a round to follow it on.
            logger.LogDebug(
                "The room's players are ready but its briefing has heard no report, so it has no set "
                + "and no round is opened under it");
            return false;
        }

        if (!ready.AllReady)
        {
            logger.LogDebug(
                "A player readied, but {PendingCount} player(s) in the room have not; the round stays "
                + "closed until they do or the briefing runs out",
                ready.PendingBriefingReadyCount);
            return false;
        }

        return OpenRound(
            briefing.Sibling,
            $"every player in the room has readied, {ready.BriefingReadyCount} of them",
            waitForEveryone: false);
    }

    /// <summary>
    /// Opens the room's round, under the identifier set a report arrived on.
    /// </summary>
    /// <param name="generation">The set the round plays under.</param>
    /// <param name="openedBy">
    /// What asked for the round, for the log and the gate's own message: the report
    /// it arrived on, or the room's ready set. The set the round plays under is the
    /// caller's (<paramref name="generation"/>) and is not read from this.
    /// </param>
    /// <param name="waitForEveryone">
    /// Whether the round waits for every player in the room to have readied. It is
    /// <c>false</c> for the briefing's deadline alone, which is the one caller
    /// allowed to open a round with players still to ready.
    /// </param>
    /// <returns><c>true</c> when this call opened the round.</returns>
    private bool OpenRound(
        MatchRecordGenerationUtils generation,
        string openedBy,
        bool waitForEveryone)
    {
        // The gate is every player in the room having readied, and it is the
        // room's own population rather than a floor: a room of one client opens
        // on that client's ready, which is what every capture does. A player
        // who never readies is still ended by the briefing's own deadline
        // (OpenWhenBriefingEndsAsync), which is the contract the class summary
        // gives it.
        if (waitForEveryone && !ready.AllReady)
        {
            logger.LogDebug(
                "The room asked for a round and {OpenedBy}, but {PendingCount} player(s) in it have not "
                + "readied; the round stays closed until they do or the briefing runs out",
                openedBy,
                ready.PendingBriefingReadyCount);
            return false;
        }

        // A round that is already playing is answered with the settings it opened
        // on — resolving them below is what a round about to open costs, and a
        // report the room has already answered does not pay it twice.
        var nextRound = roundState.RoundSettings ?? ReadRound();
        if (nextRound is null)
        {
            return false;
        }

        // The clock a round opens on is whole, and it stays whole for as much of
        // the room's briefing as is still to run. A report opens a round while its
        // client is still being briefed — that is what the report is — so the
        // round's clock counts from the briefing's own end and not from the open;
        // a clock that counted from the open would run the round out under a
        // client still reading the briefing (MatchRoundStateService.RemainingUnits).
        var hold = countdown.TimeRemaining(DateTimeOffset.UtcNow, out var remaining)
            ? (int)remaining.TotalMilliseconds
            : 0;

        var round = roundState.Open(generation, nextRound, hold);
        if (round is null)
        {
            // A client reports until it is answered, so a second report for a
            // round already playing is the same round and not another one.
            return false;
        }

        // The round opens its own operations stream. The run's three records take
        // the first values and the beat's clocks go on from there — the recorded
        // run carries 00 01 02 and its beat 03 — and the briefing's clock, which
        // wrote under the same identifier, is behind this point. A round whose
        // generation is the other one opens a stream of its own, because the
        // sibling bit is inside the keyed twelve
        // (<see cref="MatchRecordSequenceService.StreamOf"/>).
        sequences.Restart(generation.Operations);

        // The round's score starts empty: the previous round's kills and deaths
        // are not this one's, and the scoreboard is a per-round board.
        score.Reset();

        logger.LogInformation(
            "Opening round {RoundNumber} for the room: {OpenedBy}, so the round plays under that set",
            round,
            openedBy);

        _ = EndWhenTimeRunsOutAsync(
            round.Value,
            hold + RoundClockUtils.TimeLimitMilliseconds(nextRound));
        return true;
    }

    /// <summary>
    /// The round the room's own rotation names for the round that is about to
    /// open: what this host is configured to run.
    /// </summary>
    /// <remarks>
    /// The round number counted is the next one and not the one playing, because
    /// the rotation is walked by the round that opens: a room whose first round is
    /// playing answers here with its second entry. Nothing is resolved while a
    /// round is already open — the report that would open one has been answered,
    /// and a second resolution would only be settings nobody is asking for.
    /// </remarks>
    /// <returns>The round to open, or <c>null</c> when the room has none published.</returns>
    private MatchRotationEntry? ReadRound()
    {
        var settings = matchSettings.FindPublished();
        if (settings is null)
        {
            logger.LogWarning(
                "This host's room is not published, so no round is opened for it; the client goes on "
                + "reporting until it is");
            return null;
        }

        var round = settings.Round(roundState.RoundNumber + 1);
        logger.LogDebug(
            "Match {GameIdentifier} ({GameName}) plays round {RoundNumber} on rule {Rule}, map {Map} for "
            + "{TimeLimitMinutes} minutes; the room's briefing lasts {BriefingTimeMinutes} minutes",
            settings.GameIdentifier,
            settings.Name,
            roundState.RoundNumber + 1,
            round.Rule,
            round.Map,
            round.TimeLimitMinutes,
            settings.BriefingTimeMinutes);
        return round;
    }
}
