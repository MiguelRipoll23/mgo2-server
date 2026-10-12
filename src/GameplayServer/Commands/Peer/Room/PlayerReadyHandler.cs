using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

/// <summary>
/// Recognises the record in which a client reports that it has loaded and is ready
/// to play, and opens the round the recorded host opens on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The round opens on this report and not on a clock.</b> Measured over all
/// three captures, every round begins within 0.6–5.5 s of one of these reports and
/// no round begins without one. The report is handed to
/// <see cref="MatchStartRunService"/>, which writes the round's opening marker,
/// the match-start run and the run's four repeats — and the run's payload echoes
/// the byte this report carried, which is why nothing here can be written before
/// the report arrives.
/// </para>
/// <list type="bullet">
/// <item>game1 — <c>0x1ade</c> <c>e1 14</c> at t+44.76 s, round open at t+46.42 s.</item>
/// <item>game1 — <c>0x12de</c> <c>e1 14</c> at t+190.39 s, round open at t+194.04 s.</item>
/// <item>game2 — <c>0x12de</c> <c>e1 01</c> at t+56.72 s, round open at t+57.36 s.</item>
/// <item>game2 — <c>0x1ade</c> <c>e1 01</c> at t+107.98 s, round open at t+109.12 s.</item>
/// <item>game2 — <c>0x12de</c> <c>e1 01</c> at t+223.63 s, round open at t+224.62 s.</item>
/// </list>
/// <para>
/// The report and the host's reply are one record read in two directions: the
/// body begins <c>e1</c> from the client and <c>e0</c> from the host, and the byte
/// after that opcode is echoed back unchanged — <c>e1 14</c> is answered
/// <c>e0 14 …</c>, <c>e1 01</c> is answered <c>e0 01 …</c>. That echo is the whole
/// of what the reply is read for. What the byte <em>means</em> is **[U]**, and the
/// measurement says more about it than "an identifier": the record keeps carrying
/// values the host rewrites through the round, under other fourth bytes — game1
/// writes <c>e0 14</c> under fourth byte 0 and <c>e0 0f</c> under fourth byte 1,
/// then <c>e0 0b c3 cf 00 00</c> under fourth byte 7, and game3 writes
/// <c>e0 05</c> and <c>e0 0f</c> the same way. It is a per-fourth-byte value the
/// client reports and the host confirms, not a round number, and this server
/// carries it rather than deriving it.
/// </para>
/// <para>
/// The identifier alternates with the round's own identifier set
/// (<c>0x1ade</c>, then <c>0x12de</c> a <c>0x800</c> bit along), so it is
/// recognised from the body rather than bound to one value: the readiness opcode
/// in the first byte, at the length every capture sends. The live client asked
/// twenty-three times over two minutes, alternating <c>0x9ade</c> and
/// <c>0x1ade</c>; all of them are answered, and the ones that arrive while the
/// round they asked for is still going out are recognised as that same round
/// rather than opening another.
/// </para>
/// <para>
/// That identifier also carries the round's other payloads, which belong on the
/// relay and not to a round. A record on it that is not a report is still
/// broadcast to the room's other peers, so the identifier keeps carrying the
/// traffic the captures show it carrying.
/// </para>
/// </remarks>
/// <param name="matchStart">Writer of the round a report opens.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerReadyHandler(
    MatchStartRunService matchStart,
    ILogger<PlayerReadyHandler> logger) : IPeerCommandHandler
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

            // Relayed rather than broadcast: the record is the peer's own and this
            // host only passes it on, which is what keeps the replay console from
            // reading it as a record this server builds.
            var relay = context.Relay ?? context.Broadcast;
            return relay(context.Message.Type, body, context.Message.Flags);
        }

        var reported = body[MatchRecordBodiesUtils.ReadinessReportValueIndex];
        matchStart.Open(context, reported);
        return Task.CompletedTask;
    }
}
