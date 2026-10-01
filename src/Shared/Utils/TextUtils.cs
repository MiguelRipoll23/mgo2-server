namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Wraps text so no line runs past a caller's width.
/// <para>
/// The client renders a line at a fixed width and wraps nothing of its own, so a
/// message longer than that width runs off the edge of its container. Text the
/// client authors is already wrapped by the client before it reaches us; text the
/// server authors, and the documents it serves whole, are not, and are wrapped
/// here before they are written.
/// </para>
/// </summary>
public static class TextUtils
{
    /// <summary>Character inserted where a line is broken.</summary>
    private const char LineBreak = '\n';

    /// <summary>
    /// Breaks every line of <paramref name="text"/> that is longer than
    /// <paramref name="width"/>. A break is taken at the last space that fits, and
    /// at the width itself when a single word does not. Line breaks already in the
    /// text are kept, so a line that already fits is returned untouched.
    /// </summary>
    /// <param name="text">Text to wrap.</param>
    /// <param name="width">Longest line the wrapper may produce.</param>
    /// <returns>The text with a line break wherever a line reached the width.</returns>
    public static string Wrap(string text, int width)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Every break the text may already carry is folded to one character so the
        // lines below are split on a single separator, whatever the source used.
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = new List<string>();
        foreach (var line in normalized.Split(LineBreak))
        {
            AppendWrapped(lines, line, width);
        }

        return string.Join(LineBreak, lines);
    }

    /// <summary>Appends one line, broken into as many lines as its width needs.</summary>
    /// <param name="lines">Lines collected so far.</param>
    /// <param name="line">Line to break.</param>
    /// <param name="width">Longest line the wrapper may produce.</param>
    private static void AppendWrapped(List<string> lines, string line, int width)
    {
        var remaining = line;
        while (remaining.Length > width)
        {
            // A space in the window is the break a reader would have chosen; a
            // word wider than the window has none, and is broken at the width.
            var breakAt = remaining.LastIndexOf(' ', width);
            if (breakAt <= 0)
            {
                breakAt = width;
            }

            lines.Add(remaining[..breakAt].TrimEnd());
            remaining = remaining[breakAt..].TrimStart();
        }

        lines.Add(remaining);
    }
}
