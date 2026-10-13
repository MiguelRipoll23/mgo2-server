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
/// <param name="logger">Logger of this service.</param>
public sealed class MatchResultService(
    MatchScoreService score,
    LobbySlotService lobbySlots,
    MatchRoundStateService roundState,
    ILogger<MatchResultService> logger)
{
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
    /// The match's own result at <c>state[3]</c> is not written: how many rounds a
    /// match is has not been resolved, so a host that wrote one would be stating a
    /// match it cannot count (<c>docs/GAMEPLAY_SERVER.md</c> §5 item 7).
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

        if (!MatchRecordPeerUtils.IsAlive(context.Session))
        {
            return;
        }

        var record = new MatchRecordFramesUtils.RecordMessage(
            generation.Operations,
            MatchRecordBodiesUtils.StateWrite(MatchRecordBodiesUtils.StateRoundResultIndex, result));

        logger.LogInformation(
            "UDP {LocalPort}: writing round {RoundNumber}'s result to {RemoteAddress}: state[{Offset}] = {Result}",
            context.LocalPort,
            roundState.RoundNumber,
            context.Remote,
            MatchRecordBodiesUtils.StateRoundResultIndex,
            result);

        await MatchRecordSendUtils.SendAsync(context, [record]);
    }
}
