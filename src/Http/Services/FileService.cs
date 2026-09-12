using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Services;

/// <summary>
/// Serves the client's patch files from the upstream launcher, keeping a local
/// copy of every file it downloads so a later request survives an upstream
/// outage. The header set of the original file server is reproduced, because
/// the client's downloader inspects the length and the entity tag.
/// </summary>
/// <param name="httpClientFactory">Factory the upstream requests are made with.</param>
/// <param name="options">Options of the HTTP API.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class FileService(
    IHttpClientFactory httpClientFactory,
    IOptions<HttpApiOptions> options,
    ILogger<FileService> logger)
{
    private readonly HttpApiOptions options = options.Value;

    /// <summary>Serves one patch file.</summary>
    /// <param name="filePath">Path of the file below the files route.</param>
    /// <param name="isHeadRequest">Whether the client only asked for the metadata.</param>
    /// <param name="rangeHeader">Range header of the request, when the client resumes a download.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<IResult> GetFileAsync(
        string filePath,
        bool isHeadRequest,
        string? rangeHeader = null,
        CancellationToken cancellationToken = default)
    {
        if (EscapesFilesDirectory(filePath))
        {
            return Results.NotFound();
        }

        var range = ParseRange(rangeHeader);
        var upstream = await TryFetchUpstreamAsync(filePath, isHeadRequest, range, cancellationToken);

        if (upstream is not null)
        {
            if (isHeadRequest)
            {
                return BuildResponse(
                    filePath,
                    StatusCodes.Status200OK,
                    null,
                    upstream.ContentLength,
                    upstream.ContentType,
                    upstream.LastModified,
                    upstream.EntityTag,
                    null);
            }

            var data = upstream.Data!;

            // A partial answer already is the slice the client asked for.
            if (range is not null && !upstream.IsPartial)
            {
                return BuildRangeResponse(filePath, data, range, upstream.LastModified, upstream.EntityTag);
            }

            // Only a whole file is cached, never a slice of one.
            if (!upstream.IsPartial)
            {
                await SaveLocalCopyAsync(filePath, data, cancellationToken);
            }

            return BuildResponse(
                filePath,
                upstream.IsPartial ? StatusCodes.Status206PartialContent : StatusCodes.Status200OK,
                data,
                data.Length,
                upstream.ContentType,
                upstream.LastModified,
                upstream.EntityTag,
                upstream.ContentRange);
        }

        return await ServeLocalAsync(filePath, isHeadRequest, range, cancellationToken);
    }

    private async Task<UpstreamFile?> TryFetchUpstreamAsync(
        string filePath,
        bool isHeadRequest,
        RangeRequest? range,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateUpstreamClient();
            using var request = new HttpRequestMessage(
                isHeadRequest ? HttpMethod.Head : HttpMethod.Get,
                $"/files/{filePath}");

            if (range is not null && !isHeadRequest)
            {
                request.Headers.Range = range.ToHeaderValue();
            }

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Upstream file request for {FilePath} failed with status {StatusCode}; falling back to the local copy",
                    filePath,
                    (int)response.StatusCode);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.ToString();

            // The upstream answers a missing or blocked file with a 200 whose
            // body is an HTML error page, so the status alone does not tell the
            // file from the error. Serving such a body would hand the client an
            // invalid patch and poison the local cache, so the answer is
            // rejected and the local copy takes over instead.
            if (contentType is not null && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Upstream file request for {FilePath} returned an HTML error page; falling back to the local copy",
                    filePath);
                return null;
            }

            var data = isHeadRequest
                ? null
                : await response.Content.ReadAsByteArrayAsync(cancellationToken);

            return new UpstreamFile(
                data,
                (int)response.StatusCode,
                response.Content.Headers.ContentLength,
                contentType,
                response.Content.Headers.LastModified?.ToString("r"),
                response.Headers.ETag?.ToString(),
                response.Content.Headers.ContentRange?.ToString());
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogInformation(exception, "Upstream file request for {FilePath} failed; falling back to the local copy", filePath);
            return null;
        }
    }

    private async Task<IResult> ServeLocalAsync(
        string filePath,
        bool isHeadRequest,
        RangeRequest? range,
        CancellationToken cancellationToken)
    {
        var localPath = ResolveLocalPath(filePath);
        if (localPath is null || !File.Exists(localPath))
        {
            return Results.NotFound();
        }

        var info = new FileInfo(localPath);
        var lastModified = info.LastWriteTimeUtc.ToString("r");
        var entityTag = BuildEntityTag(info);

        if (isHeadRequest)
        {
            return BuildResponse(
                filePath,
                StatusCodes.Status200OK,
                null,
                info.Length,
                null,
                lastModified,
                entityTag,
                null);
        }

        if (range is not null)
        {
            return await BuildRangeResponseAsync(filePath, info, range, lastModified, entityTag, cancellationToken);
        }

        var data = await File.ReadAllBytesAsync(localPath, cancellationToken);
        return BuildResponse(
            filePath,
            StatusCodes.Status200OK,
            data,
            data.Length,
            null,
            lastModified,
            entityTag,
            null);
    }

    private IResult BuildRangeResponse(
        string filePath,
        byte[] data,
        RangeRequest range,
        string? lastModified,
        string? entityTag)
    {
        var slice = range.Resolve(data.Length);
        if (slice is null)
        {
            return BuildResponse(
                filePath,
                StatusCodes.Status416RangeNotSatisfiable,
                null,
                null,
                null,
                lastModified,
                entityTag,
                $"bytes */{data.Length}");
        }

        var (start, end) = slice.Value;
        var body = new byte[(int)(end - start + 1)];
        Array.Copy(data, start, body, 0, body.Length);
        return BuildResponse(
            filePath,
            StatusCodes.Status206PartialContent,
            body,
            body.Length,
            null,
            lastModified,
            entityTag,
            $"bytes {start}-{end}/{data.Length}");
    }

    private async Task<IResult> BuildRangeResponseAsync(
        string filePath,
        FileInfo info,
        RangeRequest range,
        string lastModified,
        string entityTag,
        CancellationToken cancellationToken)
    {
        var slice = range.Resolve(info.Length);
        if (slice is null)
        {
            return BuildResponse(
                filePath,
                StatusCodes.Status416RangeNotSatisfiable,
                null,
                null,
                null,
                lastModified,
                entityTag,
                $"bytes */{info.Length}");
        }

        var (start, end) = slice.Value;
        var length = (int)(end - start + 1);
        var body = new byte[length];

        await using var stream = info.OpenRead();
        stream.Seek(start, SeekOrigin.Begin);
        await stream.ReadExactlyAsync(body, cancellationToken);

        return BuildResponse(
            filePath,
            StatusCodes.Status206PartialContent,
            body,
            length,
            null,
            lastModified,
            entityTag,
            $"bytes {start}-{end}/{info.Length}");
    }

    private IResult BuildResponse(
        string filePath,
        int statusCode,
        byte[]? data,
        long? contentLength,
        string? contentType,
        string? lastModified,
        string? entityTag,
        string? contentRange)
    {
        return new MirroredFileResult(filePath, statusCode, data, contentLength, contentType, lastModified, entityTag, contentRange);
    }

    private async Task SaveLocalCopyAsync(string filePath, byte[] data, CancellationToken cancellationToken)
    {
        try
        {
            var localPath = ResolveLocalPath(filePath);
            if (localPath is null)
            {
                return;
            }

            var directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(localPath, data, cancellationToken);
        }
        catch (IOException exception)
        {
            // Non-fatal: the file is still served, it is simply not cached.
            logger.LogWarning(exception, "Caching the local copy of {FilePath} failed", filePath);
        }
    }

    /// <summary>Maps a request path to a location inside the cache directory.</summary>
    /// <param name="filePath">Path of the file below the files route.</param>
    /// <returns>The resolved path, or <c>null</c> when it escapes the cache directory.</returns>
    private string? ResolveLocalPath(string filePath)
    {
        var root = Path.GetFullPath(options.LocalFilesPath);
        var candidate = Path.GetFullPath(Path.Combine(root, filePath));
        return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? candidate
            : null;
    }

    /// <summary>Reports whether the path climbs out of the files route.</summary>
    /// <param name="filePath">Path of the file below the files route.</param>
    private static bool EscapesFilesDirectory(string filePath)
    {
        return filePath.Split('/', '\\').Any(segment => segment == "..");
    }

    private HttpClient CreateUpstreamClient()
    {
        // The user agent is the one every upstream launcher request carries, so
        // the launcher tells the files service from a generic crawler the same
        // way it tells the policy service.
        var client = httpClientFactory.CreateClient(nameof(FileService));
        client.BaseAddress = new Uri(options.LauncherServer);
        client.Timeout = TimeSpan.FromMilliseconds(options.UpstreamFetchTimeoutMilliseconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(PolicyService.UpstreamUserAgent);
        return client;
    }

    /// <summary>Parses a single byte range of a Range header. A header the client's downloader never sends, such as a multipart range, is reported as absent.</summary>
    /// <param name="header">Value of the Range header.</param>
    private static RangeRequest? ParseRange(string? header)
    {
        if (string.IsNullOrEmpty(header))
        {
            return null;
        }

        var match = Regex.Match(header, @"^bytes=(\d*)-(\d*)$", RegexOptions.IgnoreCase);
        if (!match.Success || (match.Groups[1].Value.Length == 0 && match.Groups[2].Value.Length == 0))
        {
            return null;
        }

        var start = match.Groups[1].Value.Length == 0
            ? null
            : long.TryParse(match.Groups[1].Value, out var parsedStart) ? parsedStart : (long?)null;
        var end = match.Groups[2].Value.Length == 0
            ? null
            : long.TryParse(match.Groups[2].Value, out var parsedEnd) ? parsedEnd : (long?)null;

        return start is null && end is null ? null : new RangeRequest(start, end);
    }

    /// <summary>Entity tag of a locally cached file, in the style of the original file server.</summary>
    /// <param name="info">File the tag describes.</param>
    private static string BuildEntityTag(FileInfo info) =>
        $"\"{info.Length:x}-{info.LastWriteTimeUtc.ToFileTime():x}\"";

    /// <summary>Content type the client expects for a file.</summary>
    /// <param name="filePath">Path of the file.</param>
    public static string GetContentType(string filePath) => filePath switch
    {
        _ when filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) => "application/zip",
        _ when filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) => "text/plain; charset=utf-8",
        _ => "application/octet-stream",
    };

    private sealed record UpstreamFile(
        byte[]? Data,
        int StatusCode,
        long? ContentLength,
        string? ContentType,
        string? LastModified,
        string? EntityTag,
        string? ContentRange)
    {
        /// <summary>Whether the upstream answered the range itself with a slice.</summary>
        public bool IsPartial => StatusCode == StatusCodes.Status206PartialContent;
    }

    /// <summary>A single byte range of a Range header, with the bounds left open kept as null.</summary>
    /// <param name="Start">First byte requested, or null for a suffix range.</param>
    /// <param name="End">Last byte requested, or null for an open range.</param>
    private sealed record RangeRequest(long? Start, long? End)
    {
        /// <summary>Range header value the upstream request carries.</summary>
        public RangeHeaderValue ToHeaderValue() =>
            Start is { } start ? new RangeHeaderValue(start, End) : new RangeHeaderValue(null, End!.Value);

        /// <summary>Resolves the range against a length.</summary>
        /// <param name="length">Length of the file the range is resolved against.</param>
        /// <returns>The inclusive first and last byte, or null when the range is unsatisfiable.</returns>
        public (long Start, long End)? Resolve(long length)
        {
            long start;
            long end;

            if (Start is { } requestedStart)
            {
                start = requestedStart;
                end = End ?? length - 1;
            }
            else
            {
                // A suffix range names the number of trailing bytes.
                var suffix = End!.Value;
                if (suffix == 0)
                {
                    return null;
                }

                start = Math.Max(0, length - suffix);
                end = length - 1;
            }

            if (start >= length || start > end)
            {
                return null;
            }

            return (start, Math.Min(end, length - 1));
        }
    }

    /// <summary>Writes a mirrored file with the header set of the original file server.</summary>
    private sealed class MirroredFileResult(
        string filePath,
        int statusCode,
        byte[]? data,
        long? contentLength,
        string? contentType,
        string? lastModified,
        string? entityTag,
        string? contentRange) : IResult
    {
        /// <inheritdoc />
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var response = httpContext.Response;
            response.StatusCode = statusCode;

            // The original file server decides the type of the file, so its own
            // type is passed through and only a locally cached file falls back
            // to the one derived from the extension.
            response.ContentType = contentType ?? GetContentType(filePath);

            if (contentLength is { } length)
            {
                response.ContentLength = length;
            }

            if (lastModified is not null)
            {
                response.Headers.LastModified = lastModified;
            }

            if (entityTag is not null)
            {
                response.Headers.ETag = entityTag;
            }

            if (contentRange is not null)
            {
                response.Headers.ContentRange = contentRange;
            }

            response.Headers["Accept-Ranges"] = "bytes";
            response.Headers.Server = "nginx/1.18.0";
            response.Headers.Connection = "keep-alive";

            if (data is not null)
            {
                await response.Body.WriteAsync(data);
            }
        }
    }
}
