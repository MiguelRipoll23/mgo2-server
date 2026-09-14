using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Services;

/// <summary>
/// Serves the terms-of-service document: the local copy followed by whatever
/// the upstream launcher returns, or a timeout notice when it cannot be reached.
/// </summary>
/// <param name="httpClientFactory">Factory the upstream request is made with.</param>
/// <param name="options">Options of the HTTP API.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class PolicyService(
    IHttpClientFactory httpClientFactory,
    IOptions<HttpApiOptions> options,
    ILogger<PolicyService> logger)
{
    /// <summary>Text returned when the upstream document is unavailable.</summary>
    private const string UpstreamUnavailableMessage = "Request has timed out.";

    private readonly HttpApiOptions options = options.Value;

    /// <summary>Returns the policy document the client is shown.</summary>
    /// <param name="request">Original client request whose headers are forwarded upstream.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<string> GetPolicyAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        var upstreamPolicy = await FetchUpstreamPolicyAsync(request, cancellationToken);

        var original = !string.IsNullOrWhiteSpace(upstreamPolicy)
            ? upstreamPolicy.TrimEnd()
            : UpstreamUnavailableMessage;

        var localPolicy = await ReadLocalPolicyAsync(cancellationToken);
        return $"{localPolicy.TrimEnd()}\n{original}\n";
    }

    private async Task<string?> FetchUpstreamPolicyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateUpstreamClient();
            using var upstreamRequest = new HttpRequestMessage(HttpMethod.Get, "/jp/mgo2/policy/policy.txt");

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
                "Upstream policy request failed with status {StatusCode}; using the fallback message",
                (int)response.StatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogInformation(exception, "Upstream policy request failed; using the fallback message");
        }

        return null;
    }

    private async Task<string> ReadLocalPolicyAsync(CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(options.LocalPolicyPath);
        if (!File.Exists(path))
        {
            logger.LogWarning("Local policy file {Path} is missing", path);
            return string.Empty;
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    /// <summary>Builds the client the upstream launcher is reached with.</summary>
    public HttpClient CreateUpstreamClient()
    {
        var client = httpClientFactory.CreateClient(nameof(PolicyService));
        client.BaseAddress = new Uri(options.LauncherServer);
        client.Timeout = TimeSpan.FromMilliseconds(options.UpstreamFetchTimeoutMilliseconds);
        return client;
    }
}
