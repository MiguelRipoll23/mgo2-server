using System.Text;
using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>Validation of the text fields the team commands carry.</summary>
internal static class EventTeamTextUtils
{
    /// <summary>
    /// Retail accepts three to sixteen encoded bytes. A shorter or longer string
    /// is refused rather than padded, because the slot is the client's own field
    /// and padding would change what was typed.
    /// </summary>
    /// <param name="value">Text to test.</param>
    public static bool IsValidTeamName(string value) => IsValidText(value, 3, 16);

    /// <summary>Validates the password of a protected team.</summary>
    /// <param name="flagBits">Option bits of the team.</param>
    /// <param name="password">Password to test.</param>
    public static bool IsValidPassword(int flagBits, string password) =>
        (flagBits & EventTeamService.PasswordProtectedFlag) == 0 || IsValidText(password, 3, 16);

    private static bool IsValidText(string value, int minimum, int maximum)
    {
        if (value is null)
        {
            return false;
        }

        // Encoded length, because the client measures the wire field rather than
        // the character count, and the control-byte rule is about encoded bytes.
        var encoded = Encoding.Latin1.GetBytes(value);
        if (encoded.Length < minimum || encoded.Length > maximum)
        {
            return false;
        }

        foreach (var item in encoded)
        {
            if (item is >= 0x01 and <= 0x1f)
            {
                return false;
            }
        }

        return true;
    }
}