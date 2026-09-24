using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Reclaims the event state a connection held when it goes away without saying
/// so. A dropped socket never sends the leave, the withdrawal or the invitation
/// answer, so without this the team it was in stays queued for a match, and the
/// invitations it was sent stay open until they expire — the next player to look
/// at the team sees a roster with a member who is not there.
/// <para>
/// Leaving on disconnect is deliberate rather than a reconnection courtesy: the
/// connection was the member's presence in the lobby, and an event team is
/// formed here and now. A player who reconnects can rejoin, which is a decision
/// they can make; a team quietly holding a place for a socket that is gone is
/// not.
/// </para>
/// </summary>
/// <param name="teamService">Service that owns the teams.</param>
/// <param name="pushService">Service that tells the remaining members.</param>
/// <param name="invitationService">Service that owns the pending invitations.</param>
/// <param name="matchmakingService">Service that owns the waiting teams.</param>
/// <param name="logger">Logger of the service.</param>
public sealed class EventSessionCleanupService(
    EventTeamService teamService,
    EventTeamPushService pushService,
    EventInvitationService invitationService,
    EventMatchmakingService matchmakingService,
    ILogger<EventSessionCleanupService> logger)
{
    /// <summary>Releases everything the session held.</summary>
    /// <param name="session">Session that ended.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task CleanupAsync(TcpSession session, CancellationToken cancellationToken = default)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            return;
        }

        try
        {
            // Invitations name live connections, so they are dropped first: they
            // are the state that can only be wrong while the socket is gone.
            invitationService.RemoveForCharacter(characterIdentifier);

            if (session.EventTeamIdentifier is not { } teamIdentifier)
            {
                return;
            }

            var team = await teamService.FindAsync(teamIdentifier, cancellationToken);
            if (team is null)
            {
                return;
            }

            // The projection is captured before the row changes, so the push
            // carries the slot and sequence the remaining members can reconcile.
            var snapshot = EventTeamService.BuildSnapshot(team);
            var slot = snapshot.IndexOfParticipant(characterIdentifier);

            // The queue is released before the roster changes, so the team is
            // never waiting in the field with a roster that has a member missing.
            await matchmakingService.CancelAsync(teamIdentifier, cancellationToken);

            var outcome = await teamService.LeaveAsync(teamIdentifier, characterIdentifier, cancellationToken);
            if (outcome == EventLeaveOutcome.NotAMember)
            {
                return;
            }

            if (outcome == EventLeaveOutcome.TeamDisbanded)
            {
                invitationService.RemoveForTeam(teamIdentifier);
                await pushService.PushTeamClearedAsync(snapshot, session, cancellationToken);
                return;
            }

            await pushService.PushParticipantRemovedAsync(snapshot, slot, session, cancellationToken);
        }
        catch (Exception exception)
        {
            // A cleanup failure must not take the process down with the socket,
            // and it must not be silent either: what is left behind is a team the
            // next player will see with a member who is gone.
            logger.LogError(
                exception,
                "Event cleanup for character {CharacterIdentifier} after a disconnect failed",
                characterIdentifier);
        }
    }
}
