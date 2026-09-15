using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Services;

/// <summary>
/// Serves the terms-of-service document the client is shown, read from the
/// local static directory.
/// </summary>
/// <param name="options">Options of the HTTP API.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class PolicyService(
    IOptions<HttpApiOptions> options,
    ILogger<PolicyService> logger)
{
    private readonly HttpApiOptions options = options.Value;

    /// <summary>Returns the policy document the client is shown.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<string> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.GetFullPath(options.LocalPolicyPath);
        if (!File.Exists(path))
        {
            logger.LogWarning("Local policy file {Path} is missing", path);
            return string.Empty;
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
