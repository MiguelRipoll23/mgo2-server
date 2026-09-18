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

        var client = new DiscordGatewayClientService(
            CreateOptions(gateway.Port),
            new DiscordCommandService(
                responder,
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

        await FakeGatewayServer.SendAsync(socket, """{"op":0,"t":"READY","s":1,"d":{"session_id":"abc"}}""");
        await FakeGatewayServer.SendAsync(socket, Interaction);

        var reply = await responder.NextAnswer.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("1", reply.InteractionIdentifier);
        Assert.Contains("1 lobbies", reply.Content, StringComparison.Ordinal);

        Assert.True(lobby.Outgoing.TryRead(out var packet));
        Assert.Equal("Maintenance soon", packet.FlashNews.Message);

        await client.StopAsync(CancellationToken.None);
    }

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

    /// <summary>A loopback WebSocket server that speaks as much gateway as the client needs.</summary>
    private sealed class FakeGatewayServer : IAsyncDisposable
    {
        private readonly HttpListener listener = new();
        private readonly TaskCompletionSource<WebSocket> connection =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private FakeGatewayServer(int port)
        {
            Port = port;
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        }

        public int Port { get; }

        public static Task<FakeGatewayServer> StartAsync()
        {
            var server = new FakeGatewayServer(FreePort());
            server.listener.Start();
            _ = server.AcceptAsync();
            return Task.FromResult(server);
        }

        public Task<WebSocket> AcceptConnectionAsync() => connection.Task;

        public ValueTask DisposeAsync()
        {
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

        private async Task AcceptAsync()
        {
            try
            {
                var context = await listener.GetContextAsync();
                var accepted = await context.AcceptWebSocketAsync(subProtocol: null);
                connection.TrySetResult(accepted.WebSocket);
            }
            catch (Exception exception)
            {
                connection.TrySetException(exception);
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