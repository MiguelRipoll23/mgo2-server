using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

/// <summary>
/// Recognises the record in which a client reports that it has loaded and is ready
/// to play, and leaves it unanswered.
/// </summary>
/// <remarks>
/// <para>
/// <b>The recorded host opens its round around this report; this server opens no
/// round at all.</b> Measured over all three captures, every round begins within
/// 0.6–5.5 s of one of these reports and no round begins without one. The report
/// used to be latched for the identifier set its round played under and the byte
/// the round's payload echoed; with the round start never written
/// (<see cref="MatchRecordService"/>) neither fact has a reader, so the report is
/// recognised, logged and dropped. A client that reports ready therefore stays in
/// its ready state and re-reports — the live client asked thirty-eight times over
/// one session — and that is the deliberate shape: the round start is the host's
/// answer to this record, and an answer this server never sends cannot be sent
/// early instead.
/// </para>
/// <list type="bullet">
/// <item>game1 — <c>0x1ade</c> <c>e1 14</c> at t+44.76 s, round open at t+46.42 s.</item>
/// <item>game1 — <c>0x12de</c> <c>e1 14</c> at t+190.39 s, round open at t+194.04 s.</item>
/// <item>game2 — <c>0x12de</c> <c>e1 01</c> at t+56.72 s, round open at t+62.18 s.</item>
/// <item>game2 — <c>0x1ade</c> <c>e1 01</c> at t+107.98 s, round open at t+109.12 s.</item>
/// <item>game2 — <c>0x12de</c> <c>e1 01</c> at t+223.63 s, round open at t+224.62 s.</item>
/// </list>
/// <para>
/// The report and the host's reply are one record read in two directions: the
/// body begins <c>e1</c> from the client and <c>e0</c> from the host, and the byte
/// after that opcode is echoed back unchanged — <c>e1 14</c> is answered
/// <c>e0 14 …</c>, <c>e1 01</c> is answered <c>e0 01 …</c>. What the byte means
/// is **[U]**; it is carried rather than interpreted, because nothing measured
/// varies with it. Nothing echoes it now, so it is read only to say in the log
/// which report arrived.
/// </para>
/// <para>
/// The identifier alternates with the round's own identifier set
/// (<c>0x1ade</c>, then <c>0x12de</c> a <c>0x800</c> bit along), so it is
/// recognised from the body rather than bound to one value: the readiness opcode
/// in the first byte, at the length every capture sends. The live client asked
/// twenty-three times over two minutes, alternating <c>0x9ade</c> and
/// <c>0x1ade</c>, and each one was logged as a record with no handler and relayed
/// to a room of one — which is a game that stays in its ready state and a client
/// that keeps asking.
/// </para>
/// <para>
/// That identifier also carries the round's other payloads, which belong on the
/// relay and not to a round. A record on it that is not a report is still
/// broadcast to the room's other peers, so the identifier keeps carrying the
/// traffic the captures show it carrying.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerReadyHandler(ILogger<PlayerReadyHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(PeerContext context)
    {
        var body = context.Message.Body;
        if (!MatchRecordBodiesUtils.IsReadinessReport(body))
        {
            // The same identifier carries the round's other payloads, which the
            // dispatcher relays to the room's other peers. A report is not one of
            // them: it is the reporting client's own and goes no further.
            logger.LogDebug(
                "UDP {LocalPort}: 0x{MessageType:x4} of {BodyLength} bytes from {RemoteAddress} is not a "
                + "readiness report; left to the relay",
                context.LocalPort,
                context.Message.Type,
                body.Length,
                context.Remote);
            return context.Broadcast(context.Message.Type, body, context.Message.Flags);
        }

        var reported = body[MatchRecordBodiesUtils.ReadinessReportValueIndex];

        logger.LogInformation(
            "UDP {LocalPort}: {RemoteAddress} reported itself ready (0x{MessageType:x4} carrying "
            + "0x{Reported:x2}); no round is opened and the report is not answered, so the client will ask again",
            context.LocalPort,
            context.Remote,
            context.Message.Type,
            reported);

        return Task.CompletedTask;
    }
}
