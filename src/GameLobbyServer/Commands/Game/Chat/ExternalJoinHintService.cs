using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Chat;

/// <summary>
/// Raises the line that tells a host whose game only the tailnet can reach how to
/// make it joinable from outside it.
/// <para>
/// It is sent as the host's own character — the speaker field is the host — so the
/// room reads it as a message from them rather than as a staff notice, and it is
/// sent to the host alone, because only the host has an address to publish.
/// </para>
/// <para>
/// Two facts gate it and each one missing makes the line either useless or
/// unaddressable, so neither is guessed at: the page is configured, and the
/// advertised address is inside the tailnet range.
/// </para>
/// </summary>
/// <param name="sessionHelper">Helper used to write the line.</param>
/// <param name="options">Server options, for the page and the advertised address.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class ExternalJoinHintService(
    SessionHelper sessionHelper,
    IOptions<ServerOptions> options,
    ILogger<ExternalJoinHintService> logger)
{
    /// <summary>Sends the host their line, or nothing when the two facts do not hold.</summary>
    /// <param name="session">Host connection the line is written to.</param>
    /// <param name="characterIdentifier">Character the line is spoken as.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SendAsync(
        TcpSession session,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        var serverOptions = options.Value;
        var hostname = serverOptions.ExternalWebHostname;

        if (string.IsNullOrWhiteSpace(hostname) ||
            !ExternalJoinUtils.IsTailnetAddress(serverOptions.AdvertisedAddress))
        {
            return;
        }

        var message = ExternalJoinUtils.BuildHintMessage(hostname);

        // The line shares the client's 127-byte read limit, so a hostname too long
        // to fit is reported rather than truncated into advice pointing nowhere.
        if (message.Length > ChatPayloadBuilder.MaximumTextLength)
        {
            logger.LogWarning(
                "EXTERNAL_WEB_HOSTNAME builds a {Length}-character line, past the {Maximum} the chat line carries; no line sent",
                message.Length,
                ChatPayloadBuilder.MaximumTextLength);
            return;
        }

        var line = new ChatRequest(ChatPayloadBuilder.PublicChannelDigit, message);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.SendChatResult,
            ChatPayloadBuilder.BuildServerReply(characterIdentifier, line),
            cancellationToken);
    }
}
