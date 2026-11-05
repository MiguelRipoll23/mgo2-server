using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Gameplay;

/// <summary>
/// Reads the records a client writes on the operations stream — the briefing ready
/// state it reports and the kill feed — and passes every record on to the room.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two records, told apart by their first byte.</b> The stream carries the round
/// clock, the state block and this host's own insertions as well, and every one of
/// them travels under the same identifier pair, so the identifier cannot separate
/// them: the opcode and the length do. A body that is neither of the two this
/// handler knows is relayed exactly as it arrived.
/// </para>
/// <para>
/// <b>The kill feed says who killed whom.</b> The replay parser maps it as opcode
/// <c>0x09</c> of the operations stream — a three-byte body
/// <c>[0x09, killerSlot, victimSlot]</c> — and every captured kill feed travels
/// host → client, because the host is the side that knows
/// (<c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>, and
/// <c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §3). The slot the feed names as the
/// killer is owed the kill and the victim the death
/// (<see cref="MatchScoreService.NoteKill"/>), which queues the two scoreboard
/// deltas that are published behind it.
/// </para>
/// <para>
/// <b>The ready record is the player's briefing state, and it opens nothing.</b> A
/// client writes <c>1f &lt;slot&gt; 01</c> on this stream when the player in front of
/// it changes its briefing state to ready (<see cref="BriefingReadyBodiesUtils"/>),
/// and the record is kept as the room's ready set for the countdown. **A round does
/// not open on it**: the round opens on the report the client sends once it has left
/// its briefing screen for the weapon select and its spawn
/// (<see cref="MatchStartRunService.OnJoinReport"/>), and the reference capture of
/// that flow holds no ready record at all
/// (`working-server-4-sessions.pcapng`).
/// </para>
/// <para>
/// <b>The record is relayed rather than consumed.</b> The other clients in the room
/// are told the same ready the host was — which is also what its own countdown
/// carries as the room's ready set — and a host that swallowed the record would
/// show one client every event but the last. **[U]** whether the reference relays
/// this record to the room's other clients is not visible in a capture of one
/// flow; what is measured is that the room's ready set is published to every peer
/// on the countdown.
/// </para>
/// </remarks>
/// <param name="ready">The room's briefing states, which a ready record is recorded in.</param>
/// <param name="score">The host's own score, which the kill feed feeds.</param>
/// <param name="publisher">Publisher of the scoreboard a kill changes.</param>
/// <param name="matchStart">The room's round, which a kill can end on its tickets.</param>
/// <param name="results">The round's result, which the ticket end is read from.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class OperationsStreamHandler(
    BriefingStateService briefingStates,
    MatchScoreService score,
    ScoreboardPublishService publisher,
    MatchStartRunService matchStart,
    MatchResultService results,
    ILogger<OperationsStreamHandler> logger) : IPeerCommandHandler
{
    /// <summary>Opcode the operations stream uses for its kill feed.</summary>
    public const byte KillFeedOpcode = (byte)OperationsOpcode.KillFeed;

    /// <summary>Length of a kill-feed body: the opcode and the two slots.</summary>
    public const int KillFeedLength = 3;

    /// <summary>
    /// Whether a body is a kill-feed record: the opcode followed by the killer's
    /// slot and the victim's.
    /// </summary>
    /// <param name="body">Body of an inbound operations record.</param>
    /// <returns><c>true</c> when the body says who killed whom.</returns>
    public static bool IsKillFeed(ReadOnlySpan<byte> body) =>
        body.Length == KillFeedLength && body[0] == KillFeedOpcode;

    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        var body = context.Message.Body;
        if (IsKillFeed(body))
        {
            var killer = body[1];
            var victim = body[2];
            logger.LogDebug(
                "UDP {LocalPort}: {RemoteAddress} reports a kill feed entry: slot {KillerSlot} killed "
                + "slot {VictimSlot}",
                context.LocalPort,
                context.Remote,
                killer,
                victim);

            score.NoteKill(killer, victim);
            await publisher.PublishAsync(context);

            // A kill is one of the things that can reach a rule's ticket limit,
            // so the round is checked here rather than on the clock that would
            // otherwise be its only end.
            await EndOnTicketsAsync(context);
        }
        else if (BriefingReadyBodiesUtils.IsBriefingReady(body))
        {
            var slot = BriefingReadyBodiesUtils.SlotOf(body);
            var state = BriefingReadyBodiesUtils.StateOf(body);
            logger.LogDebug(
                "UDP {LocalPort}: {RemoteAddress} reports briefing state {State} for slot {Slot}",
                context.LocalPort,
                context.Remote,
                state,
                slot);

            briefingStates.NoteBriefingReady(context.Remote.ToString(), slot, state);
        }
        else
        {
            // The stream's clock, state block and this host's own insertions share
            // the identifier, and so does a client's join report. None of them
            // is a kill or a ready, and all of them are relayed unchanged.
            logger.LogDebug(
                "UDP {LocalPort}: 0x{MessageType:x4} of {BodyLength} bytes from {RemoteAddress} is neither "
                + "a kill feed nor a briefing ready; relayed unchanged",
                context.LocalPort,
                context.Message.Type,
                body.Length,
                context.Remote);
        }

        var relay = context.Relay ?? context.Broadcast;
        await relay(context.Message.Type, body, context.Message.Flags);
    }

    /// <summary>
    /// Ends the round when the kill just recorded has taken a side to its rule's
    /// limit.
    /// </summary>
    /// <param name="context">Context the kill arrived through.</param>
    private async Task EndOnTicketsAsync(PeerContext context)
    {
        if (await results.TicketsReachedAsync() is not { } reached)
        {
            return;
        }

        logger.LogInformation(
            "UDP {LocalPort}: the kill feed has taken team {Team} to its round's {Tickets}-kill limit",
            context.LocalPort,
            reached.Team,
            reached.Tickets);
        matchStart.End(
            $"team {reached.Team} reached the round's {reached.Tickets}-kill limit",
            reached.ResultCode);
    }
}
