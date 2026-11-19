using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Gameplay;

/// <summary>
/// Chooses who hears a confirmed chat macro: the sender's side while a round is
/// playing, and the whole room once it has ended.
/// </summary>
/// <remarks>
/// <para>
/// <b>The round is the gate.</b> <see cref="MatchRoundStateService.IsOpen"/> is
/// true only while a round is playing; the briefing before it and the result
/// screens after it are room-wide. That is the same boundary the round's own
/// records use, so a macro sent while the room is still placing players is not
/// kept inside a side they have not been put on yet.
/// </para>
/// <para>
/// <b>The side comes from the lobby-slot read, not from the chat body.</b> The
/// body's slot is what the client names and the confirmation repeats; the
/// audience is resolved from the endpoint the macro arrived on and the side the
/// room's own rows hold for that slot (<see cref="LobbySlotService"/>). A macro
/// whose sender has no side yet is confirmed to the sender only; a room-wide
/// send would cross sides.
/// </para>
/// </remarks>
/// <param name="roster">The room's roster, which maps an endpoint to a slot.</param>
/// <param name="lobbySlots">The room's slot-to-side assignments.</param>
/// <param name="roundState">The room's round, which says whether a round is open.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class ChatMacroAudienceService(
    RoomRosterService roster,
    LobbySlotService lobbySlots,
    MatchRoundStateService roundState,
    ILogger<ChatMacroAudienceService> logger)
{
    /// <summary>
    /// Sends a confirmed chat macro to its audience.
    /// </summary>
    /// <param name="context">Context the macro arrived through.</param>
    /// <param name="messageType">Identifier the host confirmation travels under.</param>
    /// <param name="body">Confirmed body.</param>
    /// <param name="ordinal">Fourth byte of the record.</param>
    public async Task BroadcastAsync(
        PeerContext context,
        ushort messageType,
        byte[] body,
        byte ordinal)
    {
        if (!roundState.IsOpen)
        {
            await context.Broadcast(messageType, body, ordinal);
            return;
        }

        if (context.BroadcastTo is not { } broadcastTo)
        {
            logger.LogWarning(
                "UDP {LocalPort}: a chat macro arrived without a way to address its side; "
                + "only the sender's confirmation is sent",
                context.LocalPort);
            return;
        }

        var sender = FindSender(context);
        if (sender is null)
        {
            logger.LogWarning(
                "UDP {LocalPort}: a chat macro arrived from {RemoteAddress} with no roster entry; "
                + "its side cannot be resolved, so only the sender's confirmation is sent",
                context.LocalPort,
                context.Remote);
            return;
        }

        var slots = await lobbySlots.ReadAsync();
        var senderSlot = slots.FirstOrDefault(slot => slot.Slot == sender.SessionSlot);
        if (senderSlot is null)
        {
            logger.LogWarning(
                "UDP {LocalPort}: a chat macro from slot {Slot} has no lobby-slot entry; "
                + "its side cannot be resolved, so only the sender's confirmation is sent",
                context.LocalPort,
                sender.SessionSlot);
            return;
        }

        var sameSide = slots
            .Where(slot => slot.Team == senderSlot.Team)
            .Select(slot => slot.Slot)
            .ToHashSet();
        var recipients = roster.Snapshot()
            .Where(member => !member.IsBot && sameSide.Contains(member.SessionSlot))
            .Select(member => member.RemoteAddress)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        logger.LogDebug(
            "UDP {LocalPort}: chat macro from slot {Slot} on side {Side} goes to {RecipientCount} "
            + "same-side peer(s) in round {RoundNumber}",
            context.LocalPort,
            sender.SessionSlot,
            senderSlot.Team,
            recipients.Count,
            roundState.RoundNumber);
        await broadcastTo(recipients, messageType, body, ordinal);
    }

    /// <summary>
    /// The roster entry the macro arrived from, or <c>null</c> while it has none.
    /// </summary>
    /// <param name="context">Context the macro arrived through.</param>
    /// <returns>The sender's roster entry, when the room holds one.</returns>
    private RosterMember? FindSender(PeerContext context) =>
        roster.Find(context.Remote.ToString())
        ?? roster.Find(context.Session.RemoteAddress)
        ?? roster.Find(context.Session.DialBack.ToString());
}
