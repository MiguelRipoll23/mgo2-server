using Mgo2Server.Http.Coordination;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Sends a request to create a team that exists only in a lobby's memory to the
/// one lobby that will show it.
/// <para>
/// The command is answered by the HTTP API but the team has to exist in the
/// lobby that lists it, and the two are separate processes. This is the step in
/// between: it turns the lobby a moderator named into the identifier the
/// coordination registry is keyed by, and hands the request down that lobby's
/// open stream.
/// </para>
/// <para>
/// Nothing is written: the team lives in the lobby's memory and goes away with
/// it, which is what makes it a testing device rather than a second source of
/// teams.
/// </para>
/// </summary>
/// <param name="registry">Registry of the connected lobbies.</param>
/// <param name="lobbyService">Service that resolves the lobby a mode names.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class FakeTeamDispatchService(
    LobbyConnectionRegistryService registry,
    LobbyService lobbyService,
    ILogger<FakeTeamDispatchService> logger)
{
    /// <summary>How many players one fake team may hold, leader included.</summary>
    public const int MaximumCount = EventConstants.TeamMemberLimit;

    /// <summary>Why a request could not be carried out.</summary>
    public enum DispatchOutcome
    {
        /// <summary>The lobby took the request.</summary>
        Sent,

        /// <summary>The count was outside what a team can hold.</summary>
        InvalidCount,

        /// <summary>No lobby of that mode exists.</summary>
        NoSuchLobby,

        /// <summary>The lobby exists but its stream is not open.</summary>
        LobbyOffline,
    }

    /// <summary>Carries a request to the lobby that will hold the team.</summary>
    /// <param name="outcome">What happened.</param>
    /// <param name="mode">Lobby mode the team is created in.</param>
    /// <param name="count">How many players the team holds.</param>
    /// <param name="teamName">Name the team is given, or blank for a composed one.</param>
    /// <param name="playerPrefix">Name the players are shown with, or blank.</param>
    /// <param name="lobbyIdentifier">Lobby the request went to, when it went anywhere.</param>
    public readonly record struct Result(
        DispatchOutcome Outcome,
        int Mode,
        int Count,
        string TeamName,
        string PlayerPrefix,
        int LobbyIdentifier);

    /// <summary>Routes a request to the lobby that runs the named mode.</summary>
    /// <param name="mode">Lobby mode the team is created in.</param>
    /// <param name="count">How many players the team holds, leader included.</param>
    /// <param name="teamName">Name the team is given, or blank for a composed one.</param>
    /// <param name="playerPrefix">Name the players are shown with, or blank.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Result> DispatchAsync(
        int mode,
        int count,
        string teamName,
        string playerPrefix,
        CancellationToken cancellationToken = default)
    {
        if (count < 1 || count > MaximumCount)
        {
            return new Result(DispatchOutcome.InvalidCount, mode, count, teamName, playerPrefix, 0);
        }

        var lobby = await ResolveLobbyAsync(mode, cancellationToken);
        if (lobby is null)
        {
            return new Result(DispatchOutcome.NoSuchLobby, mode, count, teamName, playerPrefix, 0);
        }

        var message = new HttpEvent
        {
            FakeTeam = new FakeTeamRequest
            {
                LobbySubtype = mode,
                Count = count,
                TeamName = teamName,
                PlayerPrefix = playerPrefix,
            },
        };

        if (!registry.SendTo(lobby.Identifier, message))
        {
            logger.LogWarning(
                "Lobby {LobbyIdentifier} has no open coordination stream, so its fake team was not created",
                lobby.Identifier);
            return new Result(DispatchOutcome.LobbyOffline, mode, count, teamName, playerPrefix, lobby.Identifier);
        }

        logger.LogInformation(
            "Asked lobby {LobbyIdentifier} to create an in-memory team in mode {Mode}",
            lobby.Identifier,
            mode);

        return new Result(DispatchOutcome.Sent, mode, count, teamName, playerPrefix, lobby.Identifier);
    }

    /// <summary>
    /// Finds the running lobby of a mode. Several lobbies may share a mode, so
    /// the first is taken rather than insisting a deployment only ever runs
    /// one. The team goes to a lobby that exists rather than to none.
    /// </summary>
    /// <param name="mode">Lobby mode to resolve.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<LobbyResponse?> ResolveLobbyAsync(int mode, CancellationToken cancellationToken)
    {
        if (!EventConstants.IsEventSelector(mode))
        {
            return null;
        }

        var lobbies = await lobbyService.GetLobbiesAsync(cancellationToken);
        foreach (var lobby in lobbies)
        {
            if (lobby.SubtypeIdentifier == mode)
            {
                return lobby;
            }
        }

        return null;
    }
}
