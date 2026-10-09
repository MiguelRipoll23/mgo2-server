using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Chat;

/// <summary>
/// Answers the public-address chat command from the endpoint the character itself
/// registered, and not from the live socket.
/// <para>
/// The value comes from the character's row in <c>character_connections</c>, the
/// same row every other player is handed as this character's peer endpoint. That
/// is the address the game will actually try to reach, whereas the socket address
/// is only what this connection saw this instant; behind NAT the two differ, and
/// only the row is the one a peer could use.
/// </para>
/// <para>
/// The line is written as the character's own message and goes to the character
/// alone, because there is nothing in it for anyone else to read. It is answered
/// in the character's own words, with no server prefix.
/// </para>
/// </summary>
/// <param name="gameService">Service the registered endpoint is read from.</param>
/// <param name="sessionHelper">Helper used to write the line.</param>
public sealed class PublicIpChatCommandService(
    GameService gameService,
    SessionHelper sessionHelper)
{
    /// <summary>Text that triggers the command.</summary>
    public const string CommandText = "/ip";

    /// <summary>Line sent when the character has no endpoint, or none worth printing.</summary>
    private const string NoAddressMessage = "No public address is registered for your character.";

    /// <summary>Whether a chat line is the public-address command.</summary>
    /// <param name="text">Chat text as it arrived.</param>
    public static bool IsCommand(string text) =>
        string.Equals(text.Trim(), CommandText, StringComparison.OrdinalIgnoreCase);

    /// <summary>Answers the command to the character that asked.</summary>
    /// <param name="session">Connection the line is written to.</param>
    /// <param name="characterIdentifier">Character the line is spoken as.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleAsync(
        TcpSession session,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        var information = await gameService.GetConnectionInformationAsync(
            characterIdentifier,
            cancellationToken);

        var line = new ChatRequest(
            ChatPayloadBuilder.PublicChannelDigit,
            BuildMessage(information?.PublicIpAddress));

        foreach (var payload in ChatPayloadBuilder.BuildServerReplies(characterIdentifier, line))
        {
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.SendChatResult,
                payload,
                cancellationToken);
        }
    }

    /// <summary>
    /// The line for a registered address, or the refusal when there is none.
    /// </summary>
    /// <param name="publicIpAddress">Address from the character's connection row.</param>
    public static string BuildMessage(string? publicIpAddress)
    {
        var address = publicIpAddress?.Trim();
        if (string.IsNullOrEmpty(address))
        {
            return NoAddressMessage;
        }

        var message = $"Your public IP is {address}.";

        // An address too long to fit is reported as absent rather than cut short:
        // a truncated address still reads as a real one and would send the player
        // hunting a NAT problem they do not have. The refusal is a fixed short
        // string, so it always fits.
        return message.Length > ChatPayloadBuilder.MaximumTextLength ? NoAddressMessage : message;
    }
}
