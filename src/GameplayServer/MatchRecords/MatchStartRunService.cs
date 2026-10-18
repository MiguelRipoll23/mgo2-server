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
/// player's own record and none begins without one
/// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2), and the round of
/// <c>mgo2-game-joined-when-red-team-then-briefing</c> opens one second after the
/// ready record of the room's last player. A host that opened its round on the
/// measured interval from the roster got the timing right and the behaviour wrong:
/// the client sat in its ready state and reported twenty-three times over two
/// minutes while the run it had already been sent went unrepeated. The gate here is
/// therefore <see cref="OpenWhenEveryPlayerIsReady"/>, which opens the round on the
/// ready that completes the room's readied players.
/// </para>
/// <para>
/// <b>A room's clients are briefed before they are started, and the briefing is
/// answered on its own set.</b> A client joins the room, reports that join, and is
/// briefed; what the host writes while it waits is the briefing's own
/// run, under the set that first report arrived on, and not a round. The round
/// opens on the room's ready records, or on the next join report once the briefing's
/// time is out — the two reports per round the reference shows
/// (<c>docs/protocol/BRIEFING_FLOW.md</c> §1–§2). The earlier reading
/// that held a sole client's first report back was withdrawn for the wrong
/// reason: the report *is* answered, but with the briefing's run, and a report a
/// capture answers with a round is the second one.
/// </para>
/// <para>
/// <b>A room of one is frozen, and a room of two has a deadline.</b> A client waits
/// in its briefing either until every player is ready or until its own briefing
/// timer ends, so a room that waited on a player who never readies leaves the
/// others past the end of their timer with no round at all
/// (<c>docs/GAMEPLAY_SERVER.md</c> §4.2). The timer is the room's, though, and a
/// room of one has none: its briefing count is **held** at the room's whole
/// briefing time and nothing runs out, which is what the reference host writes for a
/// sole client (`mgo2-game-started-on-briefing` holds `0b 3c` for tens of seconds,
/// and `mgo2-started-briefing-then-solo-game` starts its round only after its
/// client readies and re-reports at t+165.780 and t+173.438). **The map-loaded
/// report is therefore not a readiness and arms nothing**: it says the client is on
/// the briefing screen, and the deadline it used to arm was the host starting a round
/// nobody had asked it for (§17 of <c>docs/analysis/join-stall-after-mode2.md</c>).
/// The deadline begins where the room has somebody to be briefed with — the second
/// player's own join report — and a briefing that runs out opens the round for the
/// players it did hear from, under the set the last of their reports arrived on.
/// A briefing no player has answered opens nothing: a round is still something a
/// report asks for, and a room nothing has answered has no set to open one under —
/// which is why the ready gate above opens nothing without one either
/// (<see cref="BriefingGeneration"/>).
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
/// <param name="briefingStates">
/// The room's briefing states — who has joined and who has readied — which a
/// round's end re-arms.
/// </param>
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
    Rooms.BriefingStateService briefingStates,
    MatchSettingsService matchSettings,
    Rooms.LobbySlotService lobbySlots,
    Rooms.RoomRosterService roster,
    Rooms.TeamArrayService teamArrays,
    MatchScoreService score,
    MatchResultService results,
    BriefingCountdownService countdown,
    MatchRecordSequenceService sequences,
    ILogger<MatchStartRunService> logger)
{
    /// <summary>
    /// How many players must have joined before the briefing's count moves and its
    /// deadline is armed: the second player is what ends a room of one's frozen
    /// briefing.
    /// </summary>
    private const int CountingJoinReportCount = 2;

    private readonly Lock gate = new();

    private int briefingNumber;
    private ushort? lastJoinReportIdentifier;
    private MatchRecordGenerationUtils? briefingGeneration;

    /// <summary>
    /// Briefing number whose deadline has been armed, or <c>null</c> while the
    /// briefing current has not been armed with one. A briefing arms its deadline
    /// once, at the report that gives it a second player, so the reports after that
    /// one cost nothing (<see cref="ArmBriefingDeadline"/>).
    /// </summary>
    private int? armedDeadlineBriefing;

    /// <summary>
    /// Identifier the room's briefing last heard a join report on, or <c>null</c>
    /// while the briefing current has heard from nobody. It is the set the ready set
    /// is repeated under while the room waits
    /// (<see cref="BriefingReadyPublishService"/>), and the set a briefing that runs
    /// out opens its round under.
    /// </summary>
    public ushort? LastJoinReportIdentifier
    {
        get
        {
            lock (gate)
            {
                return lastJoinReportIdentifier;
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

    /// Opens the room's round because every player in it has readied, which is
    /// what the reference host does at the ready of the room's last player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a ready record and not a join report that starts the round.</b> In
    /// <c>mgo2-game-joined-when-red-team-then-briefing</c> the room readies
    /// <c>1f 05 01</c> at t+112.750 and its round is open at t+113.758, one second
    /// later; the reports of the same briefing are at t+57.656 and later, and the
    /// room's other players readied between t+76.6 and t+111.2 while it waited
    /// (<see cref="Rooms.BriefingStateService.ReadyMask"/>). A gate on the reports
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

        if (!briefingStates.AllReady)
        {
            logger.LogDebug(
                "A player readied, but {PendingReadyCount} player(s) in the room have not; the round stays "
                + "closed until they do or the briefing runs out",
                briefingStates.PendingBriefingReadyCount);
            return false;
        }

        return OpenRound(
            briefing.Sibling,
            $"every player in the room has readied, {briefingStates.BriefingReadyCount} of them",
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
        if (waitForEveryone && !briefingStates.AllReady)
        {
            logger.LogDebug(
                "The room asked for a round and {OpenedBy}, but {PendingReadyCount} player(s) in it have not "
                + "readied; the round stays closed until they do or the briefing runs out",
                openedBy,
                briefingStates.PendingBriefingReadyCount);
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
        // the room's briefing as is still to run. A ready opens a round while its
        // room may still be briefed, so the round's clock counts from the briefing's
        // own end and not from the open — and a room of one has no end to wait for,
        // its count being frozen and TimeRemaining answering nothing for it, so its
        // clock counts from the open; a clock that counted from the open would run
        // the round out under a
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
