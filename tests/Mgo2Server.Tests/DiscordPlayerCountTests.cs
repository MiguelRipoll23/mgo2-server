using System.Net;
using System.Text;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Discord;
using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// The global player count lives in one Discord channel: it is created once,
/// renamed whenever the count moves, and every arrival and departure is written
/// in it.
/// </summary>
public sealed class DiscordPlayerCountServiceTests
{
    [Fact]
    public void ChannelNameCarriesTheCount()
    {
        Assert.Equal("players [0]", DiscordPlayerCountService.FormatChannelName(0));
        Assert.Equal("players [42]", DiscordPlayerCountService.FormatChannelName(42));
    }

    [Fact]
    public async Task InitializationAdoptsTheChannelOfAPreviousRun()
    {
        var rest = new RecordingRestClient(
            new Dictionary<string, (HttpStatusCode, string)>
            {
                ["GET /api/v10/guilds/10/channels"] = (HttpStatusCode.OK, """[{"id":"77","name":"players [0]"}]"""),
            });
        var service = new DiscordPlayerCountService(rest.Client, new LobbyPresenceService(), rest.Options, NullLogger<DiscordPlayerCountService>.Instance);

        await service.InitializeAsync(3, CancellationToken.None);

        Assert.DoesNotContain(rest.Requests, request => request.Method == "POST" && request.Path.Contains("/guilds/"));
        Assert.Contains(rest.Requests, request =>
            request.Method == "PATCH" &&
            request.Path == "/api/v10/channels/77" &&
            request.Body.Contains("players [3]"));
    }

    [Fact]
    public async Task InitializationCreatesTheChannelWhenTheGuildHasNone()
    {
        var rest = new RecordingRestClient(
            new Dictionary<string, (HttpStatusCode, string)>
            {
                ["GET /api/v10/guilds/10/channels"] = (HttpStatusCode.OK, "[]"),
            },
            jsonResponse: """{"id":"88","name":"players [0]"}""");
        var service = new DiscordPlayerCountService(rest.Client, new LobbyPresenceService(), rest.Options, NullLogger<DiscordPlayerCountService>.Instance);

        await service.InitializeAsync(0, CancellationToken.None);

        var created = Assert.Single(rest.Requests, request => request.Method == "POST" && request.Path == "/api/v10/guilds/10/channels");
        Assert.Contains("players [0]", created.Body);
        Assert.Contains("\"type\":0", created.Body);
        Assert.Contains(rest.Requests, request => request.Path == "/api/v10/channels/88");
    }

    [Fact]
    public async Task EveryConnectionAndDisconnectionIsWrittenInTheChannel()
    {
        var rest = new RecordingRestClient(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["GET /api/v10/guilds/10/channels"] = (HttpStatusCode.OK, """[{"id":"77","name":"players [0]"}]"""),
            ["PATCH /api/v10/channels/77"] = (HttpStatusCode.OK, """{"id":"77"}"""),
            ["POST /api/v10/channels/77/messages"] = (HttpStatusCode.OK, """{"id":"91"}"""),
        });
        var presence = new LobbyPresenceService();
        var service = new DiscordPlayerCountService(
            rest.Client,
            presence,
            Configured(options => options.PresenceCoalesceMilliseconds = 20),
            NullLogger<DiscordPlayerCountService>.Instance);

        await service.InitializeAsync(0, CancellationToken.None);

        presence.AddPlayer(1, 42, out _);
        await service.PlayerPresenceChangedAsync(
            new PlayerPresenceNotification(1, "Free Battle", 42, "Snake", Connected: true, TotalPlayers: 1),
            CancellationToken.None);
        await WaitForAsync(() => Messages(rest).Any(body => body.Contains("Snake connected", StringComparison.Ordinal)));

        presence.RemovePlayer(1, 42, out _);
        await service.PlayerPresenceChangedAsync(
            new PlayerPresenceNotification(1, "Free Battle", 42, "Snake", Connected: false, TotalPlayers: 0),
            CancellationToken.None);
        await WaitForAsync(() => Messages(rest).Any(body => body.Contains("Snake disconnected", StringComparison.Ordinal)));

        var messages = Messages(rest);
        var connected = Assert.Single(messages, body => body.Contains("Snake connected", StringComparison.Ordinal));
        Assert.Contains("\"parse\":[]", connected, StringComparison.Ordinal);
        Assert.DoesNotContain("connected.", connected, StringComparison.Ordinal);

        var disconnected = Assert.Single(messages, body => body.Contains("Snake disconnected", StringComparison.Ordinal));
        Assert.Contains("\"parse\":[]", disconnected, StringComparison.Ordinal);
        Assert.DoesNotContain("disconnected.", disconnected, StringComparison.Ordinal);

        var renames = rest.Requests
            .Where(request => request.Method == "PATCH")
            .Select(request => request.Body)
            .ToList();
        Assert.Contains(renames, body => body.Contains("players [1]"));
        Assert.Contains(renames, body => body.Contains("players [0]"));
    }

    [Fact]
    public async Task ASwitchBetweenLobbiesIsNotWrittenInTheChannel()
    {
        var rest = new RecordingRestClient(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["GET /api/v10/guilds/10/channels"] = (HttpStatusCode.OK, """[{"id":"77","name":"players [0]"}]"""),
            ["PATCH /api/v10/channels/77"] = (HttpStatusCode.OK, """{"id":"77"}"""),
            ["POST /api/v10/channels/77/messages"] = (HttpStatusCode.OK, """{"id":"91"}"""),
        });
        var presence = new LobbyPresenceService();
        var service = new DiscordPlayerCountService(
            rest.Client,
            presence,
            Configured(options => options.PresenceCoalesceMilliseconds = 60),
            NullLogger<DiscordPlayerCountService>.Instance);

        await service.InitializeAsync(1, CancellationToken.None);

        // Leaving one lobby for another is a departure and an arrival of the
        // same character inside one window, so they cancel out.
        presence.RemovePlayer(1, 42, out _);
        await service.PlayerPresenceChangedAsync(
            new PlayerPresenceNotification(1, "Free Battle", 42, "Snake", Connected: false, TotalPlayers: 0),
            CancellationToken.None);
        presence.AddPlayer(2, 42, out _);
        await service.PlayerPresenceChangedAsync(
            new PlayerPresenceNotification(2, "Team Battle", 42, "Snake", Connected: true, TotalPlayers: 1),
            CancellationToken.None);

        await Task.Delay(200);

        Assert.Empty(Messages(rest));
        Assert.Single(rest.Requests, request => request.Method == "PATCH");
    }

    [Fact]
    public async Task AChannelThatCannotBeRenamedIsRetriedAtTheNextChange()
    {
        var rest = new RecordingRestClient(
            new Dictionary<string, (HttpStatusCode, string)>
            {
                ["GET /api/v10/guilds/10/channels"] = (HttpStatusCode.OK, """[{"id":"123","name":"players [0]"}]"""),
                ["PATCH /api/v10/channels/123"] = (HttpStatusCode.TooManyRequests, """{"retry_after":60}"""),
            });
        var service = new DiscordPlayerCountService(rest.Client, new LobbyPresenceService(), Configured(options => { }), NullLogger<DiscordPlayerCountService>.Instance);

        await service.InitializeAsync(1, CancellationToken.None);
        await service.PlayerTotalChangedAsync(2, CancellationToken.None);

        Assert.Equal(2, rest.Requests.Count(request => request.Method == "PATCH" && request.Path == "/api/v10/channels/123"));
    }

    [Fact]
    public async Task AConfiguredChannelIsUsedAsItIs()
    {
        var rest = new RecordingRestClient(new Dictionary<string, (HttpStatusCode, string)>());
        var service = new DiscordPlayerCountService(
            rest.Client,
            new LobbyPresenceService(),
            Configured(options => options.PlayerCountChannelIdentifier = "55"),
            NullLogger<DiscordPlayerCountService>.Instance);

        await service.InitializeAsync(7, CancellationToken.None);

        Assert.DoesNotContain(rest.Requests, request => request.Path.Contains("/guilds/"));
        Assert.Contains(rest.Requests, request =>
            request.Method == "PATCH" &&
            request.Path == "/api/v10/channels/55" &&
            request.Body.Contains("players [7]"));
    }

    [Fact]
    public async Task WithoutATokenNothingIsPublished()
    {
        var rest = new RecordingRestClient(new Dictionary<string, (HttpStatusCode, string)>());
        var service = new DiscordPlayerCountService(
            rest.Client,
            new LobbyPresenceService(),
            Options.Create(new DiscordOptions { Enabled = true, GuildIdentifier = "10" }),
            NullLogger<DiscordPlayerCountService>.Instance);

        await service.InitializeAsync(4, CancellationToken.None);

        Assert.Empty(rest.Requests);
    }

    private static List<string> Messages(RecordingRestClient rest) =>
        [.. rest.Requests
            .Where(request => request.Path.EndsWith("/messages", StringComparison.Ordinal))
            .Select(request => request.Body)];

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "The expected Discord call was not made in time.");
    }

    private static IOptions<DiscordOptions> Configured(Action<DiscordOptions> configure)
    {
        var options = new DiscordOptions
        {
            Enabled = true,
            BotToken = "bot-token",
            GuildIdentifier = "10",
        };

        configure(options);
        return Options.Create(options);
    }

    /// <summary>The REST client of the integration, on a transport that records the calls.</summary>
    private sealed class RecordingRestClient
    {
        public RecordingRestClient(
            Dictionary<string, (HttpStatusCode Status, string Body)> responses,
            string jsonResponse = "{}")
        {
            Handler = new RecordingHandler(responses, jsonResponse);
            var factory = new RecordingHttpClientFactory(Handler);
            Client = new DiscordRestClientService(
                factory,
                Configured(options => options.PlayerCountChannelIdentifier = string.Empty),
                NullLogger<DiscordRestClientService>.Instance);
        }

        public RecordingHandler Handler { get; }

        public DiscordRestClientService Client { get; }

        public IOptions<DiscordOptions> Options => Configured(options => { });

        public List<RecordedRequest> Requests => Handler.Requests;
    }

    private sealed record RecordedRequest(string Method, string Path, string Body);

    private sealed class RecordingHttpClientFactory(RecordingHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(
        Dictionary<string, (HttpStatusCode Status, string Body)> responses,
        string jsonResponse) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var path = request.RequestUri!.PathAndQuery;

            Requests.Add(new RecordedRequest(request.Method.Method, path, body));

            var (status, content) = responses.TryGetValue($"{request.Method.Method} {path}", out var configured)
                ? configured
                : (HttpStatusCode.OK, jsonResponse);

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };
        }
    }
}
