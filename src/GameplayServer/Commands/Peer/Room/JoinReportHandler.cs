using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

/// <summary>
/// Takes the record a client sends when it has joined the room and loaded its
/// map — the **join report** — records it against the player that sent it,
/// publishes the room's states, and hands it to the round opener, which answers
/// the briefing before it opens a round: the first report names the set the
/// briefing is written under and the next, on the other set, opens it once the
/// room has readied (<see cref="MatchStartRunService.OnJoinReport"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a join and not a readiness.</b> Nothing here is the player pressing
/// ready: that record is <c>1f &lt;slot&gt; 01</c> on the operations stream and it
/// is taken elsewhere (<see cref="Gameplay.OperationsStreamHandler"/>,
/// <see cref="Rooms.BriefingStateService.NoteBriefingReady"/>). A room gated on
/// this report's own record would open its round on the wrong packet — a client's
/// report and its ready are up to half a minute apart, and in the reference's
/// second briefing the ready comes *first* (`mgo2-game-joined-when-red-team-then-briefing`
/// reports at t+57.656 and readies at t+112.750, and reports at t+870.222 behind a
/// ready at t+844.858).
/// </para>
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
/// <b>The publication, and the report's own bare answer, are why the round does
/// not have to open yet.</b> Only the report that opens the round is answered by
/// the round's marker; every other one draws the same record with the answer bit
/// set and no body, which is what the reference host writes while its briefing
/// waits (<c>docs/analysis/connection-not-stable-disconnect.md</c>). A client
/// that readied first in a room of six is answered by the host's publication of
/// the set — its own state in it — and not by the round,
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
/// rather than bound to one value: the join opcode in the first byte, at
/// the length every capture sends. The identifier also carries the round's other
/// payloads, which belong on the relay and not to a round; a record on it that
/// is not a report is still broadcast to the room's other peers.
/// </para>
/// </remarks>
/// <param name="briefingStates">The room's briefing states, which this report is recorded in.</param>
/// <param name="matchStart">Writer of the round a room's last report opens.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class JoinReportHandler(
    BriefingStateService briefingStates,
    MatchStartRunService matchStart,
    ILogger<JoinReportHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        var body = context.Message.Body;
        if (!MatchStartBodiesUtils.IsJoinReport(body))
        {
            // The same identifier carries the round's other payloads, which the
            // dispatcher relays to the room's other peers. A report is not one of
            // them: it is the reporting client's own and goes no further.
            logger.LogDebug(
                "UDP {LocalPort}: 0x{MessageType:x4} of {BodyLength} bytes from {RemoteAddress} is not a "
                + "join report; left to the relay",
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
        var reported = body[MatchStartBodiesUtils.JoinReportValueIndex];
        briefingStates.NoteJoinReport(player, reported);

        // The join state record the client sends in the same datagram as its report
        // is answered here as well as inside the round's run. Both captures write
        // the answer beside the report it follows — within half a second of it,
        // and again as the set changes — and its first byte is the room's own
        // reporters, so a client whose join was heard is told so while the
        // briefing still waits rather than only when the round opens
        // (docs/protocol/BRIEFING_FLOW.md §1).
        var generation = MatchRecordGenerationUtils.OfJoinReport(context.Message.Type);
        await context.Send(
            generation.JoinState,
            MatchStartBodiesUtils.JoinStateAnswer(briefingStates.JoinedMask),
            MatchStartBodiesUtils.JoinStateAnswerClass);

        // The publication goes out on every report and not only on the first: it
        // is the host's answer to the report, and a client whose report changed
        // nothing is still waiting to be answered.
        await PublishAsync(context, generation);

        if (!matchStart.OnJoinReport(context.Message.Type))
        {
            // The report that does not open the round is still answered on its
            // own record, and the marker is only the answer of the report that
            // does open it. The reference host writes the bare answer beside
            // its publication while the briefing waits: in
            // `mgo2-game-joined-when-red-team-then-briefing` the client reports
            // `0x12de e1 05` at t+57.7 and the host writes `0x52de` at t+60.3 in
            // the same datagram as its `0x12de e0 05`, with the round still
            // forty seconds out (`docs/protocol/DATA_PHASE_DELIVERY.md` §2).
            //
            // Withholding it is what leaves the client waiting on itself: a
            // live solo briefing whose report was answered only by the
            // publication (`myserver17.pcapng`, t+68.5) had the client
            // retransmit the same record as `0x92de e1 01` eight times over
            // 24 s — the one capture in the set in which that record appears —
            // and give up at its own deadline 64 s after reporting, which is
            // the `CONNECTION NOT STABLE` modal
            // (`docs/analysis/connection-not-stable-disconnect.md`).
            //
            // Only the record without the payload-class bit is answered here,
            // because that is the record the capture answers: the bare
            // `0x52de` / `0x5ade` carries no body, and a retransmission under
            // `0x8000` stops as soon as this one is heard.
            if ((context.Message.Type & PeerAcknowledgementUtils.PayloadClassBit) == 0)
            {
                var answer = PeerAcknowledgementUtils.AcknowledgementOf(context.Message);
                await context.Send(answer.Type, answer.Body, answer.Flags);
            }

            logger.LogDebug(
                "UDP {LocalPort}: {RemoteAddress} has joined with {Value}; {JoinedCount} of {PlayerCount} "
                + "player(s) in the room have joined, and the round waits for the room's readies",
                context.LocalPort,
                context.Remote,
                reported,
                briefingStates.JoinedCount,
                briefingStates.PlayerCount);
        }
    }

    /// <summary>
    /// Writes the room's join states: the reporting peer gets them as one frame, and
    /// the rest of the room gets the same records, so every client can see who has
    /// joined. It is not the ready set — that one is
    /// <see cref="MatchRecords.BriefingReadyPublishService"/>'s, out of the ready
    /// records.
    /// </summary>
    /// <param name="context">Context the report arrived through.</param>
    /// <param name="generation">
    /// Identifier set the report arrived on, which is the generation that client is
    /// on: a round's payload record is the same record in both directions.
    /// </param>
    private async Task PublishAsync(PeerContext context, MatchRecordGenerationUtils generation)
    {
        var publication = briefingStates.BuildPublication(generation);
        if (publication.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "UDP {LocalPort}: publishing {RecordCount} join state(s) to {RemoteAddress} and the room",
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
