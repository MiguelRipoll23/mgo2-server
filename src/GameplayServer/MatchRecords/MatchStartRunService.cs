using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Opens a round for the room and ends it again.
/// </summary>
/// <remarks>
/// <para>
/// <b>A round opens on the report that follows the room's briefing, and on nothing
/// else.</b> The client sits on its briefing screen for the room's whole briefing
/// time — writing its own briefing rows, sending nothing — and then reports once
/// more, when it has left the screen for the weapon select it shows next and its
/// spawn; that report is what opens the round. Measured: a client's report at
/// t+304.686 is followed by its own briefing rows and ninety seconds of silence, its
/// report at t+395.811 is followed by the round 0.2 s later with the clock at 2 400
/// (`working-server-4-sessions.pcapng`), and that capture holds no ready record in
/// its briefing at all. An earlier reading that gated the round on the room's ready
/// set — <c>1f &lt;slot&gt; &lt;state&gt;</c> — was wrong on both halves: the record is
/// not a readiness a round waits on, and the briefing's time running out starts no
/// round either (<see cref="OnJoinReport"/>).
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
/// <b>The client's own briefing timer is the room's, and the round follows it.</b> A
/// client in a briefing waits out its own timer and leaves the screen when it
/// expires, and its timing is the room's briefing time as the lobby's room details
/// state it (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c>). The count the host writes
/// while the room waits is that same time (<see cref="BriefingCountdownService"/>):
/// held at the room's whole briefing time for a room of one, moved once the room has
/// a second player, and written for the screen to show — it opens nothing.
/// **The map-loaded report is therefore not a readiness**: it says the client is on
/// the briefing screen, and every report before the briefing's time is out is that
/// screen still being waited on. A briefing no player has answered opens nothing: a
/// round is still something a report asks for, and a room nothing has answered has
/// no set to open one under (<see cref="BriefingGeneration"/>).
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
    Rooms.RoomRosterService roster,
    Rooms.TeamArrayService teamArrays,
    Rooms.LobbySlotService lobbySlots,
    MatchScoreService score,
    MatchResultService results,
    BriefingCountdownService countdown,
    MatchRecordSequenceService sequences,
    ILogger<MatchStartRunService> logger)
{
    /// <summary>
    /// The most reports the briefing's count and its deadline wait for: a room of
    /// one is armed by its own report, and a room of two or more by the second —
    /// the report that gives a frozen room somebody else to wait for. Which of the
    /// two it is, is the room's population (<see cref="OnJoinReport"/>).
    /// </summary>
    private const int CountingJoinReportCount = 2;

    private readonly Lock gate = new();

    private int briefingNumber;
    private ushort? lastJoinReportIdentifier;
    private MatchRecordGenerationUtils? briefingGeneration;

    /// <summary>
    /// Briefing number whose count has been started, or <c>null</c> while the
    /// briefing current has not started one. A briefing starts its count once, at
    /// the report that gives it a second player, so the reports after that one cost
    /// nothing (<see cref="StartBriefingCount"/>).
    /// </summary>
    private int? countStartedBriefing;

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

    /// <summary>
    /// Opens the room's round, under the identifier set a report arrived on.
    /// </summary>
    /// <param name="generation">The set the round plays under.</param>
    /// <param name="openedBy">
    /// What asked for the round, for the log: the report it arrived on. The set the
    /// round plays under is the caller's (<paramref name="generation"/>) and is not
    /// read from this.
    /// </param>
    /// <returns><c>true</c> when this call opened the round.</returns>
    private bool OpenRound(
        MatchRecordGenerationUtils generation,
        string openedBy)
    {
        // A round that is already playing is answered with the settings it opened
        // on — resolving them below is what a round about to open costs, and a
        // report the room has already answered does not pay it twice.
        var nextRound = roundState.RoundSettings ?? ReadRound();
        if (nextRound is null)
        {
            return false;
        }

        // The clock a round opens on is whole, and it stays whole for as much of the
        // room's briefing as is still to run. A count that is still moving is a
        // briefing still being waited out, so the round's clock counts from that
        // briefing's own end and not from the open; a room of one has no end to wait
        // for, its count held and TimeRemaining answering nothing for it, so its
        // clock counts from the open. A clock that counted from the open would run
        // the round out under a client still reading the briefing
        // (MatchRoundStateService.RemainingUnits).
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
