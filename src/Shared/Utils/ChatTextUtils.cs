namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Splits a line the server authors into the messages the chat client can show.
/// <para>
/// The client draws one line and wraps nothing of its own, so a message wider
/// than the line it draws runs off the screen. The width it draws at is narrower
/// than the 127 bytes a chat packet carries, so a long server line is more than
/// one message — and it is more than one message rather than one message with a
/// break in it, because the client has no break character to honour.
/// </para>
/// </summary>
public static class ChatTextUtils
{
    /// <summary>Longest message the client's chat line draws.</summary>
    public const int VisibleLineWidth = 58;

    /// <summary>
    /// Breaks server-authored chat text into the messages to send, one per line
    /// the client can draw.
    /// </summary>
    /// <param name="text">Text the server authored.</param>
    /// <returns>The messages, in order; empty when there is nothing to send.</returns>
    public static IReadOnlyList<string> SplitMessages(string text) =>
        TextUtils.Split(text, VisibleLineWidth);
}
