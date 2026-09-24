using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>
/// Serves the shared entry request, whose meaning depends on which lobby it
/// arrives in.
/// <para>
/// The request carries no body in either case: the event is the one whose detail
/// screen the connection last opened, and the team is the one the connection is
/// attached to. Both are routing context the server holds, because the client
/// names neither again — a request that repeated them would be a second source
/// of truth for what the player is looking at.
/// </para>
/// </summary>
public sealed class EnterEventHandler(
    TournamentRegistrationService registrationService,
    EventEntryService entryService,
    EventTeamService teamService,
    EventTeamPushService pushService,
    EventInvitationService invitationService,
    EventMatchmakingService matchmakingService,
    LobbyService lobbyService,
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // A body means the client asked for something this command does not do,
        // and answering it as an entry would act on the wrong request.
        if (!options.Value.Enabled
            || session.CharacterIdentifier is not { } characterIdentifier
            || packet.Payload.Length != 0)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var lobbyIdentifier = session.LobbyIdentifier;
        if (lobbyIdentifier is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var lobby = await lobbyService.FindByIdAsync(lobbyIdentifier.Value, cancellationToken);
        var route = EventEntryUtils.Resolve(
            lobby.SubtypeIdentifier,
            hasTeam: session.EventTeamIdentifier is not null,
            hasSelectedEvent: session.SelectedEventIdentifier is not null);

        switch (route)
        {
            case EventEntryRoute.SubmitTournamentTeam:
                await SubmitTournamentTeamAsync(session, characterIdentifier, cancellationToken);
                return;

            case EventEntryRoute.EnterEventSolo:
                await EnterEventAsync(session, characterIdentifier, lobbyIdentifier.Value, lobby.SubtypeIdentifier, cancellationToken);
                return;

            case EventEntryRoute.CancelSurvivalEntry:
                await CancelSurvivalEntryAsync(session, characterIdentifier, cancellationToken);
                return;

            default:
                // Reporting success here would tell a client it had entered an
                // event the server did not register it for.
                await sessionHelper.SendResultAsync(
                    session,
                    CommandConstants.EnterEventResult,
                    ErrorCodeConstants.ResultFeatureNotImplemented,
                    cancellationToken);
                return;
        }
    }

    /// <summary>Submits the caller's team into a Tournament event.</summary>
    /// <param name="session">Connection entering.</param>
    /// <param name="characterIdentifier">Character entering.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task SubmitTournamentTeamAsync(
        TcpSession session,
        int characterIdentifier,
        CancellationToken cancellationToken)
    {
        var eventIdentifier = session.SelectedEventIdentifier;
        var teamIdentifier = session.EventTeamIdentifier;
        if (eventIdentifier is not { } eventId || teamIdentifier is not { } teamId)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var outcome = await registrationService.SubmitTeamAsync(
            eventId,
            teamId,
            characterIdentifier,
            cancellationToken);

        var result = outcome switch
        {
            // A resubmission is the same request, so it is answered as success
            // rather than as a refusal the player would read as a failure.
            TournamentSubmitOutcome.Registered or TournamentSubmitOutcome.AlreadyRegistered =>
                ErrorCodeConstants.ResultNone,
            _ => EventConstants.ResultEventNotFound,
        };

        await sessionHelper.SendResultAsync(
            session,
            CommandConstants.EnterEventResult,
            result,
            cancellationToken);
    }

    /// <summary>
    /// Enters the caller itself into the event whose detail it has open. The
    /// entrant is the character's own team of one, so the session is attached to
    /// it: every screen that follows — the field list, the active-game snapshot,
    /// the removal request — addresses the entrant through that attachment.
    /// </summary>
    /// <param name="session">Connection entering.</param>
    /// <param name="characterIdentifier">Character entering.</param>
    /// <param name="lobbyIdentifier">Lobby the entry is made in.</param>
    /// <param name="lobbySubtype">Game type of that lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task EnterEventAsync(
        TcpSession session,
        int characterIdentifier,
        int lobbyIdentifier,
        int lobbySubtype,
        CancellationToken cancellationToken)
    {
        if (session.SelectedEventIdentifier is not { } eventIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var result = await entryService.EnterAsync(
            characterIdentifier,
            eventIdentifier,
            lobbyIdentifier,
            lobbySubtype,
            cancellationToken);

        var (accepted, teamIdentifier) = result.Outcome switch
        {
            // A repeat is answered as success: the client asked for a state it
            // is already in.
            EventEntryOutcome.Entered or EventEntryOutcome.AlreadyEntered => (true, result.TeamIdentifier),

            // A character that owns a formed team keeps it: the entry this
            // command performs is the individual one, and repointing the session
            // at a second team of its own would take the client's roster away.
            _ => (false, 0),
        };

        if (accepted && teamIdentifier > 0)
        {
            session.EventTeamIdentifier = teamIdentifier;
        }

        await sessionHelper.SendResultAsync(
            session,
            CommandConstants.EnterEventResult,
            accepted ? ErrorCodeConstants.ResultNone : EventConstants.ResultEventNotFound,
            cancellationToken);
    }

    /// <summary>Withdraws the caller's Survival team from the waiting field.</summary>
    /// <param name="session">Connection asking.</param>
    /// <param name="characterIdentifier">Character asking.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task CancelSurvivalEntryAsync(
        TcpSession session,
        int characterIdentifier,
        CancellationToken cancellationToken)
    {
        if (session.EventTeamIdentifier is not { } teamIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var team = await teamService.FindAsync(teamIdentifier, cancellationToken);
        if (team is null)
        {
            session.EventTeamIdentifier = null;
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // Only the team's leader cancels its entry, so a member cannot withdraw a
        // team the whole roster is waiting in.
        if (team.OwnerCharacterIdentifier != characterIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // The projection is captured before the row changes, so the push carries
        // the identifiers the members can still reconcile.
        var snapshot = EventTeamService.BuildSnapshot(team);

        // The queue is released first: a team that is being withdrawn must not be
        // pairable in the moment between the two.
        await matchmakingService.CancelAsync(teamIdentifier, cancellationToken);

        var outcome = await teamService.LeaveAsync(teamIdentifier, characterIdentifier, cancellationToken);
        session.EventTeamIdentifier = null;
        invitationService.RemoveForCharacter(characterIdentifier);
        invitationService.RemoveForTeam(teamIdentifier);

        if (outcome == EventLeaveOutcome.TeamDisbanded)
        {
            // Withdrawal ends the team, which is why the client is told its team
            // is cleared rather than left showing a roster it no longer has.
            await pushService.PushTeamClearedAsync(snapshot, session, cancellationToken);
        }

        await sessionHelper.SendResultAsync(
            session,
            CommandConstants.EnterEventResult,
            ErrorCodeConstants.ResultNone,
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.EnterEventResult,
            EventConstants.ResultEventNotFound,
            cancellationToken);
}
