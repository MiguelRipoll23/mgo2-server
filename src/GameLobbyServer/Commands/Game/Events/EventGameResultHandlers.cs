using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>
/// Handles the terminal result a native event host reports before it leaves.
/// <para>
/// This is the report that decides a match directly instead of inferring it from
/// the players' statistics. Both paths end in the same service, so accepting
/// this one does not fork the rules — it just means the outcome arrives as the
/// host states it rather than as the server reads it.
/// </para>
/// </summary>
public sealed class ReportEventGameResultHandler(
    EventOutcomeService outcomeService,
    GameService gameService,
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled
            || session.CharacterIdentifier is not { } characterIdentifier
            || packet.Payload.Length != EventConstants.EventGameResultWireSize)
        {
            await RefuseAsync(session, EventConstants.EventGameResultMalformed, cancellationToken);
            return;
        }

        // The result is the host's own statement, so only the connection hosting
        // the room may make it, and it may only make it about the room it holds.
        if (session.GameIdentifier is not { } gameIdentifier)
        {
            await RefuseAsync(session, EventConstants.EventGameResultNoRoom, cancellationToken);
            return;
        }

        var game = await gameService.FindByIdAsync(gameIdentifier, cancellationToken);
        if (game is null)
        {
            await RefuseAsync(session, EventConstants.EventGameResultNoRoom, cancellationToken);
            return;
        }

        if (game.HostIdentifier != characterIdentifier)
        {
            await RefuseAsync(session, EventConstants.EventGameResultNotHost, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        _ = reader.ReadInt32();
        _ = reader.ReadUInt8();
        _ = reader.ReadInt32();
        var winnerIdentity = reader.ReadInt32();
        var loserIdentity = reader.ReadInt32();

        var matchIdentifier = await outcomeService.FindMatchForGameAsync(gameIdentifier, cancellationToken);
        if (matchIdentifier <= 0)
        {
            // The room is not playing an event match, so there is no result for
            // it to report. Refused as unusable rather than acknowledged, so a
            // host is never told a result was taken when none was.
            await RefuseAsync(session, EventConstants.EventGameResultMalformed, cancellationToken);
            return;
        }

        var decision = await outcomeService.ReportTerminalAsync(
            matchIdentifier,
            winnerIdentity,
            loserIdentity,
            cancellationToken);
        if (decision is null)
        {
            // The identities named do not match this match's two teams in
            // winner-then-loser order.
            await RefuseAsync(session, EventConstants.EventGameResultMalformed, cancellationToken);
            return;
        }

        await sessionHelper.SendResultAsync(
            session,
            CommandConstants.ReportEventGameResultAck,
            ErrorCodeConstants.ResultNone,
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, int result, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.ReportEventGameResultAck,
            unchecked((uint)result),
            cancellationToken);
}
