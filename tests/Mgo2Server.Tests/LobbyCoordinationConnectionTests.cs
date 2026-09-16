using System.Net;
using System.Net.Sockets;
using Mgo2Server.GameLobbyServer.Coordination;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.News;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// The coordination stream as it really runs: a lobby opens it against the
/// endpoint of the API, reports its presence up and receives the announcements
/// the API relays back down the same connection.
/// </summary>
public sealed class LobbyCoordinationConnectionTests : IAsyncLifetime
{
    private const int LobbyIdentifier = 7;

    private readonly LobbyPresenceService presence = new();
    private readonly LobbyConnectionRegistryService registry =
        new(NullLogger<LobbyConnectionRegistryService>.Instance);
    private readonly ActiveGameSessionsService sessions = new();

    private WebApplication application = null!;
    private LobbyCoordinationClientService client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        // An ephemeral port, so the test never collides with a running server.
        builder.WebHost.ConfigureKestrel(options => options.Listen(
            IPAddress.Loopback,
            0,
            listen => listen.Protocols = HttpProtocols.Http2));

        builder.Services.AddGrpc();
        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton(presence);
        builder.Services.AddSingleton(sessions);
        builder.Services.AddSingleton<PlayerPresenceNotificationService>();

        // The names of the characters are read from a database this test does
        // not have, which is precisely the state the coordination must survive.
        builder.Services.AddDbContextFactory<Mgo2DatabaseContext>(options =>
            options.UseNpgsql("Host=127.0.0.1;Port=1;Database=mgo2;Username=postgres;Timeout=1"));
        builder.Services.AddSingleton<CharacterService>();

        application = builder.Build();
        application.MapGrpcService<LobbyCoordinationGrpcService>();
        await application.StartAsync();

        var address = application.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();

        client = new LobbyCoordinationClientService(
            new FlashNewsService(sessions, new SessionHelper(new PacketCodecService(NullLogger<PacketCodecService>.Instance))),
            Options.Create(new CoordinationOptions
            {
                ServerUrl = address,
                ReconnectInterval = TimeSpan.FromMilliseconds(250),
            }),
            NullLogger<LobbyCoordinationClientService>.Instance);

        client.StartFor(LobbyIdentifier, "Free Battle");
    }

    public async Task DisposeAsync()
    {
        await client.StopAsync();
        await application.StopAsync();
        await application.DisposeAsync();
    }

    [Fact]
    public async Task TheLobbyRegistersItselfAndReportsItsPlayers()
    {
        Assert.True(await WaitUntilAsync(() => registry.Count == 1), "the stream was never opened");
        Assert.True(await WaitUntilAsync(() => presence.GetLobbyName(LobbyIdentifier) == "Free Battle"));

        client.PlayerConnected(LobbyIdentifier, 42);
        Assert.True(await WaitUntilAsync(() => presence.GetPlayerCount(LobbyIdentifier) == 1));
        Assert.Equal(1, presence.TotalPlayers);

        client.PlayerDisconnected(LobbyIdentifier, 42);
        Assert.True(await WaitUntilAsync(() => presence.TotalPlayers == 0));
    }

    [Fact]
    public async Task AStreamThatEndsReleasesThePlayersOfItsLobby()
    {
        Assert.True(await WaitUntilAsync(() => registry.Count == 1));

        client.PlayerConnected(LobbyIdentifier, 42);
        Assert.True(await WaitUntilAsync(() => presence.TotalPlayers == 1));

        await client.StopAsync();

        Assert.True(await WaitUntilAsync(() => registry.Count == 0), "the stream was never closed");
        Assert.True(await WaitUntilAsync(() => presence.TotalPlayers == 0));
    }

    [Fact]
    public async Task AFlashNewsReachesTheClientsOfTheLobby()
    {
        Assert.True(await WaitUntilAsync(() => registry.Count == 1));

        using var clientSocket = new TcpClient();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var accepted = listener.AcceptTcpClientAsync();

        await clientSocket.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var serverSocket = await accepted;
        using var stream = serverSocket.GetStream();

        sessions.Add(new TcpSession
        {
            ServerType = ServerType.GameplayLobby,
            LogPrefix = "test",
            RemoteAddress = "127.0.0.1:1",
            Connection = clientSocket.GetStream(),
            CharacterIdentifier = 42,
        });

        registry.Broadcast(new HttpEvent
        {
            FlashNews = new FlashNewsBroadcast
            {
                Message = "Maintenance in ten minutes",
                Subcommand = FlashNewsSubcommand.ServerMessage,
            },
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[512];
        var read = await stream.ReadAsync(buffer, timeout.Token);

        Assert.True(read > 0, "the ticker packet never reached the client");
    }

    /// <summary>Waits for a condition a stream fulfils asynchronously.</summary>
    /// <param name="condition">Condition to wait for.</param>
    /// <param name="timeout">Time to wait, fifteen seconds when none is given.</param>
    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(25);
        }

        return condition();
    }
}
