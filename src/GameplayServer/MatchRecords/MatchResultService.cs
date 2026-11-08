using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The result of the round: which side won it, the state write that publishes
/// that, and the check that ends a round its rule's kill limit has been reached
/// in.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is apart from the round's opener because the round has two ends.</b> The
/// clock ends it — <see cref="ClockResultAsync"/> is the winner that end is
/// written with — and the rule's kill limit ends it, which is
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
/// <param name="logger">Logger of this service.</param>
public sealed class MatchResultService(
    MatchScoreService score,
    LobbySlotService lobbySlots,
    MatchRoundStateService roundState,
    ScoreboardPublishService publisher,
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
    /// A round that has reached its rule's kill limit: the side that reached it,
    /// the limit it reached, and the result code its win is written as.
    /// </summary>
    /// <param name="Team">Raw team value the room's rows hold.</param>
    /// <param name="Tickets">The rule's kill limit the side reached.</param>
    /// <param name="ResultCode">Result code the win is written as.</param>
    public sealed record TicketsReached(short Team, int Tickets, byte ResultCode);

    /// <summary>
    /// Whether one of the room's sides has reached the kill limit the round's
    /// rule is set to.
    /// </summary>
    /// <remarks>
    /// The limit is the rule's own <c>tickets</c>, in the same stored bank as the
    /// time the round opened on (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2,
    /// §3), and a rule the bank states no limit for never reaches this. The side
    /// that reached it is the round's winner, because crossing the limit is what
    /// says the round is over.
    /// </remarks>
    /// <param name="cancellationToken">Token that cancels the read of the room's sides.</param>
    /// <returns>The side that reached the limit, or <c>null</c>.</returns>
    public async Task<TicketsReached?> TicketsReachedAsync(CancellationToken cancellationToken = default)
    {
        if (!roundState.IsOpen || roundState.RoundSettings is not { Tickets: { } tickets })
        {
            return null;
        }

        var reached = score.TeamLimits(await lobbySlots.ReadAsync(cancellationToken))
            .Where(team => team.Score >= tickets)
            .OrderByDescending(team => team.Score)
            .ToList();
        if (reached.Count == 0)
        {
            return null;
        }

        var winner = reached[0];
        logger.LogInformation(
            "Team {Team} has reached the {Tickets} kills its round's rule ends on",
            winner.Team,
            tickets);
        return new TicketsReached(
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

    /// <summary>
    /// Writes the result of the round that just ended to a peer: the operations
    /// stream's state write into <c>state[2]</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The write is opcode <c>0x02</c> followed by the offset, the count and the
    /// values, so <c>02 02 01 02</c> is "state[2] = 2" — the round's winner in the
    /// decoder's own coding (<c>docs/protocol/BRIEFING_FLOW.md</c> §4, measured
    /// at every round end of `joined-when-red-team`). Nothing is written when the
    /// round ended with no result, which leaves the state block's own <c>0xff</c>
    /// where it stands.
    /// </para>
    /// <para>
    /// <b>The game's result rides the round's when the round decides it.</b> A
    /// round that is its game's last writes <c>state[2]</c> and <c>state[3]</c> in
    /// one record — <c>02 02 02 02 02</c> — which is what the captured server that
    /// decides a match writes, and a round inside a game writes
    /// <c>state[2]</c> alone (<c>docs/protocol/STATE_VECTOR.md</c> §3). The value is
    /// the side that took *every* round of the game and not the deciding round's
    /// winner: <c>replay_360824_6</c> splits its rounds one each and its match goes
    /// to the side that lost the last of them, so a split game's result is left
    /// unwritten rather than guessed
    /// (<c>docs/GAMEPLAY_SERVER.md</c> §5 item 1).
    /// </para>
    /// </remarks>
    /// <param name="context">Context the result is written through.</param>
    /// <param name="generation">Identifier set the round played under.</param>
    public async Task WriteResultAsync(PeerContext context, MatchRecordGenerationUtils generation)
    {
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
        var record = new MatchRecordFramesUtils.RecordMessage(
            generation.Operations,
            matchResult is { } decided
                ? MatchRecordBodiesUtils.StateWrite(
                    MatchRecordBodiesUtils.StateRoundResultIndex,
                    result,
                    decided)
                : MatchRecordBodiesUtils.StateWrite(MatchRecordBodiesUtils.StateRoundResultIndex, result));

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
    }

    /// <summary>
    /// Steps the winning side's rounds-won counter, once per round, publishes the
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
    /// <b>The deltas are published here and not left queued.</b> A closed round writes
    /// no further kill or death, so nothing behind it would ever take them: the whole
    /// side steps at the round's end and the screen states it immediately.
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

            if (MatchRecordBodiesUtils.TeamOfResult(roundResult) is { } winningTeam)
            {
                var slots = await lobbySlots.ReadAsync(CancellationToken.None);
                var winners = slots
                    .Where(slot => slot.Team == winningTeam)
                    .Select(slot => slot.Slot)
                    .ToList();

                var credited = score.NoteRoundWin(winners);
                await publisher.PublishAsync(context);

                logger.LogInformation(
                    "UDP {LocalPort}: team {Team} took round {RoundNumber}, so its {PlayerCount} player(s) step "
                    + "the rounds-won counter",
                    context.LocalPort,
                    winningTeam,
                    roundState.RoundNumber,
                    credited);
            }

            settledResult = await DecideMatchResultAsync();

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

    /// <summary>
    /// The game's result when the round that just ended decided it: the side that
    /// took every round of the game, or <c>null</c> when the rounds were split.
    /// </summary>
    /// <remarks>
    /// <b>A split game is not decided here.</b> Of the replays that decide a match,
    /// three were swept and each states the sweeping side; the fourth splits its
    /// rounds one each and gives the match to the side with the higher total score
    /// (<c>replay_360824_6</c>). One sample is not a rule to decide a screen with —
    /// and the score this host keeps is a floor in the modes whose objectives it
    /// cannot see — so a split game writes <c>state[2]</c> alone rather than a side
    /// it cannot support (<c>docs/GAMEPLAY_SERVER.md</c> §5 item 1).
    /// </remarks>
    private async Task<byte?> DecideMatchResultAsync()
    {
        if (!roundState.LastRoundClosedGame)
        {
            return null;
        }

        var slots = await lobbySlots.ReadAsync(CancellationToken.None);
        var holders = slots
            .Where(slot => slot.Team is not null)
            .GroupBy(slot => slot.Team!.Value)
            .Where(side => side.Any(slot => score.RoundsWon(slot.Slot) > 0))
            .Select(side => side.Key)
            .ToList();
        if (holders.Count == 1)
        {
            return MatchRecordBodiesUtils.ResultCode(holders[0]);
        }

        if (holders.Count > 1)
        {
            logger.LogInformation(
                "The game's rounds went to both sides, so no match result is written for it: which side took "
                + "a split game is not a rule this host has measured");
        }

        return null;
    }
}
