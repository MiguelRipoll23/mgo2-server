using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

/// <summary>
/// Takes the weapon-select value a client reports — one of
/// <c>0x12e2</c>–<c>0x12e7</c>, a two-byte body of the value and the player's
/// own slot — records it against that slot, and publishes the block the row
/// belongs to.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a choice on a screen, not a ready and not a round.</b> The report
/// arrives in the pre-game pass beside the client's <c>0x025c</c> and its join
/// report, and in a played round it arrives with the same clean slot byte
/// (<c>mgo2-started-on-game-round1</c>: <c>12e4 ff 11</c> · <c>12e3 ff 11</c> ·
/// <c>12e2 ff 11</c> at the client's own slot <c>0x11</c>). Nothing here opens
/// anything: the round's opener reads the join report, and this record only
/// moves one value of the room's pre-game state.
/// </para>
/// <para>
/// <b>The publication is the row with the trailer on it.</b> The reference host
/// answers each of the client's three reports with a three-byte row under the
/// same identifier and the same two leading bytes, in the same datagram as the
/// bare <c>0x52eN</c> answers — <c>12e4 ff 11</c> draws <c>52e4</c> and
/// <c>12e4 ff 11 18</c>. The rows go to the reporting peer as one frame and to
/// the rest of the room as the room's own records, which is the split every
/// publication in this server makes (<see cref="RosterRunSendUtils"/> and
/// <see cref="MatchRecords.ScoreboardPublishService"/>).
/// </para>
/// <para>
/// <b>One block per report, so a family nobody mentions is still published.</b>
/// The block is the slot's six rows rather than the one family the report
/// named: the reference host's block for a slot carries all six, <c>0xff</c>
/// for the families that slot never reported, and a row per report would leave
/// a client waiting on the five it did not send.
/// </para>
/// </remarks>
/// <param name="selections">The room's weapon-select values, which this report is recorded in.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class WeaponSelectionHandler(
    WeaponSelectionService selections,
    ILogger<WeaponSelectionHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        var body = context.Message.Body;
        if (!WeaponSelectionBodiesUtils.IsReport(context.Message.Type, body))
        {
            // The identifier carries the reporting client's own two-byte record
            // and nothing else. A body of another length is not a choice this
            // host can file, so it is logged and left rather than relayed as if
            // it were the room's.
            logger.LogDebug(
                "UDP {LocalPort}: 0x{MessageType:x4} of {BodyLength} bytes from {RemoteAddress} is not a "
                + "weapon-select report; left alone",
                context.LocalPort,
                context.Message.Type,
                body.Length,
                context.Remote);

            return;
        }

        var slot = body[WeaponSelectionBodiesUtils.SlotIndex];
        var value = body[WeaponSelectionBodiesUtils.ValueIndex];
        selections.Note(context.Message.Type, slot, value);

        var rows = selections.RowsFor(slot);
        logger.LogDebug(
            "UDP {LocalPort}: {RemoteAddress} reported 0x{Value:x2} under 0x{MessageType:x4} for slot "
            + "{Slot}; publishing {RowCount} weapon-select row(s) to it and the room",
            context.LocalPort,
            context.Remote,
            value,
            context.Message.Type,
            slot,
            rows.Count);

        await RosterRunSendUtils.SendAsync(context, rows);

        foreach (var row in rows)
        {
            await context.Broadcast(row.Type, row.Body, row.Ordinal);
        }
    }
}
