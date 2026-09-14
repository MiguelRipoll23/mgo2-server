using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Services;

/// <summary>
/// Serves the game client's help/tip text files from the upstream launcher.
/// The client requests paths such as <c>/jp/mgo2/help/2_6.txt</c> after the
/// character-list packet is processed, and these must resolve so the tip
/// screens are not shown as broken downloads.
/// </summary>
/// <param name="httpClientFactory">Factory the upstream requests are made with.</param>
/// <param name="options">Options of the HTTP API.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class HelpService(
    IHttpClientFactory httpClientFactory,
    IOptions<HttpApiOptions> options,
    ILogger<HelpService> logger)
{
    private readonly HttpApiOptions options = options.Value;

    /// <summary>
    /// Returns the help file content for the given path below the help route.
    /// The path is validated against directory traversal before the upstream
    /// request is made.
    /// </summary>
    /// <param name="filePath">Path of the help file below the help route.</param>
    /// <param name="request">Original client request whose headers are forwarded upstream.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<string> GetHelpTextAsync(string filePath, HttpRequest request, CancellationToken cancellationToken = default)
    {
        var escaped = EscapesHelpDirectory(filePath);
        if (escaped)
        {
            logger.LogWarning("Help file request for {FilePath} escapes the help directory", filePath);
            return string.Empty;
        }

        try
        {
            var client = CreateUpstreamClient();
            using var upstreamRequest = new HttpRequestMessage(HttpMethod.Get, $"/jp/mgo2/help/{filePath}");

            foreach (var header in request.Headers)
            {
                upstreamRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            using var response = await client.SendAsync(upstreamRequest, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }

            logger.LogInformation(
                "Upstream help request for {FilePath} failed with status {StatusCode}",
                filePath,
                (int)response.StatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogInformation(exception, "Upstream help request for {FilePath} failed", filePath);
        }

        return string.Empty;
    }

    /// <summary>Reports whether the path climbs out of the help directory.</summary>
    /// <param name="filePath">Path of the help file.</param>
    private static bool EscapesHelpDirectory(string filePath)
    {
        return filePath.Split('/', '\\').Any(segment => segment == "..");
    }

    /// <summary>Builds the client the upstream launcher is reached with.</summary>
    public HttpClient CreateUpstreamClient()
    {
        var client = httpClientFactory.CreateClient(nameof(HelpService));
        client.BaseAddress = new Uri(options.LauncherServer);
        client.Timeout = TimeSpan.FromMilliseconds(options.UpstreamFetchTimeoutMilliseconds);
        return client;
    }
}
