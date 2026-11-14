using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Gameplay;

/// <summary>
/// Confirms a client's chat macro and passes it to the room as the host's
/// broadcast rather than the client's request.
/// </summary>
/// <remarks>
/// <para>
/// <b>A macro only appears when the host answers it.</b> The captured client
/// writes <c>01 &lt;slot&gt; &lt;macro&gt; 02</c> and the captured host writes
/// <c>02 &lt;slot&gt; &lt;macro&gt; 02</c> back to the same peer and to the room
/// (<c>.pcapngs/actions/sending-chat-macros.pcapng</c>). The generic relay sends
/// the client's kind <c>1</c> only to the *other* peers, so the sender never sees
/// its own macro confirmed and the other client sees a request the host has not
/// acknowledged.
/// </para>
/// <para>
/// <b>The identifier also carries the room count.</b> Only the four-byte body is
/// a chat macro; a two-byte count is relayed unchanged.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this handler.</param>
public sealed class ChatMacroHandler(ILogger<ChatMacroHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        var body = context.Message.Body;
        if (!ChatMacroBodiesUtils.IsChatMacro(body))
        {
            // The room count and any other shape under this identifier keep the
            // relay's fate.
            var relay = context.Relay ?? context.Broadcast;
            await relay(context.Message.Type, body, context.Message.Flags);
            return;
        }

        var confirmed = ChatMacroBodiesUtils.Build(
            ChatMacroBodiesUtils.HostKind,
            body[1],
            body[2]);
        logger.LogDebug(
            "UDP {LocalPort}: {RemoteAddress} sends chat macro {Macro} for slot {Slot}; "
            + "confirmed as host kind and broadcast",
            context.LocalPort,
            context.Remote,
            body[2],
            body[1]);

        // Broadcast excludes the sender, so the sender gets its own confirmation
        // through Send and the rest of the room gets the same host record.
        await context.Send(context.Message.Type, confirmed, context.Message.Flags);
        await context.Broadcast(context.Message.Type, confirmed, context.Message.Flags);
    }
}
