using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

/// <summary>
/// Recognises the one-byte record a client sends beside its readiness report,
/// which carries the same byte the report carries.
/// </summary>
/// <remarks>
/// <para>
/// <b>The report never arrives alone.</b> In five of five reports across the two
/// reference captures the client puts a second record in the same datagram,
/// immediately before the report, on an identifier that splits the same way the
/// report's does and carrying the report's own byte
/// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2):
/// </para>
/// <code>
/// 0x0a5c len 1 class 4 14   .   0x1ade len 2 class 0 e1 14
/// 0x025c len 1 class 4 01   .   0x12de len 2 class 0 e1 01
/// </code>
/// <para>
/// The host answers it in four bytes under class 6 — <c>02 00 00 00</c> in one
/// capture, six other bodies in the other — and that answer rides inside the
/// round's opening run (<see cref="MatchStartRunFramesUtils"/>), which is the one
/// position a capture measures. So this handler has nothing of its own to write:
/// it recognises the record so that the relay it needs is a decision rather than
/// the default an unnamed gameplay record falls into, and passes it on.
/// </para>
/// <para>
/// Its identifiers are the same four the report uses on its own stream — the
/// companion's two are <c>0x25c</c> under both generations, where the report's
/// are <c>0x2de</c> — so a client that moves to the next round's set moves this
/// record with it.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerReadyStateHandler(ILogger<PlayerReadyStateHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        logger.LogDebug(
            "UDP {LocalPort}: readiness state 0x{MessageType:x4} from {RemoteAddress} carries {Value:x2} "
            + "({BodyLength} byte); its answer rides the round's opening run",
            context.LocalPort,
            context.Message.Type,
            context.Remote,
            context.Message.Body.Length > 0 ? context.Message.Body[0] : 0,
            context.Message.Body.Length);

        // Relayed rather than broadcast: the record is the peer's own, and the
        // room's other clients need the state the same way they need the report.
        var relay = context.Relay ?? context.Broadcast;
        await relay(context.Message.Type, context.Message.Body, context.Message.Flags);
    }
}
