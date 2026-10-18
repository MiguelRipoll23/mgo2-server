using Mgo2Server.GameplayServer.Gameplay;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Gameplay;

/// <summary>
/// Takes a player's vitals record — health, then stamina — keeps the value,
/// counts the death when health reaches zero, and passes the record on to the
/// room's other peers as the relay did before this handler existed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The record is the peer's own and the relay stays its fate.</b> A client
/// reports its health under <c>0x0080</c> and a second player's under
/// <c>0x0880</c>; the pair is measured in <c>docs/protocol/UDP_GAME_CAPTURE.md</c>
/// §5, where health is the byte that falls to 0 on death and rises back to 250,
/// and the second byte is the one that never moves. Binding it makes the relay a
/// decision rather than the default — the same thing the join report's
/// handlers do — and what the decision is here is unchanged: the record is the
/// player's and the room's other clients are the ones it is for, since a client
/// sees every other player's health and its own on its own.
/// </para>
/// <para>
/// <b>A death is the one score change the host reads for itself.</b> Nothing on
/// this channel names a killer, but the victim is measured at both ends — health
/// <c>0</c> is death and a return to <c>250</c> is the next life — so the host
/// counts the death here and leaves the kill to the kill feed
/// (<see cref="OperationsStreamHandler"/>). The transition and not the value is what is
/// counted: a player lies at zero for many records while it is dead, and counting
/// each one would make a single death read as dozens. The scoreboard the change
/// queues is published behind it.
/// </para>
/// <para>
/// <b>The record is also the beat's vitals onset.</b> The round's first vitals is
/// where the beat's vitals record starts riding
/// (<see cref="MatchRoundStateService.NoteVitalsArrived"/>), which is the one
/// phase of the beat the captures' own offset can be replaced with a record.
/// </para>
/// <para>
/// <b>Nothing is answered.</b> The capture's own reading is that no answer
/// follows these records, and a host that replied to a tick would spend an
/// outbound sequence a frame on a stream that runs at 30 records a second.
/// </para>
/// </remarks>
/// <param name="vitals">The room's per-player values this record is kept in.</param>
/// <param name="roundState">The room's round, which the beat's vitals onset is gated on.</param>
/// <param name="score">The host's own score, which the death is counted in.</param>
/// <param name="publisher">Publisher of the scoreboard the death changes.</param>
/// <param name="roster">Roster of the room, which names the player's slot.</param>
/// <param name="matchStart">The room's round, which a death can end on its tickets.</param>
/// <param name="results">The round's result, which the ticket end is read from.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerVitalsHandler(
    PlayerVitalsService vitals,
    MatchRoundStateService roundState,
    MatchScoreService score,
    ScoreboardPublishService publisher,
    RoomRosterService roster,
    MatchStartRunService matchStart,
    MatchResultService results,
    ILogger<PlayerVitalsHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        var player = context.Remote.ToString();
        var reported = PlayerVitalsRecordUtility.Parse(context.Message.Body);

        if (reported is not { } value)
        {
            // The identifier carries the character's vitals and, in the same
            // range, the rest of its tick stream. A body that is not the two
            // bytes this record is reads as no values rather than as a guess, and
            // is still relayed.
            logger.LogDebug(
                "UDP {LocalPort}: 0x{MessageType:x4} of {BodyLength} bytes from {RemoteAddress} is not a vitals record; relayed unchanged",
                context.LocalPort,
                context.Message.Type,
                context.Message.Body.Length,
                context.Remote);
            await RelayAsync(context);
            return;
        }

        var alive = vitals.TryGet(player, out var previous) && !previous.IsDead;
        vitals.Note(player, value);
        roundState.NoteVitalsArrived();
        logger.LogDebug(
            "UDP {LocalPort}: {RemoteAddress} reports {Health} health and {Stamina} stamina under 0x{MessageType:x4}",
            context.LocalPort,
            context.Remote,
            value.Health,
            value.Stamina,
            context.Message.Type);

        if (value.IsDead && alive)
        {
            await NoteDeathAsync(context, player);
        }

        await RelayAsync(context);
    }

    /// <summary>
    /// Counts the death the record just reported, on the slot the room files the
    /// player in, and publishes the scoreboard it changed.
    /// </summary>
    /// <param name="context">Context the vitals arrived through.</param>
    /// <param name="player">Endpoint the player is filed under.</param>
    private async Task NoteDeathAsync(PeerContext context, string player)
    {
        var member = roster.Find(player);
        if (member is null)
        {
            logger.LogWarning(
                "UDP {LocalPort}: {RemoteAddress} died but is not on the room roster, so the death is counted "
                + "for no slot",
                context.LocalPort,
                context.Remote);
            return;
        }

        score.NoteDeath(member.SessionSlot);
        await publisher.PublishAsync(context);

        // A death is the other half of a kill, so it can be what reaches a rule's
        // kill limit — a room whose feed never arrives still ends on its tickets
        // from the deaths its players report.
        if (await results.TicketsReachedAsync() is { } reached)
        {
            logger.LogInformation(
                "UDP {LocalPort}: the deaths reported have taken team {Team} to its round's "
                + "{Tickets}-kill limit",
                context.LocalPort,
                reached.Team,
                reached.Tickets);
            matchStart.End(
                $"team {reached.Team} reached the round's {reached.Tickets}-kill limit",
                reached.ResultCode);
        }
    }

    /// <summary>
    /// Passes the record on to the room's other peers.
    /// </summary>
    /// <param name="context">Context the vitals arrived through.</param>
    private static async Task RelayAsync(PeerContext context)
    {
        var relay = context.Relay ?? context.Broadcast;
        await relay(context.Message.Type, context.Message.Body, context.Message.Flags);
    }
}
