using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Chat;

/// <summary>
/// Raises the line that tells a host whose game only the tailnet can reach how to
/// make it reachable from outside it.
/// <para>
/// It is sent as the host's own character — the speaker field is the host — so the
/// room reads it as a message from them rather than as a staff notice, and it is
/// sent to the host alone, because only the host has an address to publish.
/// </para>
/// <para>
/// Three facts gate it and each one missing makes the line either useless or
/// unresolvable, so none of them is guessed at: an external page is configured,
/// the secret that page resolves codes with is present, and the advertised address
/// is inside the tailnet range. See <c>docs/HOST_LINKS.md</c> for the contract the
/// page reproduces.
/// </para>
/// </summary>
/// <param name="characterService">Service the host's name is read from.</param>
/// <param name="sessionHelper">Helper used to write the line.</param>
/// <param name="options">Server options, for the page, the secret and the advertised address.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class HostLinkService(
    CharacterService characterService,
    SessionHelper sessionHelper,
    IOptions<ServerOptions> options,
    ILogger<HostLinkService> logger)
{
    /// <summary>Sends the host their link line, or nothing when the three facts do not hold.</summary>
    /// <param name="session">Host connection the line is written to.</param>
    /// <param name="characterIdentifier">Character the link is built for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SendAsync(
        TcpSession session,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        var serverOptions = options.Value;
        var baseUrl = serverOptions.ExternalServerBaseUrl;
        var secret = serverOptions.JwtSecret;

        if (string.IsNullOrWhiteSpace(baseUrl) ||
            string.IsNullOrWhiteSpace(secret) ||
            !HostLinkUtils.IsTailnetAddress(serverOptions.AdvertisedAddress))
        {
            return;
        }

        var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
        if (character is null)
        {
            return;
        }

        var message = HostLinkUtils.BuildMessage(baseUrl, HostLinkUtils.BuildCode(secret, character.Name));

        // The line shares the client's 127-byte read limit, so a base address too
        // long to fit is reported rather than truncated into a link that points
        // nowhere.
        if (message.Length > ChatPayloadBuilder.MaximumTextLength)
        {
            logger.LogWarning(
                "EXTERNAL_SERVER_BASE_URL builds a {Length}-character host link, past the {Maximum} the chat line carries; no link sent",
                message.Length,
                ChatPayloadBuilder.MaximumTextLength);
            return;
        }

        var line = new ChatRequest(ChatPayloadBuilder.PublicChannelDigit, message);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.SendChatResult,
            ChatPayloadBuilder.BuildReply(characterIdentifier, line),
            cancellationToken);
    }
}
