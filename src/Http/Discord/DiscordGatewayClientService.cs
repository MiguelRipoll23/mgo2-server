using System.Net.WebSockets;
using System.Text.Json;
using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// The WebSocket connection to the Discord gateway: it identifies the bot,
/// keeps the heartbeat alive and forwards the events Discord dispatches to the
/// command service. It is the reception side of the integration, the one the
/// slash commands arrive on.
/// </summary>
/// <remarks>
/// It runs in the background and reports every failure itself. Discord is
/// optional, so a deployment whose token is wrong, whose bot has been removed
/// from the guild or whose Discord is unreachable must start and serve exactly
/// as one that never enabled the integration.
/// </remarks>
/// <param name="options">Options of the integration.</param>
/// <param name="commandService">Service the dispatched commands are forwarded to.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordGatewayClientService(
    IOptions<DiscordOptions> options,
    DiscordCommandService commandService,
    ILogger<DiscordGatewayClientService> logger) : IHostedService
{
    /// <summary>Frame that carries an event Discord dispatches.</summary>
    private const int DispatchOpCode = 0;

    /// <summary>Heartbeat the client sends within the interval of the hello.</summary>
    private const int HeartbeatOpCode = 1;

    /// <summary>Identify the client sends to open a session.</summary>
    private const int IdentifyOpCode = 2;

    /// <summary>Resume the client sends over a session that was dropped.</summary>
    private const int ResumeOpCode = 6;

    /// <summary>Frame that asks the client to reconnect.</summary>
    private const int ReconnectOpCode = 7;

    /// <summary>Frame that reports the session cannot be resumed.</summary>
    private const int InvalidSessionOpCode = 9;

    /// <summary>Hello the gateway opens the connection with.</summary>
    private const int HelloOpCode = 10;

    /// <summary>Frame that acknowledges the last heartbeat.</summary>
    private const int HeartbeatAckOpCode = 11;

    /// <summary>Event Discord dispatches when a session is opened.</summary>
    private const string ReadyEventName = "READY";

    /// <summary>Event Discord dispatches when a command is used.</summary>
    private const string InteractionCreateEventName = "INTERACTION_CREATE";

    /// <summary>How long the client waits after a dropped connection before it retries.</summary>
    private const int InitialReconnectDelayMilliseconds = 1000;

    /// <summary>Longest the client ever waits between attempts.</summary>
    private const int MaximumReconnectDelayMilliseconds = 30000;

    /// <summary>
    /// Close codes after which reconnecting cannot succeed: an invalid token,
    /// an invalid shard or API version, and intents the app may not send. The
    /// deployment has to be corrected instead of the connection retried.
    /// </summary>
    private static readonly IReadOnlySet<int> FatalCloseCodes = new HashSet<int>
    {
        4004, // Authentication failed.
        4010, // Invalid shard.
        4011, // Sharding required.
        4012, // Invalid API version.
        4013, // Invalid intent(s).
        4014, // Disallowed intent(s).
    };

    private readonly DiscordOptions options = options.Value;
    private readonly object sequenceGate = new();
    private readonly CancellationTokenSource lifetimeCts = new();

    private int? lastSequence;

    /// <summary>Whether the last heartbeat was acknowledged, as the heartbeat loop reads it.</summary>
    private int heartbeatAcknowledged = 1;

    /// <summary>Identifier of the gateway session, kept so a dropped connection can be resumed.</summary>
    private string? sessionIdentifier;

    /// <summary>URL the Ready event of the session named for resuming it.</summary>
    private string? resumeUrl;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
        {
            logger.LogInformation("The Discord integration is disabled");
            return Task.CompletedTask;
        }

        logger.LogInformation("The Discord gateway is starting");

        // Not awaited: the API has to start whether or not Discord answers.
        _ = RunAsync(lifetimeCts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        lifetimeCts.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>Keeps the gateway connection up, retrying after every drop.</summary>
    /// <param name="cancellationToken">Token that cancels the loop.</param>
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var reconnectDelay = InitialReconnectDelayMilliseconds;

        while (!cancellationToken.IsCancellationRequested)
        {
            var immediate = false;

            try
            {
                await RunConnectionAsync(cancellationToken);
                reconnectDelay = InitialReconnectDelayMilliseconds;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (GatewayReconnectException)
            {
                // The connection is to be opened again at once, without the
                // backoff of an unexpected drop.
                immediate = true;
                reconnectDelay = InitialReconnectDelayMilliseconds;
            }
            catch (GatewayFatalCloseException exception)
            {
                logger.LogError(
                    "The Discord gateway closed the connection with {CloseCode}; the integration stops " +
                    "until the deployment is corrected",
                    exception.CloseCode);
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "The Discord gateway connection dropped; retrying in {Delay} ms",
                    reconnectDelay);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!immediate)
            {
                try
                {
                    await Task.Delay(reconnectDelay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                reconnectDelay = Math.Min(reconnectDelay * 2, MaximumReconnectDelayMilliseconds);
            }
        }
    }

    /// <summary>Serves one connection to the gateway until it drops.</summary>
    /// <param name="cancellationToken">Token that cancels the connection.</param>
    /// <exception cref="GatewayReconnectException">The connection is to be opened again at once.</exception>
    private async Task RunConnectionAsync(CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("User-Agent", options.UserAgent);
        await socket.ConnectAsync(new Uri(CurrentGatewayUrl), cancellationToken);

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var frame = await DiscordGatewayStreamUtils.ReceiveAsync(socket, cancellationToken);
                if (frame is null)
                {
                    break;
                }

                switch (frame.OpCode)
                {
                    case DispatchOpCode:
                        RecordSequence(frame.Sequence);
                        await HandleDispatchAsync(frame, cancellationToken);
                        break;

                    case HeartbeatOpCode:
                        // Discord asks for an extra heartbeat outside the
                        // interval; it is answered at once.
                        await DiscordGatewayStreamUtils.SendAsync(
                            socket,
                            new DiscordGatewayHeartbeat(Sequence: LastSequence),
                            cancellationToken);
                        break;

                    case ReconnectOpCode:
                        throw new GatewayReconnectException();

                    case InvalidSessionOpCode:
                        // The d field tells whether the session may be resumed:
                        // a false one has to be opened again with an identify.
                        if (frame.Data is not { ValueKind: JsonValueKind.True })
                        {
                            sessionIdentifier = null;
                        }

                        throw new GatewayReconnectException();

                    case HelloOpCode:
                        StartHeartbeatLoop(socket, frame, heartbeatCts.Token);
                        await IdentifyOrResumeAsync(socket, cancellationToken);
                        break;

                    case HeartbeatAckOpCode:
                        Interlocked.Exchange(ref heartbeatAcknowledged, 1);
                        break;
                }
            }
        }
        finally
        {
            heartbeatCts.Cancel();
        }

        // The loop above ends when the gateway closes the connection, and the
        // close code tells whether opening it again can succeed at all: an
        // invalid token or a disallowed intent is corrected in the deployment,
        // not waited out.
        if (socket.CloseStatus is { } closeStatus && FatalCloseCodes.Contains((int)closeStatus))
        {
            throw new GatewayFatalCloseException((int)closeStatus);
        }
    }

    /// <summary>Handles one event Discord dispatched.</summary>
    /// <param name="frame">Frame that carried the event.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task HandleDispatchAsync(GatewayFrame frame, CancellationToken cancellationToken)
    {
        switch (frame.EventName)
        {
            case ReadyEventName:
            {
                var ready = DiscordGatewayStreamUtils.ReadData<DiscordReadyData>(frame);
                if (ready is not null)
                {
                    sessionIdentifier = ready.SessionIdentifier;
                    resumeUrl = ready.ResumeGatewayUrl;
                    logger.LogInformation("The Discord gateway opened a session");
                }

                break;
            }

            case InteractionCreateEventName:
            {
                DiscordInteraction? interaction;
                try
                {
                    interaction = DiscordGatewayStreamUtils.ReadData<DiscordInteraction>(frame);
                }
                catch (JsonException exception)
                {
                    logger.LogWarning(exception, "An interaction from Discord could not be read");
                    return;
                }

                if (interaction is not null)
                {
                    await commandService.HandleInteractionAsync(interaction, cancellationToken);
                }

                break;
            }
        }
    }

    /// <summary>Starts the heartbeat of one connection, at the interval the hello set.</summary>
    /// <param name="socket">Socket the heartbeats are sent over.</param>
    /// <param name="hello">Hello that carried the interval.</param>
    /// <param name="cancellationToken">Token that cancels the heartbeats.</param>
    private void StartHeartbeatLoop(ClientWebSocket socket, GatewayFrame hello, CancellationToken cancellationToken)
    {
        var intervalMilliseconds = DiscordGatewayStreamUtils.ReadData<DiscordGatewayHello>(hello)?.HeartbeatInterval ?? 0;
        if (intervalMilliseconds <= 0)
        {
            return;
        }

        Interlocked.Exchange(ref heartbeatAcknowledged, 1);
        _ = RunHeartbeatLoopAsync(socket, intervalMilliseconds, cancellationToken);
    }

    /// <summary>Sends one heartbeat per interval, dropping the connection when none is acknowledged.</summary>
    /// <param name="socket">Socket the heartbeats are sent over.</param>
    /// <param name="intervalMilliseconds">Interval between heartbeats.</param>
    /// <param name="cancellationToken">Token that cancels the loop.</param>
    private async Task RunHeartbeatLoopAsync(
        ClientWebSocket socket,
        int intervalMilliseconds,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMilliseconds));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                // A heartbeat that is not acknowledged means the connection is
                // dead; dropping it lets the retry loop open a new one.
                if (Interlocked.Exchange(ref heartbeatAcknowledged, 0) == 0)
                {
                    logger.LogWarning("Discord did not acknowledge the previous heartbeat; reconnecting");
                    socket.Abort();
                    return;
                }

                await SendHeartbeatAsync(socket, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The connection ended.
        }
        catch (WebSocketException exception)
        {
            logger.LogWarning(exception, "The Discord heartbeat could not be sent");
        }
    }

    /// <summary>Sends a heartbeat carrying the last sequence number.</summary>
    /// <param name="socket">Socket the heartbeat is sent over.</param>
    /// <param name="cancellationToken">Token that cancels the send.</param>
    private Task SendHeartbeatAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        return DiscordGatewayStreamUtils.SendAsync(socket, new DiscordGatewayHeartbeat(Sequence: LastSequence), cancellationToken);
    }

    /// <summary>Opens the session: a fresh identify, or a resume when one is already held.</summary>
    /// <param name="socket">Socket the request is sent over.</param>
    /// <param name="cancellationToken">Token that cancels the send.</param>
    private Task IdentifyOrResumeAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        if (sessionIdentifier is null)
        {
            return DiscordGatewayStreamUtils.SendAsync(
                socket,
                new DiscordGatewayRequest(
                    IdentifyOpCode,
                    new DiscordIdentifyData(
                        options.BotToken,
                        DiscordOptions.GatewayIntents,
                        new DiscordConnectionProperties("linux", "mgo2-server", "mgo2-server"))),
                cancellationToken);
        }

        return DiscordGatewayStreamUtils.SendAsync(
            socket,
            new DiscordGatewayRequest(
                ResumeOpCode,
                new DiscordResumeData(options.BotToken, sessionIdentifier, LastSequence ?? 0)),
            cancellationToken);
    }

    /// <summary>Remembers the sequence of the last frame the gateway dispatched.</summary>
    /// <param name="sequence">Sequence to remember.</param>
    private void RecordSequence(int? sequence)
    {
        if (sequence is null)
        {
            return;
        }

        lock (sequenceGate)
        {
            lastSequence = sequence;
        }
    }

    /// <summary>The gateway URL this connection and its resumes are opened with.</summary>
    private string CurrentGatewayUrl =>
        string.IsNullOrWhiteSpace(resumeUrl) ? options.GatewayUrl : resumeUrl!;

    private int? LastSequence
    {
        get
        {
            lock (sequenceGate)
            {
                return lastSequence;
            }
        }
    }

    /// <summary>Signals a connection that has to be dropped and opened again.</summary>
    private sealed class GatewayReconnectException : Exception;

    /// <summary>Signals a close the integration cannot recover from on its own.</summary>
    /// <param name="CloseCode">Close code the gateway ended the connection with.</param>
    private sealed class GatewayFatalCloseException(int closeCode) : Exception
    {
        /// <summary>Close code the gateway ended the connection with.</summary>
        public int CloseCode { get; } = closeCode;
    }
}
