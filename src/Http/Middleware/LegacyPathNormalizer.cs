namespace Mgo2Server.Http.Middleware;

/// <summary>
/// Collapses the repeated slashes the game client sends below the <c>/jp/mgo2</c>
/// prefix, such as <c>/jp/mgo2//patch//checkver.html</c>. The paths are matched
/// literally and a route template cannot carry consecutive separators, so the
/// path is normalised before routing sees it.
/// </summary>
/// <param name="next">Next middleware in the pipeline.</param>
public sealed class LegacyPathNormalizer(RequestDelegate next)
{
    /// <summary>Prefix whose paths are normalised.</summary>
    private const string Prefix = "/jp/mgo2/";

    /// <summary>Normalises the request path, then continues the pipeline.</summary>
    /// <param name="context">Request being handled.</param>
    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (path is not null && path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Path = CollapseRepeatedSlashes(path);
        }

        return next(context);
    }

    /// <summary>Replaces every run of slashes with a single slash.</summary>
    /// <param name="path">Path to normalise.</param>
    private static string CollapseRepeatedSlashes(string path)
    {
        if (!path.Contains("//", StringComparison.Ordinal))
        {
            return path;
        }

        var builder = new System.Text.StringBuilder(path.Length);
        var previousWasSlash = false;
        foreach (var character in path)
        {
            if (character == '/')
            {
                if (previousWasSlash)
                {
                    continue;
                }

                previousWasSlash = true;
            }
            else
            {
                previousWasSlash = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
