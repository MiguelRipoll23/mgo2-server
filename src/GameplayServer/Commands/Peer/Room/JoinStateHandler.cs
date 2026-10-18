using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

/// <summary>
/// Recognises the one-byte join state record a client sends beside its join
/// report, which carries the player's own lobby slot.
/// </summary>
/// <remarks>
/// <para>
/// <b>One join, two records, one vocabulary.</b> The report is <c>0x1ade e1
/// &lt;slot&gt;</c> and this is <c>0x0a5c &lt;slot&gt;</c>, sent in the same
/// datagram immediately before it; both carry that player's own lobby slot, which
/// is the byte the round's run payload echoes back, and neither is the player
/// pressing ready (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2,
/// <c>BRIEFING_FLOW.md</c> §1):
/// </para>
/// <code>
/// 0x0a5c len 1 class 4 14   .   0x1ade len 2 class 0 e1 14
/// 0x025c len 1 class 4 01   .   0x12de len 2 class 0 e1 01
/// </code>
/// <para>
/// <b>The host answers it with the room's joined players.</b> Four bytes under class 6
/// whose first is a bit set that grows as the room's players join:
/// <c>mgo2-game-started-on-briefing</c> answers <c>02</c> and then <c>06</c>,
/// <c>0e</c> and <c>1e</c> as its other players report, and
/// <c>mgo2-game-joined-when-red-team-then-briefing</c> answers <c>2a</c> — the
/// slots its reporting players hold. That answer is written by the report that
/// arrives in the same datagram as this record (<see cref="JoinReportHandler"/>)
/// — beside the report and not only inside the round's opening run
/// (<see cref="MatchStartRunFramesUtils"/>), so a client whose join was heard
/// is told so while the briefing still waits. This handler recognises the record
/// so that the relay it needs is a decision rather than the default an unnamed
/// gameplay record falls into, and passes it on.
/// </para>
/// <para>
/// Its identifiers are the same four shapes the report uses on its own stream —
/// this record's low eleven bits are <c>0x25c</c> under both generations, where
/// the report's are <c>0x2de</c> — so a client that moves to the next round's set
/// moves this record with it.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this handler.</param>
public sealed class JoinStateHandler(ILogger<JoinStateHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        logger.LogDebug(
            "UDP {LocalPort}: join state record 0x{MessageType:x4} from {RemoteAddress} carries slot {Slot:x2} "
            + "({BodyLength} byte); its answer rides the round's opening run",
            context.LocalPort,
            context.Message.Type,
            context.Remote,
            context.Message.Body.Length > 0 ? context.Message.Body[0] : 0,
            context.Message.Body.Length);

        // Relayed rather than broadcast: the record is the peer's own, and the
        // room's other clients need the join the same way they need the report.
        var relay = context.Relay ?? context.Broadcast;
        await relay(context.Message.Type, context.Message.Body, context.Message.Flags);
    }
}
