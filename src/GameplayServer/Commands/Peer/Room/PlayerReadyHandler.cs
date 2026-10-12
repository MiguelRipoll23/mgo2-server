using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

/// <summary>
/// Handles the record in which a client reports that it has loaded and is ready
/// to play, and latches it for the round the room opens on its own clock.
/// </summary>
/// <remarks>
/// <para>
/// <b>A client says it is ready, and the recorded host opens the round around
/// it.</b> Measured over all three captures, every round begins within 0.6–5.5 s
/// of one of these reports and no round begins without one — the round follows
/// the report on the capture's clock. This server opens the round on its own
/// clock instead (see <see cref="MatchRoundService"/>), so the report no longer
/// gates it; what follows is still what the report is measured to be, and the
/// two facts it carries are what the round takes from it:
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
/// varies with it.
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
/// <b>The identifier is also which set the round plays under.</b> It is the
/// round's own payload record, and the client carries its generation from the
/// rounds it has already played rather than resetting it with the match: game1's
/// first round reports <c>0x1ade</c> and game2's first reports <c>0x12de</c>. So
/// the handler passes the identifier on with the byte, and the room's first round
/// plays under the generation the report names when one has arrived, instead of
/// assuming <see cref="MatchRecordGenerationUtils.First"/> — see
/// <see cref="MatchRecordGenerationUtils.OfReadinessReport"/>.
/// </para>
/// <para>
/// The round itself is opened by <see cref="MatchRoundService"/> on the room's
/// clock, and <see cref="MatchRecordService"/> writes its marker and run, and
/// the run again four times inside the beat that follows. The report is read for
/// the two things it carries that the room cannot derive — the identifier set
/// the round plays under, and the byte the run's payload echoes
/// (<see cref="PlayerReadyService"/>) — and does not gate the round: the marker
/// is written whether or not one arrived. The dispatcher still leaves the report
/// unanswered in the pass that received it, because the round's own marker is its
/// answer and writing both puts two markers on the wire.
/// </para>
/// </remarks>
/// <param name="ready">Reports peers have sent that open their rounds.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerReadyHandler(
    PlayerReadyService ready,
    ILogger<PlayerReadyHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(PeerContext context)
    {
        var body = context.Message.Body;
        if (!MatchRecordBodiesUtils.IsReadinessReport(body))
        {
            // The same identifier carries the round's other payloads, which the
            // dispatcher relays to the room's other peers. Recording one of those
            // as a readiness report would open a round the client did not ask for.
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
        var first = ready.Report(context.Session, reported, context.Message.Type);

        logger.LogInformation(
            "UDP {LocalPort}: {RemoteAddress} reported itself ready (0x{MessageType:x4} carrying "
            + "0x{Reported:x2}); {Disposition}",
            context.LocalPort,
            context.Remote,
            context.Message.Type,
            reported,
            first ? "recorded" : "already reported, the round is still owed");

        return Task.CompletedTask;
    }
}