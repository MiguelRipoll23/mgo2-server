using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Tells both teams how their match ended. The two commands carry the same
/// state-update record and differ only in what the client does with it: the
/// winner stays on the Survival screen with an advanced streak, and the loser
/// returns to team creation.
/// <para>
/// The teams are re-read before the payload is built, because the outcome moved
/// their streaks. Sending the streak the client already had would show a team a
/// reward it was just paid without the streak that earned it.
/// </para>
/// </summary>
/// <param name="teamService">Service that owns the teams, read for their final streak.</param>
/// <param name="sessionDirectory">Lookup of the sessions this lobby is serving.</param>
/// <param name="sessionHelper">Helper used to write the pushes.</param>
public sealed class EventOutcomePushService(
    EventTeamService teamService,
    EventSessionDirectoryService sessionDirectory,
    SessionHelper sessionHelper)
{
    /// <summary>Pushes the outcome to every session of both teams.</summary>
    /// <param name="winningTeamIdentifier">Team that won.</param>
    /// <param name="losingTeamIdentifier">Team that lost.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Sessions the outcome was delivered to.</returns>
    public async Task<int> PushOutcomeAsync(
        int winningTeamIdentifier,
        int losingTeamIdentifier,
        CancellationToken cancellationToken = default)
    {
        var delivered = 0;
        delivered += await PushToTeamAsync(
            winningTeamIdentifier,
            CommandConstants.EventMatchWinnerContinue,
            cancellationToken);
        delivered += await PushToTeamAsync(
            losingTeamIdentifier,
            CommandConstants.EventMatchLoserReturn,
            cancellationToken);
        return delivered;
    }

    private async Task<int> PushToTeamAsync(
        int teamIdentifier,
        ushort command,
        CancellationToken cancellationToken)
    {
        if (teamIdentifier <= 0)
        {
            return 0;
        }

        var sessions = sessionDirectory.TeamSessions(teamIdentifier, excludedSession: null);
        if (sessions.Count == 0)
        {
            // A team whose members have all left needs no notification; the
            // outcome is already recorded, and nothing is owed to a screen that
            // is not connected.
            return 0;
        }

        var team = await teamService.FindAsync(teamIdentifier, cancellationToken);
        if (team is null)
        {
            return 0;
        }

        var snapshot = EventTeamService.BuildSnapshot(team);
        var writer = new PacketWriter();
        EventAssignmentUtils.WriteStateUpdate(
            writer,
            snapshot.SnapshotIdentifier,
            snapshot.Sequence,
            snapshot);
        var payload = writer.Build();

        var delivered = 0;
        foreach (TcpSession session in sessions)
        {
            await sessionHelper.SendPacketAsync(session, command, payload, cancellationToken);
            delivered++;
        }

        return delivered;
    }
}
