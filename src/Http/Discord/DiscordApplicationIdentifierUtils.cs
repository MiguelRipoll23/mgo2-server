using System.Text;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Reads the application identifier out of a bot token. The first segment of a
/// bot token is the base64 of the identifier, so the deployment does not have
/// to carry the identifier as a setting of its own.
/// </summary>
public static class DiscordApplicationIdentifierUtils
{
    /// <summary>Decodes the application identifier a bot token belongs to.</summary>
    /// <param name="botToken">Bot token of the application.</param>
    /// <returns>The identifier, or <c>null</c> when the token does not carry one.</returns>
    public static string? FromBotToken(string? botToken)
    {
        if (string.IsNullOrWhiteSpace(botToken))
        {
            return null;
        }

        var separator = botToken.IndexOf('.');
        if (separator <= 0)
        {
            return null;
        }

        try
        {
            var encoded = botToken[..separator].Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');

            var identifier = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            return identifier.Length > 0 && identifier.All(char.IsAsciiDigit) ? identifier : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}