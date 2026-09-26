using Mgo2Server.Shared.Domain.News;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Acts on everything the coordinator sends down this lobby's stream.
/// <para>
/// The connection loop only keeps the stream open; what a message asks for is a
/// different concern, and a host that runs no event lobby has none of the
/// handlers. Each handler is optional here for that reason, and each is wrapped
/// so a request that cannot be carried out does not end the stream of the whole
/// lobby: the next flash news still has to arrive.
/// </para>
/// <para>
/// A gameplay lobby knows nothing about Discord or about HTTP: the message is
/// the protocol, and the payload of a ticker packet is rebuilt here because the
/// client protocol stays where the protocol is.
/// </para>
/// </summary>
/// <param name="flashNewsService">Service that writes an announcement to this lobby's clients.</param>
/// <param name="logger">Logger of this service.</param>
/// <param name="fakePlayerRequests">
/// Service that acts on a request to add fake players to a team of this lobby.
/// Left null by a host that runs no event lobby, which is told so rather than
/// having the request dropped without a word.
/// </param>
/// <param name="fakeTeamRequests">
/// Service that acts on a request to create an in-memory team in this lobby.
/// Left null by a host that runs no event lobby, for the same reason.
/// </param>
/// <param name="fakeTeamStateRequests">
/// Service that acts on a request to change the state of an in-memory team of
/// this lobby. Left null by a host that runs no event lobby, for the same reason.
/// </param>
/// <param name="fakeTeamQueries">
/// Service that answers a question about the in-memory teams of this lobby.
/// Left null by a host that runs no event lobby, for the same reason.
/// </param>
/// <param name="fakeTeamPairings">
/// Service that writes an in-memory team out as a row so it can be paired.
/// Left null by a host that runs no event lobby, for the same reason.
/// </param>
/// <param name="fakeTeamMemberStates">
/// Service that changes the entry-decision byte of a team's fake players.
/// Left null by a host that runs no event lobby, for the same reason.
/// </param>
/// <param name="fakeHostRooms">
/// Service that writes the dedicated event host room a pairing needs.
/// Left null by a host that runs no event lobby, for the same reason.
/// </param>
public sealed class LobbyCommandApplyService(
    FlashNewsService flashNewsService,
    ILogger<LobbyCommandApplyService> logger,
    FakePlayerRequestHandlerService? fakePlayerRequests = null,
    FakeTeamRequestHandlerService? fakeTeamRequests = null,
    FakeTeamStateRequestHandlerService? fakeTeamStateRequests = null,
    FakeTeamQueryRequestHandlerService? fakeTeamQueries = null,
    FakeTeamPairingRequestHandlerService? fakeTeamPairings = null,
    FakeTeamMemberStateRequestHandlerService? fakeTeamMemberStates = null,
    FakeHostRoomRequestHandlerService? fakeHostRooms = null)
{
    /// <summary>Applies one message the coordinator sent.</summary>
    /// <param name="message">Message to act on.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public Task ApplyAsync(HttpEvent message, CancellationToken cancellationToken)
    {
        switch (message.EventCase)
        {
            case HttpEvent.EventOneofCase.FakePlayers:
                var players = message.FakePlayers;
                return GuardAsync(
                    fakePlayerRequests is null
                        ? null
                        : () => fakePlayerRequests.HandleAsync(players, cancellationToken),
                    "This host cannot create fake players; the request was refused",
                    "A fake player request could not be carried out",
                    cancellationToken);

            case HttpEvent.EventOneofCase.FakeTeam:
                var team = message.FakeTeam;
                return GuardAsync(
                    fakeTeamRequests is null
                        ? null
                        : () => fakeTeamRequests.HandleAsync(team, cancellationToken),
                    "This host cannot create fake teams; the request was refused",
                    "A fake team request could not be carried out",
                    cancellationToken);

            case HttpEvent.EventOneofCase.FakeTeamState:
                var state = message.FakeTeamState;
                return GuardAsync(
                    fakeTeamStateRequests is null
                        ? null
                        : () => fakeTeamStateRequests.HandleAsync(state, cancellationToken),
                    "This host cannot change fake team state; the request was refused",
                    "A fake team state request could not be carried out",
                    cancellationToken);

            case HttpEvent.EventOneofCase.FakeTeamQuery:
                var query = message.FakeTeamQuery;
                return GuardAsync(
                    fakeTeamQueries is null
                        ? null
                        : () => fakeTeamQueries.HandleAsync(query, cancellationToken),
                    "This host cannot answer a team query; the question was refused",
                    "A fake team query could not be answered",
                    cancellationToken);

            case HttpEvent.EventOneofCase.FakeTeamPairing:
                var pairing = message.FakeTeamPairing;
                return GuardAsync(
                    fakeTeamPairings is null
                        ? null
                        : () => fakeTeamPairings.HandleAsync(pairing, cancellationToken),
                    "This host cannot make a fake team pairable; the request was refused",
                    "A fake team pairing request could not be carried out",
                    cancellationToken);

            case HttpEvent.EventOneofCase.FakeTeamMemberState:
                var memberState = message.FakeTeamMemberState;
                return GuardAsync(
                    fakeTeamMemberStates is null
                        ? null
                        : () => fakeTeamMemberStates.HandleAsync(memberState, cancellationToken),
                    "This host cannot change fake member state; the request was refused",
                    "A fake team member state request could not be carried out",
                    cancellationToken);

            case HttpEvent.EventOneofCase.FakeHostRoom:
                var hostRoom = message.FakeHostRoom;
                return GuardAsync(
                    fakeHostRooms is null
                        ? null
                        : () => fakeHostRooms.HandleAsync(hostRoom, cancellationToken),
                    "This host cannot create a dedicated host room; the request was refused",
                    "A host room request could not be carried out",
                    cancellationToken);

            case HttpEvent.EventOneofCase.FlashNews:
                return WriteFlashNewsAsync(message.FlashNews, cancellationToken);

            default:
                logger.LogDebug("The coordinator sent an unknown {EventCase}; ignored", message.EventCase);
                return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Runs the work one request asks for, when this host has the handler for
    /// it, and keeps its failure from reaching the stream.
    /// </summary>
    /// <param name="work">What the handler does, or null when this host has none.</param>
    /// <param name="missingWarning">Logged when this host has no handler.</param>
    /// <param name="failureMessage">Logged when the request cannot be carried out.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task GuardAsync(
        Func<Task>? work,
        string missingWarning,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (work is null)
        {
            logger.LogWarning("{Reason}", missingWarning);
            return;
        }

        try
        {
            await work();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A request that cannot be carried out must not end the stream of
            // the whole lobby: the next flash news still has to arrive.
            logger.LogError(exception, "{Reason}", failureMessage);
        }
    }

    /// <summary>Writes one relayed announcement to the clients of this lobby.</summary>
    /// <param name="broadcast">Message the coordinator sent.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task WriteFlashNewsAsync(FlashNewsBroadcast broadcast, CancellationToken cancellationToken)
    {
        try
        {
            var result = await flashNewsService.BroadcastAsync(
                new FlashNewsAnnouncement(
                    broadcast.Message,
                    (byte)broadcast.Unknown1,
                    (byte)broadcast.Unknown2,
                    (ushort)broadcast.Subcommand,
                    (byte)broadcast.Unknown5,
                    (byte)broadcast.MaintenanceTime),
                cancellationToken);

            logger.LogInformation(
                "Relayed flash news reached {Recipients} clients of this lobby",
                result.Recipients);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // One client that cannot be written to must not end the stream of
            // the whole lobby.
            logger.LogError(exception, "A relayed flash news could not be written to the clients");
        }
    }
}
