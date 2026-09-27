using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Http.Coordination;

/// <summary>
/// Sends a request to add test players to a team to the one lobby that holds it.
/// <para>
/// The command is answered by the HTTP API but the players have to exist in the
/// lobby that holds the team, and the two are separate processes. This is the
/// step in between: it turns the lobby a moderator named into the identifier
/// the coordination registry is keyed by, and hands the request down that
/// lobby's open stream.
/// </para>
/// <para>
/// The team is the one the request names, whether a player formed it or the
/// testing tools created it. The players come from the test-character pool, so
/// they are real character rows under the server account.
/// </para>
/// </summary>
/// <param name="registry">Registry of the connected lobbies.</param>
/// <param name="modes">Service that resolves the lobby a mode names.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class FakePlayerDispatchService(
    LobbyConnectionRegistryService registry,
    LobbyModeResolverService modes,
    ILogger<FakePlayerDispatchService> logger)
{
    /// <summary>How many fake players one request may ask for.</summary>
    public const int MaximumCount = EventConstants.TeamMemberLimit;

    /// <summary>Why a request could not be carried out.</summary>
    public enum DispatchOutcome
    {
        /// <summary>The lobby took the request.</summary>
        Sent,

        /// <summary>The count was outside what a team can hold.</summary>
        InvalidCount,

        /// <summary>The request named no team to fill.</summary>
        NoTeamName,

        /// <summary>No lobby of that mode exists.</summary>
        NoSuchLobby,

        /// <summary>The lobby exists but its stream is not open.</summary>
        LobbyOffline,
    }

    /// <summary>Carries a request to the lobby that will add the players.</summary>
    /// <param name="mode">Lobby mode the team is in.</param>
    /// <param name="count">How many players to add.</param>
    /// <param name="teamName">Name of the existing team to fill.</param>
    /// <param name="outcome">What happened.</param>
    /// <param name="lobbyIdentifier">Lobby the request went to, when it went anywhere.</param>
    public readonly record struct Result(
        DispatchOutcome Outcome,
        int Mode,
        int Count,
        string TeamName,
        int LobbyIdentifier);

    /// <summary>
    /// Routes a request to the lobby that runs the named mode.
    /// </summary>
    /// <param name="mode">Lobby mode the players are put into.</param>
    /// <param name="count">How many players to add.</param>
    /// <param name="teamName">Name of the existing team to fill.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Result> DispatchAsync(
        int mode,
        int count,
        string teamName,
        CancellationToken cancellationToken = default)
    {
        if (count < 1 || count > MaximumCount)
        {
            return new Result(DispatchOutcome.InvalidCount, mode, count, teamName, 0);
        }

        // A request with no team has nothing to fill, and the lobby would refuse
        // it just the same; refusing it here is what lets the moderator be told
        // which option was missing rather than only that nothing happened.
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return new Result(DispatchOutcome.NoTeamName, mode, count, teamName, 0);
        }

        var lobby = await modes.ResolveAsync(mode, cancellationToken);
        if (lobby is null)
        {
            return new Result(DispatchOutcome.NoSuchLobby, mode, count, teamName, 0);
        }

        var message = new HttpEvent
        {
            FakePlayers = new FakePlayerRequest
            {
                LobbySubtype = mode,
                Count = count,
                TeamName = teamName,
            },
        };

        if (!registry.SendTo(lobby.Identifier, message))
        {
            logger.LogWarning(
                "Lobby {LobbyIdentifier} has no open coordination stream, so its test players were not added",
                lobby.Identifier);
            return new Result(DispatchOutcome.LobbyOffline, mode, count, teamName, lobby.Identifier);
        }

        logger.LogInformation(
            "Asked lobby {LobbyIdentifier} to add {Count} test players to team {TeamName} in mode {Mode}",
            lobby.Identifier,
            count,
            teamName,
            mode);

        return new Result(DispatchOutcome.Sent, mode, count, teamName, lobby.Identifier);
    }
}
