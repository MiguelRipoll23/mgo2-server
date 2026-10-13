using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

/// <summary>
/// Recognises the one-byte presence record a client sends beside its readiness
/// report, which carries the player's own lobby slot.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is not the briefing ready, and its name used to say it was.</b> The
/// record this handler takes is the companion of the report — same datagram,
/// immediately before it, an identifier that splits the same way the report's
/// does — and where the report says a player is ready to play, this one says
/// which player has arrived: its byte is that player's own lobby slot, the same
/// byte the round's run payload echoes back
/// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2, <c>BRIEFING_FLOW.md</c> §1):
/// </para>
/// <code>
/// 0x0a5c len 1 class 4 14   .   0x1ade len 2 class 0 e1 14
/// 0x025c len 1 class 4 01   .   0x12de len 2 class 0 e1 01
/// </code>
/// <para>
/// <b>The host answers it with the room's players.</b> Four bytes under class 6
/// whose first is a bit set that grows as the room's players report:
/// <c>mgo2-game-started-on-briefing</c> answers <c>02</c> and then <c>06</c>,
/// <c>0e</c> and <c>1e</c> as its other players report, and
/// <c>mgo2-game-joined-when-red-team-then-briefing</c> answers <c>2a</c> — the
/// slots its reporting players hold. That answer rides inside the round's
/// opening run here (<see cref="MatchStartRunFramesUtils"/>), which is the one
/// position a capture measures the dedicated host writing it. So this handler
/// has nothing of its own to write: it recognises the record so that the relay
/// it needs is a decision rather than the default an unnamed gameplay record
/// falls into, and passes it on.
/// </para>
/// <para>
/// Its identifiers are the same four the report uses on its own stream — the
/// presence record's two are <c>0x25c</c> under both generations, where the
/// report's are <c>0x2de</c> — so a client that moves to the next round's set
/// moves this record with it.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerPresenceHandler(ILogger<PlayerPresenceHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        logger.LogDebug(
            "UDP {LocalPort}: presence record 0x{MessageType:x4} from {RemoteAddress} carries slot {Slot:x2} "
            + "({BodyLength} byte); its answer rides the round's opening run",
            context.LocalPort,
            context.Message.Type,
            context.Remote,
            context.Message.Body.Length > 0 ? context.Message.Body[0] : 0,
            context.Message.Body.Length);

        // Relayed rather than broadcast: the record is the peer's own, and the
        // room's other clients need the presence the same way they need the report.
        var relay = context.Relay ?? context.Broadcast;
        await relay(context.Message.Type, context.Message.Body, context.Message.Flags);
    }
}
