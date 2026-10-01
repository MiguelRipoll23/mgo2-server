using Mgo2Server.Http.Options;
using Mgo2Server.Shared.Utils;
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
    /// <summary>
    /// Width the client's terms screen holds, in characters. The screen wraps
    /// nothing, so a longer line runs off its edge; the document is wrapped here
    /// rather than relying on whoever edits it to keep the lines short.
    /// </summary>
    private const int PolicyLineWidth = 58;

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

        var document = await File.ReadAllTextAsync(path, cancellationToken);
        return TextUtils.Wrap(document, PolicyLineWidth);
    }
}
