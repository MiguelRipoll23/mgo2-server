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
/// <para>    /// <b>The request's class bit is not part of the confirmation.</b> The
    /// captured client sends its first macro under <c>0x1a58</c> and every later one
    /// under <c>0x9a58</c>, with the class ordinal climbing; the host answers each
    /// request under <c>0x1a58</c> with that same ordinal. The handler is
    /// registered on both forms, because a registry keyed on the base alone never
    /// sees the later requests. Answering a reliable request under the reliable
    /// identifier would also make the confirmation a numbered record the client
    /// must answer, and only the first macro is ever accepted.
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

        // The confirmation keeps the request's ordinal but drops its reliable bit:
        // the capture's answer is `0x1a58` for a `0x9a58` request. The handler is
        // registered on the class-bit form as well, so that request arrives here
        // instead of falling through to the relay. Broadcast excludes the sender,
        // so the sender gets its own confirmation through Send and the rest of the
        // room gets the same host record.
        var confirmedType = ChatMacroBodiesUtils.ConfirmedIdentifier(context.Message.Type);
        await context.Send(confirmedType, confirmed, context.Message.Flags);
        await context.Broadcast(confirmedType, confirmed, context.Message.Flags);
    }
}
