using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Discord;
using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// The slash command arrives as a gateway dispatch, so the client has to open
/// the socket, identify itself and relay the interaction it is sent.
/// </summary>
public sealed class DiscordGatewayClientTests
{
    private const string ModeratorRole = "100000000000000001";

    private const string Interaction =
        """
        {
          "op": 0,
          "t": "INTERACTION_CREATE",
          "s": 2,
          "d": {
            "id": "1",
            "token": "interaction-token",
            "type": 2,
            "guild_id": "10",
            "channel_id": "200",
            "member": { "roles": ["100000000000000001"] },
            "data": { "name": "flash", "options": [ { "name": "message", "value": "Maintenance soon" } ] }
          }
        }
        """;

    [Fact]
    public async Task TheGatewayIdentifiesAndTheFlashCommandIsRelayed()
    {
        await using var gateway = await FakeGatewayServer.StartAsync();
        var registry = new LobbyConnectionRegistryService(NullLogger<LobbyConnectionRegistryService>.Instance);
        var lobby = registry.Open(3, "Free Battle");
        var responder = new RecordingResponder();
        var messageService = new RecordingMessageService();

        var client = new DiscordGatewayClientService(
            CreateOptions(gateway.Port),
            new DiscordCommandService(
                responder,
                messageService,
                new FlashNewsDispatcherService(registry, NullLogger<FlashNewsDispatcherService>.Instance),
                CreateOptions(gateway.Port),
                NullLogger<DiscordCommandService>.Instance),
            NullLogger<DiscordGatewayClientService>.Instance);

        await client.StartAsync(CancellationToken.None);

        var socket = await gateway.AcceptConnectionAsync();

        // The gateway opens every connection with a hello, which is what the
        // client answers with an identify.
        await FakeGatewayServer.SendAsync(socket, """{"op":10,"d":{"heartbeat_interval":45000}}""");
        var identify = await FakeGatewayServer.ReceiveAsync(socket);
        Assert.Contains("\"op\":2", identify, StringComparison.Ordinal);
        Assert.Contains("\"token\":\"token\"", identify, StringComparison.Ordinal);

        await FakeGatewayServer.SendAsync(
            socket,
            FakeGatewayServer.ReadyPayload(gateway.Port));
        await FakeGatewayServer.SendAsync(socket, Interaction);

        var reply = await responder.NextAnswer.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("1", reply.InteractionIdentifier);
        Assert.Contains("1 lobbies", reply.Content, StringComparison.Ordinal);

        Assert.True(lobby.Outgoing.TryRead(out var packet));
        Assert.Equal("Maintenance soon", packet.FlashNews.Message);
        Assert.Empty(messageService.Messages);

        await client.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AnExtraHeartbeatTheGatewayAsksForIsAnsweredAtOnce()
    {
        await using var gateway = await FakeGatewayServer.StartAsync();
        var client = CreateClient(gateway.Port, new RecordingResponder());

        await client.StartAsync(CancellationToken.None);
        var socket = await gateway.AcceptConnectionAsync();

        await FakeGatewayServer.SendAsync(socket, """{"op":10,"d":{"heartbeat_interval":45000}}""");
        await FakeGatewayServer.ReceiveAsync(socket);

        await FakeGatewayServer.SendAsync(socket, """{"op":1}""");
        var heartbeat = await FakeGatewayServer.ReceiveAsync(socket);
        Assert.Contains("\"op\":1", heartbeat, StringComparison.Ordinal);

        await client.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AReconnectFrameIsFollowedByANewConnectionThatResumes()
    {
        await using var gateway = await FakeGatewayServer.StartAsync();
        var client = CreateClient(gateway.Port, new RecordingResponder());

        await client.StartAsync(CancellationToken.None);
        var socket = await gateway.AcceptConnectionAsync();

        await FakeGatewayServer.SendAsync(socket, """{"op":10,"d":{"heartbeat_interval":45000}}""");
        await FakeGatewayServer.ReceiveAsync(socket);
        await FakeGatewayServer.SendAsync(
            socket,
            FakeGatewayServer.ReadyPayload(gateway.Port));

        // The reconnect frame is answered with a fresh connection at once,
        // which carries the resume instead of a fresh identify.
        await FakeGatewayServer.SendAsync(socket, """{"op":7}""");
        var second = await gateway.AcceptConnectionAsync();

        await FakeGatewayServer.SendAsync(second, """{"op":10,"d":{"heartbeat_interval":45000}}""");
        var resume = await FakeGatewayServer.ReceiveAsync(second);
        Assert.Contains("\"op\":6", resume, StringComparison.Ordinal);
        Assert.Contains("\"session_id\":\"abc\"", resume, StringComparison.Ordinal);

        await client.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AnInvalidSessionThatCannotBeResumedIsFollowedByAnIdentify()
    {
        await using var gateway = await FakeGatewayServer.StartAsync();
        var client = CreateClient(gateway.Port, new RecordingResponder());

        await client.StartAsync(CancellationToken.None);
        var socket = await gateway.AcceptConnectionAsync();

        await FakeGatewayServer.SendAsync(socket, """{"op":10,"d":{"heartbeat_interval":45000}}""");
        await FakeGatewayServer.ReceiveAsync(socket);
        await FakeGatewayServer.SendAsync(
            socket,
            FakeGatewayServer.ReadyPayload(gateway.Port));

        // A d of false refuses the resume, so the next connection identifies
        // again instead of resuming the dead session.
        await FakeGatewayServer.SendAsync(socket, """{"op":9,"d":false}""");
        var second = await gateway.AcceptConnectionAsync();

        await FakeGatewayServer.SendAsync(second, """{"op":10,"d":{"heartbeat_interval":45000}}""");
        var identify = await FakeGatewayServer.ReceiveAsync(second);
        Assert.Contains("\"op\":2", identify, StringComparison.Ordinal);

        await client.StopAsync(CancellationToken.None);
    }

    private static DiscordGatewayClientService CreateClient(int port, RecordingResponder responder) =>
        new(
            CreateOptions(port),
            new DiscordCommandService(
                responder,
                new RecordingMessageService(),
                new FlashNewsDispatcherService(
                    new LobbyConnectionRegistryService(NullLogger<LobbyConnectionRegistryService>.Instance),
                    NullLogger<FlashNewsDispatcherService>.Instance),
                CreateOptions(port),
                NullLogger<DiscordCommandService>.Instance),
            NullLogger<DiscordGatewayClientService>.Instance);

    private static IOptions<DiscordOptions> CreateOptions(int port) => Options.Create(new DiscordOptions
    {
        Enabled = true,
        BotToken = "token",
        GatewayUrl = $"ws://127.0.0.1:{port}/?v=10&encoding=json",
        GuildIdentifier = "10",
        ModeratorRoleIdentifier = ModeratorRole,
    });



    private sealed class RecordingResponder : IDiscordInteractionResponder
    {
        private readonly TaskCompletionSource<(string InteractionIdentifier, string Content)> answer =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<(string InteractionIdentifier, string Content)> NextAnswer => answer.Task;

        public Task<bool> RespondAsync(
            string interactionIdentifier,
            string interactionToken,
            string content,
            CancellationToken cancellationToken)
        {
            answer.TrySetResult((interactionIdentifier, content));
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingMessageService : IDiscordMessageService
    {
        public List<(string ChannelIdentifier, string Content)> Messages { get; } = [];

        public Task<bool> SendChannelMessageAsync(
            string channelIdentifier,
            string content,
            CancellationToken cancellationToken)
        {
            Messages.Add((channelIdentifier, content));
            return Task.FromResult(true);
        }
    }

    /// <summary>A loopback WebSocket server that speaks as much gateway as the client needs.</summary>
    private sealed class FakeGatewayServer : IAsyncDisposable
    {
        private readonly HttpListener listener = new();
        private readonly BlockingCollection<WebSocket> accepted = new(1);
        private readonly CancellationTokenSource disposed = new();

        private FakeGatewayServer(int port)
        {
            Port = port;
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        }

        public int Port { get; }

        /// <summary>The ready event the fake gateway answers an identify with.</summary>
        public static string ReadyPayload(int port) =>
            "{\"op\":0,\"t\":\"READY\",\"s\":1,\"d\":{\"session_id\":\"abc\",\"resume_gateway_url\":\"ws://127.0.0.1:" +
            port + "/?v=10&encoding=json\"}}";

        public static async Task<FakeGatewayServer> StartAsync()
        {
            var server = new FakeGatewayServer(FreePort());
            server.listener.Start();
            _ = server.AcceptLoopAsync();
            return await Task.FromResult(server);
        }

        /// <summary>Waits for the next connection the client opens, in order.</summary>
        public async Task<WebSocket> AcceptConnectionAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    return accepted.Take(disposed.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new ObjectDisposedException(nameof(FakeGatewayServer));
                }
            });
        }

        public ValueTask DisposeAsync()
        {
            disposed.Cancel();
            try
            {
                accepted.CompleteAdding();
            }
            catch (ObjectDisposedException)
            {
            }

            listener.Stop();
            listener.Close();
            return ValueTask.CompletedTask;
        }

        public static async Task SendAsync(WebSocket socket, string payload) =>
            await socket.SendAsync(
                Encoding.UTF8.GetBytes(payload),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None);

        public static async Task<string> ReceiveAsync(WebSocket socket)
        {
            var buffer = new byte[8192];
            var payload = new MemoryStream();

            ValueWebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer.AsMemory(), CancellationToken.None);
                await payload.WriteAsync(buffer.AsMemory(0, result.Count));
            }
            while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(payload.ToArray());
        }

        private async Task AcceptLoopAsync()
        {
            while (!disposed.IsCancellationRequested)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    var accepted = await context.AcceptWebSocketAsync(subProtocol: null);
                    this.accepted.Add(accepted.WebSocket);
                }
                catch (Exception) when (disposed.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"The fake gateway dropped a connection: {exception.Message}");
                }
            }
        }

        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
