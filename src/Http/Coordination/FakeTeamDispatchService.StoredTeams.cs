using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Http.Coordination;

/// <summary>
/// The parts of the fake-team dispatch that act on a team that has a row.
/// <para>
/// They are here rather than in the main file because they answer to a
/// different premise. Everything beside them creates, changes or forgets a team
/// that lives only in a lobby's memory, and the reason a name is enough to
/// identify one is that it has no row. These two cannot use that reason: a
/// stored team is found by name in the database, and the fake players inside it
/// are rows too — which is what makes them worth a request of their own.
/// </para>
/// </summary>
/// <param name="registry">Registry of the connected lobbies.</param>
/// <param name="modes">Service that resolves the lobby a mode names.</param>
/// <param name="logger">Logger of this service.</param>
public sealed partial class FakeTeamDispatchService
{
    /// <summary>
    /// Routes a request to change the entry-decision byte on a stored team's fake
    /// players.
    /// <para>
    /// It is a separate request from the in-memory state one because the two
    /// teams are not in the same place: an in-memory team has no row and nothing
    /// to write, while a stored team's fake players are rows that a real player's
    /// own client will never press the button for.
    /// </para>
    /// </summary>
    /// <param name="mode">Mode of the lobby the team is in.</param>
    /// <param name="teamName">Name of the team whose fake players change.</param>
    /// <param name="memberState">Member state to store on each fake player.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<StateResult> DispatchMemberStateAsync(
        int mode,
        string teamName,
        int memberState,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return new StateResult(DispatchOutcome.NoTeamName, mode, teamName, 0, memberState, 0);
        }

        if (memberState < 0 || memberState > MaximumStateByte)
        {
            return new StateResult(DispatchOutcome.InvalidState, mode, teamName, 0, memberState, 0);
        }

        var lobby = await modes.ResolveAsync(mode, cancellationToken);
        if (lobby is null)
        {
            return new StateResult(DispatchOutcome.NoSuchLobby, mode, teamName, 0, memberState, 0);
        }

        var message = new HttpEvent
        {
            FakeTeamMemberState = new FakeTeamMemberStateRequest
            {
                LobbySubtype = mode,
                TeamName = teamName.Trim(),
                MemberState = memberState,
            },
        };

        if (!registry.SendTo(lobby.Identifier, message))
        {
            logger.LogWarning(
                "Lobby {LobbyIdentifier} has no open coordination stream, so its fake member state was not changed",
                lobby.Identifier);
            return new StateResult(
                DispatchOutcome.LobbyOffline, mode, teamName, 0, memberState, lobby.Identifier);
        }

        logger.LogInformation(
            "Asked lobby {LobbyIdentifier} to set the fake members of team {TeamName} to member state {MemberState} in mode {Mode}",
            lobby.Identifier,
            teamName,
            memberState,
            mode);

        return new StateResult(
            DispatchOutcome.Sent, mode, teamName, 0, memberState, lobby.Identifier);
    }

    /// <summary>
    /// Routes a request to create a dedicated event host room to the lobby whose
    /// matches that room would host.
    /// </summary>
    /// <param name="mode">Mode of the lobby the room belongs to.</param>
    /// <param name="hostCharacterIdentifier">Character to host the room, or zero to let the lobby pick.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<StateResult> DispatchHostRoomAsync(
        int mode,
        int hostCharacterIdentifier,
        CancellationToken cancellationToken = default)
    {
        var lobby = await modes.ResolveAsync(mode, cancellationToken);
        if (lobby is null)
        {
            return new StateResult(DispatchOutcome.NoSuchLobby, mode, string.Empty, 0, 0, 0);
        }

        var message = new HttpEvent
        {
            FakeHostRoom = new FakeHostRoomRequest
            {
                LobbySubtype = mode,
                HostCharacterIdentifier = hostCharacterIdentifier,
            },
        };

        if (!registry.SendTo(lobby.Identifier, message))
        {
            logger.LogWarning(
                "Lobby {LobbyIdentifier} has no open coordination stream, so no host room was created",
                lobby.Identifier);
            return new StateResult(
                DispatchOutcome.LobbyOffline, mode, string.Empty, 0, 0, lobby.Identifier);
        }

        logger.LogInformation(
            "Asked lobby {LobbyIdentifier} to create a dedicated host room in mode {Mode}",
            lobby.Identifier,
            mode);

        return new StateResult(DispatchOutcome.Sent, mode, string.Empty, 0, 0, lobby.Identifier);
    }

}
