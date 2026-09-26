using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Http.Coordination;

/// <summary>
/// Sends a request to create a team that exists only in a lobby's memory to the
/// one lobby that will show it, and carries the questions about the teams a
/// lobby is already holding.
/// <para>
/// The request is answered by the HTTP API but the team has to exist in the
/// lobby that lists it, and the two are separate processes. This is the step in
/// between: it turns the lobby a caller named into the identifier the
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
/// <param name="modes">Service that resolves the lobby a mode names.</param>
/// <param name="queries">Service that holds the questions a lobby is answering.</param>
/// <param name="logger">Logger of this service.</param>
public sealed partial class FakeTeamDispatchService(
    LobbyConnectionRegistryService registry,
    LobbyModeResolverService modes,
    LobbyTeamQueryService queries,
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

        /// <summary>The request named no team.</summary>
        NoTeamName,

        /// <summary>A state byte was outside the range the client reads.</summary>
        InvalidState,

        /// <summary>No lobby of that mode exists.</summary>
        NoSuchLobby,

        /// <summary>The lobby exists but its stream is not open.</summary>
        LobbyOffline,

        /// <summary>The lobby did not answer in time.</summary>
        NoAnswer,
    }

    /// <summary>Largest value a state byte the client reads can carry.</summary>
    public const int MaximumStateByte = 255;

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

        var lobby = await modes.ResolveAsync(mode, cancellationToken);
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

    /// <summary>Carries a request to change an in-memory team's state.</summary>
    /// <param name="Outcome">What happened.</param>
    /// <param name="Mode">Lobby mode the team is in.</param>
    /// <param name="TeamName">Name of the in-memory team.</param>
    /// <param name="State">Team state that was asked for.</param>
    /// <param name="MemberState">Member state that was asked for, zero to follow the team.</param>
    /// <param name="LobbyIdentifier">Lobby the request went to, when it went anywhere.</param>
    public readonly record struct StateResult(
        DispatchOutcome Outcome,
        int Mode,
        string TeamName,
        int State,
        int MemberState,
        int LobbyIdentifier);

    /// <summary>
    /// Routes a request to change the state of an in-memory team to the lobby that
    /// holds it.
    /// <para>
    /// The team has no row and so no identifier the API could name; the request
    /// carries its display name and the lobby resolves it against the teams it
    /// holds itself. Nothing is written: the change is visible in the lobby's own
    /// list and detail replies until the lobby restarts.
    /// </para>
    /// </summary>
    /// <param name="mode">Lobby mode the team is in.</param>
    /// <param name="teamName">Name of the in-memory team whose state changes.</param>
    /// <param name="state">Team state to store, as the client's own phase byte.</param>
    /// <param name="memberState">Member state to force on the roster, or zero to follow the team.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<StateResult> DispatchStateAsync(
        int mode,
        string teamName,
        int state,
        int memberState,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return new StateResult(DispatchOutcome.NoTeamName, mode, teamName, state, memberState, 0);
        }

        if (state < 0 || state > MaximumStateByte || memberState < 0 || memberState > MaximumStateByte)
        {
            return new StateResult(DispatchOutcome.InvalidState, mode, teamName, state, memberState, 0);
        }

        var lobby = await modes.ResolveAsync(mode, cancellationToken);
        if (lobby is null)
        {
            return new StateResult(DispatchOutcome.NoSuchLobby, mode, teamName, state, memberState, 0);
        }

        var message = new HttpEvent
        {
            FakeTeamState = new FakeTeamStateRequest
            {
                LobbySubtype = mode,
                TeamName = teamName,
                State = state,
                MemberState = memberState,
            },
        };

        if (!registry.SendTo(lobby.Identifier, message))
        {
            logger.LogWarning(
                "Lobby {LobbyIdentifier} has no open coordination stream, so its fake team state was not changed",
                lobby.Identifier);
            return new StateResult(DispatchOutcome.LobbyOffline, mode, teamName, state, memberState, lobby.Identifier);
        }

        logger.LogInformation(
            "Asked lobby {LobbyIdentifier} to set in-memory team {TeamName} to state {State} in mode {Mode}",
            lobby.Identifier,
            teamName,
            state,
            mode);

        return new StateResult(DispatchOutcome.Sent, mode, teamName, state, memberState, lobby.Identifier);
    }

    /// <summary>
    /// Routes a request to write one in-memory team out as a row so the queue
    /// can pair it.
    /// <para>
    /// This is the one request in the group that writes, and it is the pairing
    /// service on the lobby that decides what it writes rather than this one:
    /// the coordinator names a team and a mode, and the lobby holds both the
    /// team and the rule about which modes it can pair.
    /// </para>
    /// </summary>
    /// <param name="mode">Mode of the lobby the team is in.</param>
    /// <param name="teamName">Name of the in-memory team.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<StateResult> DispatchPairingAsync(
        int mode,
        string teamName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return new StateResult(DispatchOutcome.NoTeamName, mode, teamName, 0, 0, 0);
        }

        var lobby = await modes.ResolveAsync(mode, cancellationToken);
        if (lobby is null)
        {
            return new StateResult(DispatchOutcome.NoSuchLobby, mode, teamName, 0, 0, 0);
        }

        var message = new HttpEvent
        {
            FakeTeamPairing = new FakeTeamPairingRequest
            {
                LobbySubtype = mode,
                TeamName = teamName.Trim(),
            },
        };

        if (!registry.SendTo(lobby.Identifier, message))
        {
            logger.LogWarning(
                "Lobby {LobbyIdentifier} has no open coordination stream, so its fake team was not made pairable",
                lobby.Identifier);
            return new StateResult(DispatchOutcome.LobbyOffline, mode, teamName, 0, 0, lobby.Identifier);
        }

        logger.LogInformation(
            "Asked lobby {LobbyIdentifier} to make in-memory team {TeamName} pairable in mode {Mode}",
            lobby.Identifier,
            teamName,
            mode);

        return new StateResult(DispatchOutcome.Sent, mode, teamName, 0, 0, lobby.Identifier);
    }

    /// <summary>What a question about a lobby's in-memory teams did.</summary>
    /// <param name="Outcome">What happened.</param>
    /// <param name="Mode">Lobby mode that was asked about.</param>
    /// <param name="Teams">Teams the lobby reported, empty when it reported none.</param>
    public readonly record struct QueryResult(
        DispatchOutcome Outcome,
        int Mode,
        IReadOnlyList<FakeTeamSummary> Teams);

    /// <summary>
    /// Asks the lobby of a mode what it holds in memory.
    /// <para>
    /// This is the one request of the group that waits for an answer rather
    /// than handing something over and moving on: the teams live in the lobby's
    /// process, so the API cannot list them itself, and a caller that was told
    /// about a team that is not there would be told about a team it cannot
    /// change either.
    /// </para>
    /// </summary>
    /// <param name="mode">Lobby mode to ask about.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The teams the lobby holds, or why there are none.</returns>
    public async Task<QueryResult> ListAsync(int mode, CancellationToken cancellationToken = default) =>
        await AskAsync(mode, FakeTeamQueryAction.List, teamName: string.Empty, cancellationToken);

    /// <summary>
    /// Asks the lobby of a mode to forget one of the teams it holds in memory.
    /// </summary>
    /// <param name="mode">Lobby mode the team is in.</param>
    /// <param name="teamName">Name of the in-memory team to remove.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The team that was removed, or why nothing was.</returns>
    public async Task<QueryResult> RemoveAsync(
        int mode,
        string teamName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return new QueryResult(DispatchOutcome.NoTeamName, mode, []);
        }

        return await AskAsync(mode, FakeTeamQueryAction.Remove, teamName.Trim(), cancellationToken);
    }

    private async Task<QueryResult> AskAsync(
        int mode,
        FakeTeamQueryAction action,
        string teamName,
        CancellationToken cancellationToken)
    {
        var lobby = await modes.ResolveAsync(mode, cancellationToken);
        if (lobby is null)
        {
            return new QueryResult(DispatchOutcome.NoSuchLobby, mode, []);
        }

        var requestIdentifier = queries.NextRequestIdentifier();
        if (!queries.Expect(requestIdentifier, out var answer))
        {
            return new QueryResult(DispatchOutcome.NoAnswer, mode, []);
        }

        var message = new HttpEvent
        {
            FakeTeamQuery = new FakeTeamQueryRequest
            {
                RequestIdentifier = requestIdentifier,
                LobbySubtype = mode,
                Action = action,
                TeamName = teamName,
            },
        };

        if (!registry.SendTo(lobby.Identifier, message))
        {
            queries.Abandon(requestIdentifier);
            logger.LogWarning(
                "Lobby {LobbyIdentifier} has no open coordination stream, so it was not asked about its teams",
                lobby.Identifier);
            return new QueryResult(DispatchOutcome.LobbyOffline, mode, []);
        }

        try
        {
            var listing = await answer.WaitAsync(LobbyTeamQueryService.Timeout, cancellationToken);
            logger.LogInformation(
                "Lobby {LobbyIdentifier} reported {Count} in-memory team(s) for {Action} in mode {Mode}",
                lobby.Identifier,
                listing.Teams.Count,
                action,
                mode);

            return new QueryResult(DispatchOutcome.Sent, mode, listing.Teams);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            queries.Abandon(requestIdentifier);
            throw;
        }
        catch (Exception)
        {
            queries.Abandon(requestIdentifier);
            logger.LogWarning(
                "Lobby {LobbyIdentifier} did not answer the team query in mode {Mode} in time",
                lobby.Identifier,
                mode);
            return new QueryResult(DispatchOutcome.NoAnswer, mode, []);
        }
    }
}
