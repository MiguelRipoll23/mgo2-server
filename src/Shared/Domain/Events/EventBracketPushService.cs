using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Tells the entrants of a Tournament field how the bracket moved. It is driven
/// by the advance a finished match produced rather than by the match itself, so
/// the records it sends describe the bracket that now exists rather than the one
/// the result was applied to.
/// <para>
/// The status column is the field's, not one recipient's: every entrant's byte
/// is the state of its team row, and a team that no longer exists contributes a
/// neutral zero. The bitmap is the round's, and the standing push is decided by
/// whether the bracket has a champion at all — a bracket in play has nobody to
/// stand.
/// </para>
/// <para>
/// The reward that travels with the standings is the one recorded for the
/// recipient's own team in the match that decided the bracket, read from the
/// ledger rather than recomputed, so the amount the client is shown is the
/// amount the team was paid.
/// </para>
/// </summary>
/// <param name="bracketService">Service that rebuilds the bracket from its ledger.</param>
/// <param name="teamService">Service that owns the teams the field is made of.</param>
/// <param name="rewardService">Service that holds the payments a match produced.</param>
/// <param name="sessionDirectory">Lookup of the sessions this lobby is serving.</param>
/// <param name="sessionHelper">Helper used to write the pushes.</param>
public sealed class EventBracketPushService(
    TournamentBracketService bracketService,
    EventTeamService teamService,
    EventRewardService rewardService,
    EventSessionDirectoryService sessionDirectory,
    SessionHelper sessionHelper)
{
    /// <summary>Pushes the bracket as it stands to every entrant's sessions.</summary>
    /// <param name="advance">Advance a finished match produced.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Sessions the bracket was delivered to.</returns>
    public async Task<int> PushAdvanceAsync(
        TournamentAdvance advance,
        CancellationToken cancellationToken = default)
    {
        if (advance.EventIdentifier <= 0)
        {
            return 0;
        }

        var tree = await bracketService.RebuildAsync(advance.EventIdentifier, cancellationToken);

        // The order the bracket was drawn in, not the order teams were submitted
        // in: a field that has been decided has released its places, and what is
        // being pushed is the draw that was made rather than a field that can no
        // longer be enumerated.
        var seeds = await bracketService.LoadFrozenSeedOrderAsync(
            advance.EventIdentifier,
            cancellationToken);
        if (tree is null || seeds.Count == 0)
        {
            return 0;
        }

        var statuses = await BuildEntrantStatusesAsync(seeds, cancellationToken);
        var rows = EventBracketUtils.BuildRoundBitmaps(tree, seeds);
        var standings = EventBracketUtils.BuildStandings(
            advance.ChampionTeamIdentifier,
            advance.RunnerUpTeamIdentifier);

        // A bracket still being played has no standings to push, so the ledger is
        // only read once it has a champion to pay.
        var payments = advance.ChampionTeamIdentifier == 0
            ? new List<EventRoundReward>()
            : await rewardService.ListPaymentsAsync(advance.MatchIdentifier, cancellationToken);

        var delivered = 0;
        foreach (var teamIdentifier in seeds.Distinct())
        {
            if (teamIdentifier <= 0)
            {
                continue;
            }

            var sessions = sessionDirectory.TeamSessions(teamIdentifier, excludedSession: null);
            if (sessions.Count == 0)
            {
                // A team whose members have left needs nothing: the bracket is
                // recorded, and there is no screen to show it on.
                continue;
            }

            var team = await teamService.FindAsync(teamIdentifier, cancellationToken);
            if (team is null)
            {
                continue;
            }

            var snapshot = EventTeamService.BuildSnapshot(team);
            var statePayload = BuildStatePayload(snapshot, rows, statuses);
            var roundPayload = advance.Round is >= 1 and <= EventConstants.BracketMaximumRound
                ? BuildRoundPayload(snapshot, tree, seeds, statuses, advance.Round)
                : null;
            var standingsPayload = advance.ChampionTeamIdentifier == 0
                ? null
                : BuildStandingsPayload(snapshot, standings, payments, teamIdentifier);

            foreach (var session in sessions)
            {
                if (roundPayload is not null)
                {
                    await sessionHelper.SendPacketAsync(
                        session,
                        CommandConstants.EventRoundResult,
                        roundPayload,
                        cancellationToken);
                }

                await sessionHelper.SendPacketAsync(
                    session,
                    CommandConstants.EventBracketState,
                    statePayload,
                    cancellationToken);

                if (standingsPayload is not null)
                {
                    await sessionHelper.SendPacketAsync(
                        session,
                        CommandConstants.EventFinalStandings,
                        standingsPayload,
                        cancellationToken);
                }

                delivered++;
            }
        }

        return delivered;
    }

    private byte[] BuildRoundPayload(
        EventSnapshot snapshot,
        TournamentBracketTree tree,
        IReadOnlyList<int> seeds,
        byte[] statuses,
        int round)
    {
        var writer = new PacketWriter();
        EventBracketUtils.WriteRoundResult(
            writer,
            snapshot.SnapshotIdentifier,
            snapshot.Sequence,
            EventConstants.ActiveEventAssignedState,
            round,
            EventBracketUtils.BuildRoundBitmap(tree, seeds, round),
            statuses);
        return writer.Build();
    }

    private static byte[] BuildStatePayload(
        EventSnapshot snapshot,
        IReadOnlyList<int[]> rows,
        byte[] statuses)
    {
        var writer = new PacketWriter();
        EventBracketUtils.WriteBracketState(writer, snapshot.SnapshotIdentifier, rows, statuses);
        return writer.Build();
    }

    private static byte[] BuildStandingsPayload(
        EventSnapshot snapshot,
        int[] standings,
        List<EventRoundReward> payments,
        int teamIdentifier)
    {
        var writer = new PacketWriter();
        EventBracketUtils.WriteFinalStandings(
            writer,
            snapshot.SnapshotIdentifier,
            snapshot.Sequence,
            standings,
            payments
                .FirstOrDefault(payment => payment.TeamIdentifier == teamIdentifier)
                ?.Reward ?? 0);
        return writer.Build();
    }

    private async Task<byte[]> BuildEntrantStatusesAsync(
        IReadOnlyList<int> seeds,
        CancellationToken cancellationToken)
    {
        var statuses = new byte[seeds.Count];
        for (var index = 0; index < seeds.Count; index++)
        {
            var teamIdentifier = seeds[index];
            if (teamIdentifier <= 0)
            {
                continue;
            }

            // A team that was knocked out has had its row deleted, so its entrant
            // place reports the same neutral zero an empty slot does rather than a
            // state a departed team left behind.
            var team = await teamService.FindAsync(teamIdentifier, cancellationToken);
            statuses[index] = team is null ? (byte)0 : (byte)Math.Clamp(team.State, 0, byte.MaxValue);
        }

        return statuses;
    }
}
