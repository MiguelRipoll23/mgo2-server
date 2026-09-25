using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>
/// Takes a Tournament place for the calling character. The place is asked for
/// before a team exists, so this is the first moment the server sees the player
/// rather than the team, and it is where the level limits apply.
/// </summary>
public sealed class ReserveTournamentEntryHandler(
    TournamentRegistrationService registrationService,
    LobbyService lobbyService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Logical size of the request: one event identifier.</summary>
    private const int RequestWireSize = 4;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // A place is held for a character, so there is nothing to reserve without
        // one.
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // The request is one event identifier and nothing else.
        if (packet.Payload.Length != RequestWireSize)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // A place is taken from the registration lobby only: the same command
        // reached from a Survival lobby would otherwise reserve a Tournament seat.
        if (session.LobbyIdentifier is not { } lobbyIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var lobby = await lobbyService.FindByIdAsync(lobbyIdentifier, cancellationToken);
        if (lobby.SubtypeIdentifier != EventConstants.TournamentRegistrationSelector)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var eventIdentifier = new PacketReader(packet.Payload).ReadInt32();
        if (eventIdentifier <= 0)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var outcome = await registrationService.ReserveAsync(
            characterIdentifier,
            eventIdentifier,
            cancellationToken);

        var result = outcome switch
        {
            // Already holding this event's place is a success rather than a
            // refusal: the client asked for a state it is already in, and the
            // reference treats a retry as idempotent.
            TournamentReserveOutcome.Reserved or TournamentReserveOutcome.AlreadyReserved =>
                ErrorCodeConstants.ResultNone,
            _ => EventConstants.ResultTournamentReservationRejected,
        };

        await sessionHelper.SendResultAsync(
            session,
            CommandConstants.ReserveTournamentEntryResult,
            result,
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.ReserveTournamentEntryResult,
            EventConstants.ResultTournamentReservationRejected,
            cancellationToken);
}
