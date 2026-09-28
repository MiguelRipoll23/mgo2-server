namespace Mgo2Server.GameLobbyServer.Commands.Game.Chat;

/// <summary>
/// Decides who a chat line reaches. Every channel goes to the whole room except
/// team chat, which is narrowed to the sender's own team once the roster holds one.
/// <para>
/// The narrowing is deliberately one-directional: whenever a team cannot be read —
/// the channel is not a team one, the sender has no row, the recipient has none —
/// the line is delivered rather than withheld. A dropped line is a message the
/// player typed and never saw, and a message that reaches too far is at worst the
/// whole-room delivery this server used to do for every channel.
/// </para>
/// </summary>
public static class ChatRecipientUtils
{
    /// <summary>Whether a line on this channel reaches this recipient.</summary>
    /// <param name="recipientIdentifier">Character on the receiving connection, when it has one.</param>
    /// <param name="teams">Room roster by character id for a team channel, or null for every other channel.</param>
    /// <param name="senderTeam">Team of the sender, or null when the roster holds none.</param>
    public static bool ReachesRecipient(
        int? recipientIdentifier,
        IReadOnlyDictionary<int, short>? teams,
        short? senderTeam)
    {
        if (teams is null || senderTeam is null)
        {
            return true;
        }

        if (recipientIdentifier is not { } recipient || !teams.TryGetValue(recipient, out var recipientTeam))
        {
            return true;
        }

        return recipientTeam == senderTeam;
    }
}
