using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Acts on the coordinator's request to create a dedicated event host room.
/// <para>
/// The request names a mode rather than a lobby for the reason every other
/// request in this group does, and a lobby asked for a mode it is not running
/// refuses rather than furnishing a host in another lobby's event.
/// </para>
/// <para>
/// Once the room exists the assignment sweep finds it on its next tick, so
/// nothing is pushed from here: a match is announced by the assignment that
/// leases the room, not by the room's arrival.
/// </para>
/// </summary>
/// <param name="hostRoomService">Service that writes the room.</param>
/// <param name="identityService">Service that knows this lobby's own mode and row.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class FakeHostRoomRequestHandlerService(
    FakeHostRoomService hostRoomService,
    LobbyIdentityService identityService,
    ILogger<FakeHostRoomRequestHandlerService> logger)
{
    /// <summary>Carries out a coordinator's request.</summary>
    /// <param name="request">Request the coordinator sent.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleAsync(
        FakeHostRoomRequest request,
        CancellationToken cancellationToken)
    {
        var mode = await identityService.ResolveModeAsync(cancellationToken);
        if (mode is null || mode.Value != request.LobbySubtype)
        {
            logger.LogInformation(
                "A host room request named mode {Mode} and this lobby is {LobbySubtype}; refused",
                request.LobbySubtype,
                mode?.ToString() ?? "unresolved");
            return;
        }

        var lobbyIdentifier = await identityService.ResolveIdentifierAsync(mode.Value, cancellationToken);
        if (lobbyIdentifier <= 0)
        {
            logger.LogWarning("This lobby's own row could not be found; no host room was created");
            return;
        }

        var result = await hostRoomService.CreateAsync(
            lobbyIdentifier,
            mode.Value,
            request.HostCharacterIdentifier,
            cancellationToken);

        switch (result.Outcome)
        {
            case FakeHostRoomOutcome.NotAnEventHost:
                logger.LogInformation(
                    "Mode {Mode} has no dedicated event host room; refused",
                    mode.Value);
                return;

            case FakeHostRoomOutcome.NoHostCharacter:
                logger.LogWarning(
                    "No character was available to host the room in lobby {LobbyIdentifier}; none was created",
                    lobbyIdentifier);
                return;
        }

        logger.LogInformation(
            "Created dedicated host room {RoomName} ({GameIdentifier}) in lobby {LobbyIdentifier}, hosted by character {HostCharacterIdentifier}",
            result.RoomName,
            result.GameIdentifier,
            lobbyIdentifier,
            result.HostCharacterIdentifier);
    }
}
