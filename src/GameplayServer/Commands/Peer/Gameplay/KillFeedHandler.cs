using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Gameplay;

/// <summary>
/// Reads the operations stream's kill feed, records the kill and the death it
/// names, and passes the record on to the room.
/// </summary>
/// <remarks>
/// <para>
/// <b>The feed is the one record on this channel that says who killed whom.</b>
/// The replay parser maps it as opcode <c>0x09</c> of the operations stream — a
/// three-byte body <c>[0x09, killerSlot, victimSlot]</c> — and every captured
/// kill feed travels host → client, because the host is the side that knows
/// (<c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>, and
/// <c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §3). The same stream carries the
/// round clock and the state block, so the identifier cannot tell the feed from
/// them: the opcode and the length do, and a record that is neither is relayed
/// exactly as it arrived.
/// </para>
/// <para>
/// <b>A kill is a kill and a death, and the round's score follows.</b> The slot
/// the feed names as the killer is owed the kill and the victim the death
/// (<see cref="MatchScoreService.NoteKill"/>), which queues the two scoreboard
/// deltas that are published behind it. The record is relayed rather than
/// consumed: the other clients in the room read the feed themselves, and a host
/// that swallowed it would show one client every kill but the last.
/// </para>
/// </remarks>
/// <param name="score">The host's own score, which this feeds.</param>
/// <param name="publisher">Publisher of the scoreboard the kill changes.</param>
/// <param name="matchStart">The room's round, which a kill can end on its tickets.</param>
/// <param name="results">The round's result, which the ticket end is read from.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class KillFeedHandler(
    MatchScoreService score,
    ScoreboardPublishService publisher,
    MatchStartRunService matchStart,
    MatchResultService results,
    ILogger<KillFeedHandler> logger) : IPeerCommandHandler
{
    /// <summary>Opcode the operations stream uses for its kill feed.</summary>
    public const byte KillFeedOpcode = 0x09;

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
        else
        {
            // The stream's clock, state block and this host's own insertions share
            // the identifier, and so does a client's briefing report. None of them
            // is a kill, and all of them are relayed unchanged.
            logger.LogDebug(
                "UDP {LocalPort}: 0x{MessageType:x4} of {BodyLength} bytes from {RemoteAddress} is not a "
                + "kill feed; relayed unchanged",
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
