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

    /// <summary>User agent every upstream launcher request is made with, so the launcher tells the client from a generic crawler.</summary>
    public const string UpstreamUserAgent = "Mozilla/5.0 (PLAYSTATION 3; 3.55)";

    private readonly HttpApiOptions options = options.Value;

    /// <summary>Returns the policy document the client is shown.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<string> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        var upstreamPolicy = await FetchUpstreamPolicyAsync(cancellationToken);

        var original = !string.IsNullOrWhiteSpace(upstreamPolicy)
            ? upstreamPolicy.TrimEnd()
            : UpstreamUnavailableMessage;

        var localPolicy = await ReadLocalPolicyAsync(cancellationToken);
        return $"{localPolicy.TrimEnd()}\n{original}\n";
    }

    private async Task<string?> FetchUpstreamPolicyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateUpstreamClient();
            using var response = await client.GetAsync("/jp/mgo2/policy/policy.txt", cancellationToken);
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
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UpstreamUserAgent);
        return client;
    }
}
