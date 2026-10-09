using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Chat;

/// <summary>
/// Reads and writes the in-game chat family. A send (<c>0x4400</c>) is
/// <c>{u8 kind, u8 channel digit, NUL-terminated text}</c> and the line the
/// client displays (<c>0x4401</c>) is <c>{u32 speaker, channel digit, text, NUL}</c>.
/// <para>
/// The channel lives in the ASCII digit, not in <c>kind</c>: the client
/// computes the channel as <c>digit - '0'</c> and takes the text from the byte
/// after it, so the byte that opens the reply's string must be the digit the
/// sender sent. <c>kind</c> is only a coarse public/team flag and the two
/// disagree for channels 2 and 3, so answering with <c>kind</c> shifts the
/// string one byte and puts a channel the client cannot resolve where the text
/// should start.
/// </para>
/// </summary>
public static class ChatPayloadBuilder
{
    /// <summary>Longest message the wire can carry, as the client's reader takes it.</summary>
    public const int MaximumTextLength = 127;

    private const int VisibleLineWidth = 58;

    /// <summary>
    /// The channel digit of team chat — the one channel that does not reach the whole
    /// room. Channels 0 and 2 are public and 3 resolves speakers against a
    /// server-supplied table, so neither may be narrowed to a team.
    /// </summary>
    public const byte TeamChannelDigit = (byte)'1';

    /// <summary>
    /// The channel digit of the room-wide channel, and the one a line the server
    /// raises on a client's behalf is sent on: it is public, so it needs no team
    /// table to resolve, and it is the digit the client itself writes for /all.
    /// </summary>
    public const byte PublicChannelDigit = (byte)'0';

    /// <summary>Whether a send is team chat.</summary>
    /// <param name="request">Decoded send.</param>
    public static bool IsTeamChannel(ChatRequest request) => request.ChannelDigit == TeamChannelDigit;

    /// <summary>Bytes of the request that precede the text: the kind and the channel digit.</summary>
    private const int RequestHeaderLength = 2;

    /// <summary>Decodes a <c>0x4400</c> payload, or null when it is too short to be one.</summary>
    /// <param name="payload">Request payload.</param>
    public static ChatRequest? ParseRequest(byte[] payload)
    {
        if (payload.Length < RequestHeaderLength)
        {
            return null;
        }

        var reader = new PacketReader(payload);
        // The kind is a coarse public/team flag, kept only so the digit behind it
        // stays at its offset. It is never relayed: the digit is the channel.
        reader.ReadUInt8();
        var channelDigit = reader.ReadUInt8();
        var text = reader.Remaining > 0
            ? reader.ReadFixedString(Math.Min(reader.Remaining, MaximumTextLength))
            : string.Empty;

        return new ChatRequest(channelDigit, StripChannelPrefix(text));
    }

    /// <summary>
    /// Builds the <c>0x4401</c> line for a message the server authored itself. A
    /// line relayed from a client already carries the client's own wrapping, but a
    /// line the server raises has none, so it is wrapped to the longest text the
    /// client's chat reader takes.
    /// </summary>
    /// <param name="speakerCharacterIdentifier">Character id the line is spoken as.</param>
    /// <param name="request">Line the server authored.</param>
    public static byte[] BuildServerReply(int speakerCharacterIdentifier, ChatRequest request) =>
        BuildReply(
            speakerCharacterIdentifier,
            request with { Text = TextUtils.Wrap(request.Text, VisibleLineWidth) });

    /// <summary>
    /// Builds the <c>0x4401</c> line to display: the speaker's character id, then the
    /// channel digit and text as the sender gave them.
    /// </summary>
    /// <param name="speakerCharacterIdentifier">Character id of the speaker.</param>
    /// <param name="request">Decoded send being relayed.</param>
    public static byte[] BuildReply(int speakerCharacterIdentifier, ChatRequest request)
    {
        var writer = new PacketWriter();
        writer.WriteUInt32((uint)speakerCharacterIdentifier);
        writer.WriteUInt8(request.ChannelDigit);
        // Terminated and not padded: the client's reader stops on the delimiter,
        // so the text occupies its length plus one byte and nothing more.
        writer.WriteFixedString(request.Text, request.Text.Length + 1);
        return writer.Build();
    }

    /// <summary>
    /// Removes a channel prefix the client may have left in the text. It parses
    /// <c>/all</c> and <c>/team</c> itself and normally sends only the body, so this
    /// is defensive rather than the usual path.
    /// </summary>
    /// <param name="message">Text as it arrived.</param>
    private static string StripChannelPrefix(string message)
    {
        var stripped = message;
        if (stripped.StartsWith("/all", StringComparison.Ordinal))
        {
            stripped = stripped[4..];
        }
        else if (stripped.StartsWith("/team", StringComparison.Ordinal))
        {
            stripped = stripped[5..];
        }

        return stripped.StartsWith(' ') ? stripped.TrimStart() : stripped;
    }
}

/// <summary>One decoded chat send: the digit to relay and the text to display.</summary>
/// <param name="ChannelDigit">ASCII channel digit the sender put on the wire.</param>
/// <param name="Text">Message body, with any channel prefix already removed.</param>
public sealed record ChatRequest(int ChannelDigit, string Text);
