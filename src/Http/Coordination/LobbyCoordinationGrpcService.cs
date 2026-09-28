using Grpc.Core;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Http.Coordination;

/// <summary>
/// The coordination endpoint of the HTTP API. Every gameplay lobby opens one
/// stream on it; the stream carries the lobby's presence events up and the
/// announcements the API relays down, which is what makes one connection
/// bidirectional and enough.
/// </summary>
/// <remarks>
/// A stream that fails ends the coordination of that lobby alone. Nothing here
/// throws at the caller of the API: a broken stream is a normal event of a
/// deployment in which every server can be restarted on its own.
/// </remarks>
/// <param name="registry">Registry the open streams are held in.</param>
/// <param name="notifications">Service the presence events are applied to.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class LobbyCoordinationGrpcService(
    LobbyConnectionRegistryService registry,
    PlayerPresenceNotificationService notifications,
    ILogger<LobbyCoordinationGrpcService> logger)
    : LobbyCoordination.LobbyCoordinationBase
{
    /// <inheritdoc />
    public override async Task Coordinate(
        IAsyncStreamReader<LobbyEvent> requestStream,
        IServerStreamWriter<HttpEvent> responseStream,
        ServerCallContext context)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        LobbyConnection? connection = null;
        var lobbyIdentifier = 0;

        try
        {
            if (!await requestStream.MoveNext(cancellation.Token))
            {
                return;
            }

            if (requestStream.Current.EventCase != LobbyEvent.EventOneofCase.Registration)
            {
                // The protocol opens with the registration, so a stream that
                // does not is one this server cannot place or count.
                logger.LogWarning(
                    "A lobby stream opened with {EventCase} instead of a registration; closed",
                    requestStream.Current.EventCase);
                return;
            }

            var registration = requestStream.Current.Registration;
            lobbyIdentifier = registration.LobbyIdentifier;
            connection = registry.Open(lobbyIdentifier, registration.LobbyName);

            await notifications.RegisterLobbyAsync(
                lobbyIdentifier,
                registration.LobbyName,
                registration.Players.Select(player => player.CharacterIdentifier),
                cancellation.Token);

            var outgoing = WriteOutgoingAsync(responseStream, connection, cancellation.Token);

            while (await requestStream.MoveNext(cancellation.Token))
            {
                await ApplyAsync(requestStream.Current, cancellation.Token);
            }

            await cancellation.CancelAsync();
            await AwaitOutgoingAsync(outgoing);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The lobby closed the stream or the API is shutting down.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The coordination stream of lobby {LobbyIdentifier} failed", lobbyIdentifier);
        }
        finally
        {
            if (connection is not null && registry.Close(connection))
            {
                logger.LogInformation(
                    "Lobby {LobbyIdentifier} ({LobbyName}) is disconnected",
                    connection.LobbyIdentifier,
                    connection.LobbyName);

                await RemoveLobbyAsync(lobbyIdentifier);
            }

            cancellation.Dispose();
        }
    }

    /// <summary>Applies one event the lobby reported.</summary>
    /// <param name="message">Event the lobby sent.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task ApplyAsync(LobbyEvent message, CancellationToken cancellationToken)
    {
        switch (message.EventCase)
        {
            case LobbyEvent.EventOneofCase.PlayerPresence:
                var presence = message.PlayerPresence;
                if (presence.Connected)
                {
                    await notifications.PlayerConnectedAsync(
                        presence.LobbyIdentifier,
                        presence.CharacterIdentifier,
                        cancellationToken);
                }
                else
                {
                    await notifications.PlayerDisconnectedAsync(
                        presence.LobbyIdentifier,
                        presence.CharacterIdentifier,
                        cancellationToken);
                }

                break;

            default:
                logger.LogWarning("A lobby stream carried an unknown {EventCase}; ignored", message.EventCase);
                break;
        }
    }

    /// <summary>
    /// Writes everything the coordinator queues for this lobby to its stream.
    /// It is the only writer of the stream for the life of the call.
    /// </summary>
    /// <param name="responseStream">Stream the announcements are written to.</param>
    /// <param name="connection">Connection whose queue is drained.</param>
    /// <param name="cancellationToken">Token that stops the pump.</param>
    private static async Task WriteOutgoingAsync(
        IServerStreamWriter<HttpEvent> responseStream,
        LobbyConnection connection,
        CancellationToken cancellationToken)
    {
        await foreach (var message in connection.Outgoing.ReadAllAsync(cancellationToken))
        {
            await responseStream.WriteAsync(message, cancellationToken);
        }
    }

    /// <summary>Waits for the pump of a stream without letting its failure escape.</summary>
    /// <param name="outgoing">Pump that writes the stream.</param>
    private static async Task AwaitOutgoingAsync(Task outgoing)
    {
        try
        {
            await outgoing;
        }
        catch (OperationCanceledException)
        {
            // Cancelling the pump is how it stops.
        }
        catch (Exception)
        {
            // A stream that fails while the API stops is already reported by
            // the read loop that owns this call.
        }
    }

    /// <summary>
    /// Forgets the lobby and its players. The stream is gone, so nothing can
    /// report a departure for them any more.
    /// </summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    private async Task RemoveLobbyAsync(int lobbyIdentifier)
    {
        try
        {
            await notifications.RemoveLobbyAsync(lobbyIdentifier, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The disconnection of lobby {LobbyIdentifier} failed", lobbyIdentifier);
        }
    }
}
