using System.Net;
using System.Text;
using System.Text.Json;
using Mgo2Server.Http.Discord;
using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// The REST calls of the integration: the command registration and the answer
/// to an interaction, both of which the gateway socket cannot carry because it
/// only sends what Discord pushes down.
/// </summary>
[Trait("Category", "Http")]
public sealed class DiscordRestClientTests
{
    private const string Application = "123456789012345678";

    /// <summary>Token whose first segment decodes to <see cref="Application"/>.</summary>
    private const string Token = "MTIzNDU2Nzg5MDEyMzQ1Njc4.abcdef.ghijkl";

    [Fact]
    public async Task TheInteractionIsAnsweredEphemerally()
    {
        var rest = new RecordingRestClient();

        var answered = await rest.Client.RespondAsync(
            "7",
            "interaction-token",
            "Flash news sent to 2 lobbies.",
            CancellationToken.None);

        Assert.True(answered);
        var request = Assert.Single(rest.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/v10/interactions/7/interaction-token/callback", request.Path);

        using var body = JsonDocument.Parse(request.Body);
        var data = body.RootElement.GetProperty("data");
        Assert.Equal(4, body.RootElement.GetProperty("type").GetInt32());
        Assert.Equal("Flash news sent to 2 lobbies.", data.GetProperty("content").GetString());
        Assert.Equal(64, data.GetProperty("flags").GetInt32());
    }

    [Fact]
    public async Task AChannelMessageStripsEveryMention()
    {
        var rest = new RecordingRestClient();

        var sent = await rest.Client.SendChannelMessageAsync(
            "77",
            "A message from the bot.",
            CancellationToken.None);

        Assert.True(sent);
        var request = Assert.Single(rest.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/v10/channels/77/messages", request.Path);

        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("A message from the bot.", body.RootElement.GetProperty("content").GetString());
        Assert.Empty(body.RootElement.GetProperty("allowed_mentions").GetProperty("parse").EnumerateArray());
    }

    [Fact]
    public async Task TheFlashCommandIsRegisteredAgainstTheApplicationOfTheToken()
    {
        var rest = new RecordingRestClient();

        var registered = await rest.Client.RegisterFlashCommandAsync(CancellationToken.None);

        Assert.True(registered);
        var request = Assert.Single(rest.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal($"/api/v10/applications/{Application}/guilds/10/commands", request.Path);
    }

    [Fact]
    public async Task TheRegisteredCommandCarriesOneStringOption()
    {
        var rest = new RecordingRestClient();

        await rest.Client.RegisterFlashCommandAsync(CancellationToken.None);

        var request = Assert.Single(rest.Requests);
        using var body = JsonDocument.Parse(request.Body);
        var option = Assert.Single(body.RootElement.GetProperty("options").EnumerateArray());
        Assert.Equal("flash", body.RootElement.GetProperty("name").GetString());
        Assert.Equal(3, option.GetProperty("type").GetInt32());
        Assert.Equal("message", option.GetProperty("name").GetString());
        Assert.True(option.GetProperty("required").GetBoolean());
    }

    [Fact]
    public async Task TheMessageCommandIsRegisteredAgainstTheApplicationOfTheToken()
    {
        var rest = new RecordingRestClient();

        var registered = await rest.Client.RegisterMessageCommandAsync(CancellationToken.None);

        Assert.True(registered);
        var request = Assert.Single(rest.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal($"/api/v10/applications/{Application}/guilds/10/commands", request.Path);
    }

    [Fact]
    public async Task TheRegisteredMessageCommandCarriesOneStringOption()
    {
        var rest = new RecordingRestClient();

        await rest.Client.RegisterMessageCommandAsync(CancellationToken.None);

        var request = Assert.Single(rest.Requests);
        using var body = JsonDocument.Parse(request.Body);
        var option = Assert.Single(body.RootElement.GetProperty("options").EnumerateArray());
        Assert.Equal("message", body.RootElement.GetProperty("name").GetString());
        Assert.Equal(3, option.GetProperty("type").GetInt32());
        Assert.Equal("body", option.GetProperty("name").GetString());
        Assert.True(option.GetProperty("required").GetBoolean());
        Assert.Equal(2000, option.GetProperty("max_length").GetInt32());
    }

    [Fact]
    public async Task TheRegisteredFlashCommandCapsTheOptionAtTheTickerLength()
    {
        var rest = new RecordingRestClient();

        await rest.Client.RegisterFlashCommandAsync(CancellationToken.None);

        var request = Assert.Single(rest.Requests);
        using var body = JsonDocument.Parse(request.Body);
        var option = Assert.Single(body.RootElement.GetProperty("options").EnumerateArray());
        Assert.Equal(255, option.GetProperty("max_length").GetInt32());
    }

    [Fact]
    public async Task TheRenameCarriesTheNameWithoutAType()
    {
        var rest = new RecordingRestClient();

        var renamed = await rest.Client.RenameChannelAsync("77", "players [1]", CancellationToken.None);

        Assert.True(renamed);
        var request = Assert.Single(rest.Requests);
        Assert.Equal("PATCH", request.Method);
        Assert.Equal("/api/v10/channels/77", request.Path);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("players [1]", body.RootElement.GetProperty("name").GetString());
        Assert.False(body.RootElement.TryGetProperty("type", out _));
    }

    [Fact]
    public async Task TheCreateCarriesTheNameWithTheTextChannelType()
    {
        var rest = new RecordingRestClient();

        _ = await rest.Client.CreateChannelAsync("players [0]", CancellationToken.None);

        var request = Assert.Single(rest.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("/api/v10/guilds/10/channels", request.Path);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("players [0]", body.RootElement.GetProperty("name").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("type").GetInt32());
    }

    private sealed record RecordedRequest(string Method, string Path, string Body);

    private sealed class RecordingRestClient
    {
        public RecordingRestClient()
        {
            var handler = new RecordingHandler();
            Client = new DiscordRestClientService(
                new RecordingHttpClientFactory(handler),
                Options.Create(new DiscordOptions
                {
                    Enabled = true,
                    BotToken = Token,
                    GuildIdentifier = "10",
                }),
                NullLogger<DiscordRestClientService>.Instance);
            Requests = handler.Requests;
        }

        public DiscordRestClientService Client { get; }

        public List<RecordedRequest> Requests { get; }
    }

    private sealed class RecordingHttpClientFactory(RecordingHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(request.Method.Method, request.RequestUri!.PathAndQuery, body));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
        }
    }
}
