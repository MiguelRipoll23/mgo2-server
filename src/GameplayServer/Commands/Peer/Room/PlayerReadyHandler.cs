using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

/// <summary>
/// Takes a client's briefing ready report, records it against the player that
/// sent it, publishes the room's ready set, and hands the report to the round
/// opener, which answers the briefing before it opens a round: the first report
/// names the set the briefing is written under and the next, on the other set,
/// opens the round — or the room's own briefing time does, when no second report
/// arrives (<see cref="MatchStartRunService.Open"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The report and the host's reply are one record read in two directions.</b>
/// The body begins <c>e1</c> from the client and <c>e0</c> from the host, and
/// the byte after that opcode is the client's own — <c>e1 14</c> is answered
/// <c>e0 14 …</c>, <c>e1 01</c> is answered <c>e0 01 …</c>. That echo is the
/// whole of what the reply is read for. What the byte <em>means</em> is
/// **[U]**, and the measurement says more about it than "an identifier": the
/// record keeps carrying values the host rewrites through the round, under other
/// fourth bytes. It is a per-player value the client reports and the host
/// confirms, not a round number, and this server carries it rather than deriving
/// it.
/// </para>
/// <para>
/// <b>The publication is the answer, and it is why the round does not have to
/// open yet.</b> A client that readied first in a room of six is answered by the
/// host's publication of the set — its own state in it — and not by the round,
/// which is still minutes away
/// (<c>docs/protocol/BRIEFING_FLOW.md</c>: the report at t+55.8 s, a second
/// player's at t+112.6 s, the round at t+193.6 s). A host that answered only by
/// opening the round would leave five of its six clients re-reporting for two
/// minutes, which is the shape of the failure this project already made once on
/// a clock.
/// </para>
/// <para>
/// The identifier alternates with the round's own set (<c>0x1ade</c>, then
/// <c>0x12de</c> a <c>0x800</c> bit along), so it is recognised from the body
/// rather than bound to one value: the readiness opcode in the first byte, at
/// the length every capture sends. The identifier also carries the round's other
/// payloads, which belong on the relay and not to a round; a record on it that
/// is not a report is still broadcast to the room's other peers.
/// </para>
/// </remarks>
/// <param name="ready">The room's briefing states, which this report is recorded in.</param>
/// <param name="matchStart">Writer of the round a room's last report opens.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerReadyHandler(
    PlayerReadyService ready,
    MatchStartRunService matchStart,
    ILogger<PlayerReadyHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        var body = context.Message.Body;
        if (!MatchStartBodiesUtils.IsReadinessReport(body))
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

            // Relayed rather than broadcast: the record is the peer's own and this
            // host only passes it on, which is what keeps the replay console from
            // reading it as a record this server builds.
            var relay = context.Relay ?? context.Broadcast;
            await relay(context.Message.Type, body, context.Message.Flags);
            return;
        }

        var player = context.Remote.ToString();
        var reported = body[MatchStartBodiesUtils.ReadinessReportValueIndex];
        ready.Note(player, reported);

        // The presence record the client sends in the same datagram as its report
        // is answered here as well as inside the round's run. Both captures write
        // the answer beside the report it follows — within half a second of it,
        // and again as the set changes — and its first byte is the room's own
        // reporters, so a client whose presence was heard is told so while the
        // briefing still waits rather than only when the round opens
        // (docs/protocol/BRIEFING_FLOW.md §1).
        var generation = MatchRecordGenerationUtils.OfReadinessReport(context.Message.Type);
        await context.Send(
            generation.PlayerPresence,
            MatchStartBodiesUtils.PlayerPresenceAnswer(ready.PresenceMask),
            MatchStartBodiesUtils.PlayerPresenceAnswerClass);

        // The publication goes out on every report and not only on the first: it
        // is the host's answer to the report, and a client whose report changed
        // nothing is still waiting to be answered.
        await PublishAsync(context, generation);

        if (!matchStart.Open(context.Message.Type))
        {
            logger.LogDebug(
                "UDP {LocalPort}: {RemoteAddress} is ready with {Value}; {ReadyCount} of {PlayerCount} "
                + "player(s) in the room are ready, so the round stays closed",
                context.LocalPort,
                context.Remote,
                reported,
                ready.ReadyCount,
                ready.PlayerCount);
        }
    }

    /// <summary>
    /// Writes the room's ready set: the reporting peer gets it as one frame, and
    /// the rest of the room gets the same records so that every client can see
    /// which of the others is ready.
    /// </summary>
    /// <param name="context">Context the report arrived through.</param>
    /// <param name="generation">
    /// Identifier set the report arrived on, which is the generation that client is
    /// on: a round's payload record is the same record in both directions.
    /// </param>
    private async Task PublishAsync(PeerContext context, MatchRecordGenerationUtils generation)
    {
        var publication = ready.BuildPublication(generation);
        if (publication.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "UDP {LocalPort}: publishing {RecordCount} ready state(s) to {RemoteAddress} and the room",
            context.LocalPort,
            publication.Count,
            context.Remote);

        await RosterRunSendUtils.SendAsync(context, publication);

        foreach (var record in publication)
        {
            await context.Broadcast(record.Type, record.Body, record.Ordinal);
        }
    }
}
