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
    public async Task TheFlashCommandIsRegisteredAgainstTheApplicationOfTheToken()
    {
        var rest = new RecordingRestClient();

        var registered = await rest.Client.RegisterFlashCommandAsync(CancellationToken.None);

        Assert.True(registered);
        var request = Assert.Single(rest.Requests);
        Assert.Equal("PUT", request.Method);
        Assert.Equal($"/api/v10/applications/{Application}/guilds/10/commands", request.Path);
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