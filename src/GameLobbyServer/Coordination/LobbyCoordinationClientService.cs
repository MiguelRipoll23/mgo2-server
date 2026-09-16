using System.Collections.Concurrent;
using System.Threading.Channels;
using Grpc.Core;
using Grpc.Net.Client;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Domain.News;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Mgo2Server.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Keeps one persistent coordination stream open from this lobby to the HTTP
/// API, and uses it in both directions: the presence of this lobby travels up,
/// the flash news the API relays comes back down the same stream.
/// </summary>
/// <remarks>
/// The HTTP API is not a dependency of a gameplay lobby. A lobby whose
/// coordination endpoint cannot be reached keeps serving its players and tries
/// again on an interval; only the reporting and the reception of flash news are
/// missing until the connection is back.
/// </remarks>
/// <param name="flashNewsService">Service that writes an announcement to this lobby's clients.</param>
/// <param name="options">Options that hold the endpoint of the coordinator.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class LobbyCoordinationClientService(
    FlashNewsService flashNewsService,
    IOptions<CoordinationOptions> options,
    ILogger<LobbyCoordinationClientService> logger) : ILobbyPresencePublisher
{
    /// <summary>
    /// The characters this lobby has reported as connected. It is kept here
    /// rather than read from the tracker, because the tracker reports through
    /// this service: a snapshot has to come from the side that already owns the
    /// events, or the two would depend on each other.
    /// </summary>
    private readonly ConcurrentDictionary<int, byte> connectedCharacters = new();

    /// <summary>Events a lobby may queue for the coordinator before it is considered stuck.</summary>
    private const int OutgoingCapacity = 1024;

    private readonly CoordinationOptions options = options.Value;
    private readonly Channel<LobbyEvent> outgoing = Channel.CreateBounded<LobbyEvent>(
        new BoundedChannelOptions(OutgoingCapacity)
        {
            // The snapshot a new stream starts from is the truth, so an event
            // that arrives while the queue is full may be dropped: it is not
            // worth stalling a lobby that is still serving its players.
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    private CancellationTokenSource? cancellation;
    private Task? loop;
    private int lobbyIdentifier;
    private string lobbyName = string.Empty;

    /// <summary>Whether the stream to the coordinator is currently being kept.</summary>
    public bool IsRunning => loop is not null;

    /// <summary>Starts the connection loop for the lobby this process serves.</summary>
    /// <param name="identifier">Identifier of the registered lobby.</param>
    /// <param name="name">Name of the registered lobby.</param>
    public void StartFor(int identifier, string name)
    {
        lobbyIdentifier = identifier;
        lobbyName = name;

        if (loop is not null)
        {
            return;
        }

        cancellation = new CancellationTokenSource();
        loop = RunAsync(cancellation.Token);
    }

    /// <summary>Stops the connection loop.</summary>
    public async Task StopAsync()
    {
        if (cancellation is null || loop is null)
        {
            return;
        }

        await cancellation.CancelAsync();

        try
        {
            await loop;
        }
        catch (OperationCanceledException)
        {
            // The loop was cancelled, which is how it stops.
        }

        cancellation.Dispose();
        cancellation = null;
        loop = null;
    }

    /// <inheritdoc />
    public void PlayerConnected(int lobbyIdentifier, int characterIdentifier)
    {
        connectedCharacters[characterIdentifier] = 0;
        Publish(characterIdentifier, connected: true);
    }

    /// <inheritdoc />
    public void PlayerDisconnected(int lobbyIdentifier, int characterIdentifier)
    {
        connectedCharacters.TryRemove(characterIdentifier, out _);
        Publish(characterIdentifier, connected: false);
    }

    /// <summary>
    /// Queues one presence event for the coordinator. The lobby identifier is
    /// the one this process registered, never the one the caller passed, so a
    /// session of another lobby cannot be reported as a player of this one.
    /// </summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="connected">Whether the character connected rather than left.</param>
    private void Publish(int characterIdentifier, bool connected)
    {
        if (!IsRunning)
        {
            return;
        }

        var message = new LobbyEvent
        {
            PlayerPresence = new PlayerPresenceChange
            {
                LobbyIdentifier = lobbyIdentifier,
                CharacterIdentifier = characterIdentifier,
                Connected = connected,
            },
        };

        if (!outgoing.Writer.TryWrite(message))
        {
            logger.LogDebug(
                "The coordination queue of lobby {LobbyIdentifier} is full; a presence event was dropped",
                lobbyIdentifier);
        }
    }

    /// <summary>
    /// Keeps a stream open, and reopens one every interval for as long as the
    /// process runs. A coordinator that is down, unreachable or restarted is
    /// therefore a state this lobby passes through rather than a failure of it.
    /// </summary>
    /// <param name="cancellationToken">Token that stops the loop.</param>
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "The coordination endpoint at {Url} is unavailable; retrying in {Interval}",
                    options.ServerUrl,
                    options.ReconnectInterval);
            }

            try
            {
                await Task.Delay(options.ReconnectInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Keeps one stream open until it ends or one of its directions fails.</summary>
    /// <param name="cancellationToken">Token that stops the connection.</param>
    private async Task RunConnectionAsync(CancellationToken cancellationToken)
    {
        using var channel = GrpcChannel.ForAddress(options.ServerUrl);
        var client = new LobbyCoordination.LobbyCoordinationClient(channel);
        using var call = client.Coordinate(cancellationToken: cancellationToken);

        // Everything queued while there was no stream described a lobby the
        // coordinator never saw. The registration below carries the population
        // as it is now, which makes the reclaimed deltas both unnecessary and
        // wrong to replay.
        while (outgoing.Reader.TryRead(out _))
        {
        }

        await call.RequestStream.WriteAsync(BuildRegistration(), cancellationToken);
        logger.LogInformation(
            "Lobby {LobbyIdentifier} ({LobbyName}) is coordinating with {Url}",
            lobbyIdentifier,
            lobbyName,
            options.ServerUrl);

        using var connection = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sending = SendAsync(call.RequestStream, connection.Token);
        var receiving = ReceiveAsync(call.ResponseStream, connection.Token);

        // The first direction to end ends the stream: a half-open connection
        // cannot deliver flash news or report presence, so it is replaced.
        await Task.WhenAny(sending, receiving);
        await connection.CancelAsync();
        await CompleteRequestStreamAsync(call.RequestStream);
        await AwaitDirectionAsync(sending);
        await AwaitDirectionAsync(receiving);

        logger.LogInformation(
            "Lobby {LobbyIdentifier} ({LobbyName}) lost the coordination stream",
            lobbyIdentifier,
            lobbyName);
    }

    /// <summary>Builds the registration that opens a stream.</summary>
    private LobbyEvent BuildRegistration()
    {
        var registration = new LobbyRegistration
        {
            LobbyIdentifier = lobbyIdentifier,
            LobbyName = lobbyName,
        };

        foreach (var characterIdentifier in connectedCharacters.Keys)
        {
            registration.Players.Add(new ConnectedPlayer { CharacterIdentifier = characterIdentifier });
        }

        return new LobbyEvent { Registration = registration };
    }

    /// <summary>Writes the queued presence events to the stream.</summary>
    /// <param name="requestStream">Stream the events are written to.</param>
    /// <param name="cancellationToken">Token that stops the direction.</param>
    private async Task SendAsync(
        IClientStreamWriter<LobbyEvent> requestStream,
        CancellationToken cancellationToken)
    {
        await foreach (var message in outgoing.Reader.ReadAllAsync(cancellationToken))
        {
            await requestStream.WriteAsync(message, cancellationToken);
        }
    }

    /// <summary>Applies everything the coordinator sends down the stream.</summary>
    /// <param name="responseStream">Stream the messages are read from.</param>
    /// <param name="cancellationToken">Token that stops the direction.</param>
    private async Task ReceiveAsync(
        IAsyncStreamReader<HttpEvent> responseStream,
        CancellationToken cancellationToken)
    {
        await foreach (var message in responseStream.ReadAllAsync(cancellationToken))
        {
            await ApplyAsync(message, cancellationToken);
        }
    }

    /// <summary>Writes one relayed announcement to the clients of this lobby.</summary>
    /// <param name="message">Message the coordinator sent.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task ApplyAsync(HttpEvent message, CancellationToken cancellationToken)
    {
        if (message.EventCase != HttpEvent.EventOneofCase.FlashNews)
        {
            logger.LogDebug("The coordinator sent an unknown {EventCase}; ignored", message.EventCase);
            return;
        }

        var broadcast = message.FlashNews;

        try
        {
            // The payload is the client protocol, so the announcement is
            // rebuilt here and encoded the way the lobby always encoded it.
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
                "Relayed flash news reached {Recipients} clients of lobby {LobbyIdentifier}",
                result.Recipients,
                lobbyIdentifier);
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

    /// <summary>Closes the writing half of a stream that is being replaced.</summary>
    /// <param name="requestStream">Stream to complete.</param>
    private static async Task CompleteRequestStreamAsync(IClientStreamWriter<LobbyEvent> requestStream)
    {
        try
        {
            await requestStream.CompleteAsync();
        }
        catch (Exception)
        {
            // The stream is already gone, which is the usual reason it ends.
        }
    }

    /// <summary>Waits for one direction of a stream without letting its failure escape.</summary>
    /// <param name="direction">Direction that is being replaced.</param>
    private static async Task AwaitDirectionAsync(Task direction)
    {
        try
        {
            await direction;
        }
        catch (Exception)
        {
            // The failure of the stream is reported by the connection loop.
        }
    }
}
