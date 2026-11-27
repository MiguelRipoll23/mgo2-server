using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The result of the round: which side won it, the state write that publishes
/// that, and the check that ends a round whose side has spent its last ticket.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is apart from the round's opener because the round has two ends.</b> The
/// clock ends it — <see cref="ClockResultAsync"/> is the winner that end is
/// written with — and the rule's ticket limit ends it, which is
/// <see cref="TicketsReachedAsync"/>. Both are read off
/// <see cref="MatchScoreService"/> and the room's own sides, and neither is the
/// opener's to decide: the caller that saw the round end asks this service what
/// to write.
/// </para>
/// <para>
/// <b>The opener writes the result, this service builds it.</b> The write lands
/// in the state block the round's opening run carried, so it is the same peer's
/// same set (<see cref="WriteResultAsync"/>), and the round watcher that is
/// waiting on the close is the only writer holding that context.
/// </para>
/// </remarks>
/// <param name="score">The host's own score for the round.</param>
/// <param name="lobbySlots">Reader of the room's players, by slot and side.</param>
/// <param name="roundState">The room's round, which holds the result once it ends.</param>
/// <param name="publisher">Writer of the scoreboard the winning side's step goes out on.</param>
/// <param name="sequences">The streams the result writes take their fourth bytes from.</param>
/// <param name="logger">Logger of this service.</param>
public sealed partial class MatchResultService(
    MatchScoreService score,
    LobbySlotService lobbySlots,
    MatchRoundStateService roundState,
    MatchSettingsService matchSettings,
    ScoreboardPublishService publisher,
    MatchRecordSequenceService sequences,
    ILogger<MatchResultService> logger)
{
    /// <summary>
    /// Held across the once-per-round award of the win and the game's result that is
    /// decided after it, so a peer's write reads the same decision as the first
    /// peer's and never one taken before the deciding round was credited.
    /// </summary>
    private readonly SemaphoreSlim awardGate = new(1, 1);

    /// <summary>
    /// The last round whose win was awarded, as
    /// <see cref="MatchRoundStateService.RoundsClosed"/> numbers them. The round's own
    /// number is not that identity, because an emptied room returns to its first
    /// round and the number repeats.
    /// </summary>
    private int awardedRound;

    /// <summary>
    /// The game's result the round that was awarded last decided, or <c>null</c>. It
    /// is kept for the other peers' writes of the same round, which are barred behind
    /// <see cref="awardGate"/> and so read the first peer's decision rather than taking
    /// their own against counters that have since started over.
    /// </summary>
    private byte? settledResult;
    /// <summary>
    /// A round whose tickets have run out: the side that spent its last one, the
    /// limit it reached, the side that takes the round, and the result code that
    /// win is written as.
    /// </summary>
    /// <param name="Team">Raw team value of the side whose tickets reached zero.</param>
    /// <param name="WinningTeam">Raw team value of the side that takes the round.</param>
    /// <param name="Tickets">The rule's ticket limit the side ran out of.</param>
    /// <param name="ResultCode">Result code the winning side's win is written as.</param>
    public sealed record TicketsReached(short Team, short WinningTeam, int Tickets, byte ResultCode);

    /// <summary>
    /// Whether one of the room's sides has spent its last ticket, which ends its
    /// round.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The count is the side's own deaths and not its opponents' kills.</b> A
    /// ticket is spent by the death of the side that holds it — the eleven-byte
    /// ticket record steps a side down once per death of that side
    /// (<see cref="ScoreboardPublishService"/>) — so a side is out when its deaths
    /// reach the rule's <c>tickets</c>, and the other side takes the round. Reading
    /// the killer's kill total instead let a round play on after the panel had
    /// already reached zero: a suicide or a team kill spends a ticket that no
    /// opposing kill count accounts for.
    /// </para>
    /// <para>
    /// The limit is the rule's own <c>tickets</c>, in the same stored bank as the
    /// time the round opened on (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2,
    /// §3). A rule the bank states no limit for never reaches this, and a side the
    /// room holds no players on is not a side the round can end against.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">Token that cancels the read of the room's sides.</param>
    /// <returns>The side that ran out of tickets, or <c>null</c>.</returns>
    public async Task<TicketsReached?> TicketsReachedAsync(CancellationToken cancellationToken = default)
    {
        if (!roundState.IsOpen
            || roundState.RoundSettings is not { Tickets: int tickets }
            || tickets <= 0)
        {
            return null;
        }

        var teams = score.TeamScores(await lobbySlots.ReadAsync(cancellationToken));
        if (teams.Count < 2)
        {
            return null;
        }

        var exhausted = teams
            .Where(team => team.Deaths >= tickets)
            .OrderByDescending(team => team.Deaths)
            .FirstOrDefault();
        if (exhausted is null)
        {
            return null;
        }

        var winner = teams.First(team => team.Team != exhausted.Team);
        logger.LogInformation(
            "Team {Team}'s tickets have reached zero after {Deaths} death(s), so team {WinnerTeam} takes "
            + "the round",
            exhausted.Team,
            exhausted.Deaths,
            winner.Team);
        return new TicketsReached(
            exhausted.Team,
            winner.Team,
            tickets,
            MatchRecordBodiesUtils.ResultCode(winner.Team));
    }

    /// <summary>
    /// The result of a round that ended on its clock: the side with more kills,
    /// the drawn code when the two are level, or <c>null</c> when this host has no
    /// score to decide it with.
    /// </summary>
    /// <remarks>
    /// It is this host's own reading and not a measured rule. The replay decoder's
    /// own note is that kills do not decide a round in the modes it measured —
    /// Capture is won on a target, not on a total — so a round decided by an
    /// objective this host cannot see carries the code of the score it does have,
    /// and <c>null</c> writes nothing rather than a winner it cannot name.
    /// </remarks>
    /// <param name="cancellationToken">Token that cancels the read of the room's sides.</param>
    /// <returns>The result code, or <c>null</c>.</returns>
    public async Task<byte?> ClockResultAsync(CancellationToken cancellationToken = default)
    {
        var teams = score.TeamLimits(await lobbySlots.ReadAsync(cancellationToken));
        if (teams.Count < 2 || teams.All(team => team.Score == 0))
        {
            return null;
        }

        var ordered = teams.OrderByDescending(team => team.Score).ToList();
        return ordered[0].Score == ordered[1].Score
            ? MatchRecordBodiesUtils.StateResultDrawn
            : MatchRecordBodiesUtils.ResultCode(ordered[0].Team);
    }

    /// <summary>Drops the awarded round, because the room has emptied.</summary>
    public void Reset()
    {
        awardedRound = 0;
        settledResult = null;
        stageResult = null;
    }

    /// <summary>
    /// Writes the result of the round that just ended to a peer: the operations
    /// stream's state write into <c>state[2]</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The write is opcode <c>0x02</c> followed by the offset, the count and the
    /// values, so <c>02 02 01 02</c> sets offset 2 — the result block's first byte,
    /// which the <c>00</c> vector carries as its byte 0 — to the round's winner in
    /// the decoder's own coding (<c>docs/protocol/BRIEFING_FLOW.md</c> §4, measured
    /// at every round end of `joined-when-red-team`). No result byte is written
    /// when the round ended with no result, which leaves the state block's own
    /// <c>0xff</c> where it stands; the round-over verdict still goes out.
    /// </para>
    /// <para>
    /// <b>The game's result rides the round's when the round decides it.</b> A
    /// round that is its game's last writes offsets 2 and 3 in one record —
    /// <c>02 02 02 02 02</c>, the round's and the match's results — which is what
    /// the captured server that decides a match writes, and a round inside a game
    /// writes offset 2 alone (<c>docs/protocol/STATE_VECTOR.md</c> §3). The value is
    /// the side that took the game: the side that swept it, or, when the rounds
    /// were split, the side with the higher total score. <c>replay_360824_6</c>
    /// splits its rounds one each and its match goes to the side that lost the last
    /// of them on that total (<c>docs/GAMEPLAY_SERVER.md</c> §5 item 1).
    /// </para>
    /// </remarks>
    /// <param name="context">Context the result is written through.</param>
    /// <param name="generation">Identifier set the round played under.</param>
    public async Task WriteResultAsync(PeerContext context, MatchRecordGenerationUtils generation)
    {
        // The verdict goes out first, as the image's match code publishes it
        // (`MatchResultService.RoundOver.cs`); a round with no result still has a
        // verdict to announce, so this is before the result's own early return.
        await WriteRoundOverAsync(context, generation);

        if (roundState.LastRoundResult is not { } result)
        {
            logger.LogDebug(
                "UDP {LocalPort}: round {RoundNumber} ended with no result to write to {RemoteAddress}",
                context.LocalPort,
                roundState.RoundNumber,
                context.Remote);
            return;
        }

        // The side that won the round steps its whole team's rounds-won counter,
        // which is the number every result screen states the score with. It is
        // awarded here, where the write that follows the close happens for each
        // peer, and once per round because the count must not step twice; the
        // game's own result is decided from that counter in the same turn.
        var matchResult = await AwardRoundWinAsync(result, context);

        if (!MatchRecordPeerUtils.IsAlive(context.Session))
        {
            return;
        }
        var ordinal = sequences.Take(generation.Operations);
        var record = new MatchRecordFramesUtils.RecordMessage(
            generation.Operations,
            matchResult is { } decided
                ? MatchRecordBodiesUtils.StateWrite(
                    MatchRecordBodiesUtils.StateRoundResultIndex,
                    result,
                    decided)
                : MatchRecordBodiesUtils.StateWrite(MatchRecordBodiesUtils.StateRoundResultIndex, result),
            ordinal);

        logger.LogInformation(
            "UDP {LocalPort}: writing round {RoundNumber}'s result to {RemoteAddress}: state[{Offset}] = {Result}"
            + "{Match}",
            context.LocalPort,
            roundState.RoundNumber,
            context.Remote,
            MatchRecordBodiesUtils.StateRoundResultIndex,
            result,
            matchResult is { } matchCode
                ? $", state[{MatchRecordBodiesUtils.StateMatchResultIndex}] = {matchCode}"
                : string.Empty);

        await MatchRecordSendUtils.SendAsync(context, [record]);
        // Result first: reference state[2] at t+154.244, scoreboard at t+198.680.
        await publisher.PublishAsync(context);

        // The stage's own result screen is drawn from fields written 21.1 s
        // behind this record. The wait runs beside the caller's round watcher,
        // so the briefing it goes on to serve is not held behind the screen.
        if (roundState.LastRoundClosedGame && roundState.IsStageEnding)
        {
            _ = WriteStageResultAsync(context, generation, roundState.StageEndVersion);
        }
    }

    /// <summary>
    /// Steps the winning side's rounds-won counter, once per round, queues the
    /// scoreboard that carries it, and settles the game's result when the round was
    /// its last.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only the side a result code names is credited.</b> The drawn and no-result
    /// codes name none, and the corpus shows nobody stepping on those rounds
    /// (<see cref="MatchScoreboardFramesUtils.RoundsWonBit"/>).
    /// </para>
    /// <para>
    /// <b>The deltas are queued here and written after the result.</b> A closed round
    /// writes no further kill or death, so nothing behind it would take them: the
    /// whole side steps at the round's end and the result write publishes the step.
    /// </para>
    /// </remarks>
    /// <param name="roundResult">Result code the round ended on.</param>
    /// <param name="context">Context the result is written through.</param>
    /// <returns>
    /// The game's result code when this round decided the game, or <c>null</c> when
    /// the game has another round or its rounds were split.
    /// </returns>
    private async Task<byte?> AwardRoundWinAsync(byte roundResult, PeerContext context)
    {
        await awardGate.WaitAsync();
        try
        {
            if (awardedRound >= roundState.RoundsClosed)
            {
                return settledResult;
            }

            awardedRound = roundState.RoundsClosed;
            settledResult = null;
            stageResult = null;

            if (MatchRecordBodiesUtils.TeamOfResult(roundResult) is { } winningTeam)
            {
                var slots = await lobbySlots.ReadAsync(CancellationToken.None);
                var winners = slots
                    .Where(slot => slot.Team == winningTeam)
                    .Select(slot => slot.Slot)
                    .ToList();

                var credited = score.NoteRoundWin(winners);

                logger.LogInformation(
                    "UDP {LocalPort}: team {Team} took round {RoundNumber}, so its {PlayerCount} player(s) step "
                    + "the rounds-won counter",
                    context.LocalPort,
                    winningTeam,
                    roundState.RoundNumber,
                    credited);
            }

            settledResult = await DecideMatchResultAsync();

            // The stage's score is read from the rounds-won counters before the
            // reset below clears them. A split game names the side with the higher
            // total score too, so its stage result is written from the rounds that
            // side took; only a level split has no result to write.
            if (roundState.LastRoundClosedGame
                && settledResult is { } matchCode
                && MatchRecordBodiesUtils.TeamOfResult(matchCode) is { } matchWinner)
            {
                var rounds = (await lobbySlots.ReadAsync(CancellationToken.None))
                    .Where(slot => slot.Team == matchWinner)
                    .Select(slot => score.RoundsWon(slot.Slot))
                    .DefaultIfEmpty(0)
                    .Max();
                if (rounds > 0)
                {
                    stageResult = new StageResult(
                        (byte)matchWinner,
                        (byte)Math.Clamp(rounds, 0, byte.MaxValue),
                        (byte)Math.Clamp(
                            matchSettings.RoundsInGame(roundState.RoundNumber),
                            0,
                            byte.MaxValue));
                    logger.LogInformation(
                        "UDP {LocalPort}: the stage's result is team {Team}, {Rounds} round(s) of "
                        + "{StageRounds}",
                        context.LocalPort,
                        matchWinner,
                        rounds,
                        stageResult.StageRounds);
                }
            }

            // A game's last round is where the screen that states the game's score
            // is drawn, so the counters start over once that number has been
            // published and the game's result has been read off them.
            if (roundState.LastRoundClosedGame)
            {
                score.ResetRoundWins();
            }

            return settledResult;
        }
        finally
        {
            awardGate.Release();
        }
    }

}
