using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Services;

/// <summary>
/// Serves the game client's help/tip text files from the static directory.
/// The client requests paths such as <c>/jp/mgo2/help/2_6.txt</c> after the
/// character-list packet is processed, and these must resolve so the tip
/// screens are not shown as broken downloads. A file that is not part of the
/// static content is answered with a notice instead of an error.
/// </summary>
/// <param name="options">Options of the HTTP API.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class HelpService(
    IOptions<HttpApiOptions> options,
    ILogger<HelpService> logger)
{
    /// <summary>Body used when the requested help file is not available.</summary>
    public const string ContentNotAvailable = "Content not available";

    private readonly HttpApiOptions options = options.Value;

    /// <summary>
    /// Returns the help file content for the given path below the help route,
    /// or a notice that the content is not available.
    /// </summary>
    /// <param name="filePath">Path of the help file below the help route.</param>
    public string GetHelpText(string filePath)
    {
        var localPath = ResolveLocalPath(filePath);
        if (localPath is null)
        {
            logger.LogWarning("Help file request for {FilePath} escapes the help directory", filePath);
            return ContentNotAvailable;
        }

        if (!File.Exists(localPath))
        {
            logger.LogInformation("Help file request for {FilePath} is not available", filePath);
            return ContentNotAvailable;
        }

        return File.ReadAllText(localPath);
    }

    /// <summary>Maps a request path to a location inside the help directory.</summary>
    /// <param name="filePath">Path of the file below the help route.</param>
    /// <returns>The resolved path, or <c>null</c> when it escapes the help directory.</returns>
    private string? ResolveLocalPath(string filePath)
    {
        var root = Path.GetFullPath(options.LocalHelpPath);
        var candidate = Path.GetFullPath(Path.Combine(root, filePath));
        return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? candidate
            : null;
    }
}
