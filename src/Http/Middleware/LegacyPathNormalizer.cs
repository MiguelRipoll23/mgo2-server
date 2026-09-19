namespace Mgo2Server.Http.Middleware;

/// <summary>
/// Canonicalises the game paths the client sends, before routing sees them.
/// <para>
/// Two things vary in a path the game composes, and neither is fixed by anything
/// the server holds. The <em>region</em> comes from the disc: the first segment is
/// the region its own address table names, so a North American disc asks for
/// <c>/us/mgo2/…</c>, a European one for <c>/eu/mgo2/…</c> and a Japanese one for
/// <c>/jp/mgo2/…</c>. And the client repeats separators below the prefix, as in
/// <c>/us/mgo2//patch//checkver.html</c>.
/// </para>
/// <para>
/// Both are folded onto the single form the routes are declared in, because the
/// paths are matched literally and a route template can carry neither a second
/// region nor consecutive separators. One declaration therefore answers every
/// disc, instead of only the region it was written against.
/// </para>
/// </summary>
/// <param name="next">Next middleware in the pipeline.</param>
public sealed class LegacyPathNormalizer(RequestDelegate next)
{
    /// <summary>Region the routes are declared under, which every other one is folded onto.</summary>
    private const string DeclaredRegion = "jp";

    /// <summary>Second segment of every game path, which is the same in all regions.</summary>
    private const string GameSegment = "mgo2";

    /// <summary>Width of a region segment, which is always two letters.</summary>
    private const int RegionLength = 2;

    /// <summary>Normalises the request path, then continues the pipeline.</summary>
    /// <param name="context">Request being handled.</param>
    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (path is not null)
        {
            var normalized = Normalize(path);
            if (!string.Equals(normalized, path, StringComparison.Ordinal))
            {
                context.Request.Path = normalized;
            }
        }

        return next(context);
    }

    /// <summary>
    /// Folds the region onto the declared one and collapses repeated separators, for a
    /// path of the <c>/&lt;region&gt;/mgo2/…</c> shape. Anything else is returned
    /// unchanged, so the API's own paths keep their meaning.
    /// </summary>
    /// <param name="path">Path to normalise.</param>
    public static string Normalize(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 ||
            segments[0].Length != RegionLength ||
            !string.Equals(segments[1], GameSegment, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        segments[0] = DeclaredRegion;
        return string.Concat("/", string.Join('/', segments));
    }
}
