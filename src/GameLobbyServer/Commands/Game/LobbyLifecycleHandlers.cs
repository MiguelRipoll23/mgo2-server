using Mgo2Server.Http.Discord;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game;

/// <summary>
/// Echoes the payload of the pre-lobby handshake back to the client. The
/// command sits outside the lobby packet space, but the client stalls when it
/// is not answered.
/// </summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class EchoHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        sessionHelper.SendPacketAsync(session, CommandConstants.Echo, packet.Payload, cancellationToken);
}

/// <summary>Leaves the gameplay lobby and republishes the player counts.</summary>
/// <param name="lobbyTrackerService">Service that tracks the lobby population.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="characterService">Service the departing character's name is read from.</param>
/// <param name="discordGameEventService">Service that posts the departure to Discord.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class GetLobbyDisconnectHandler(
    LobbyTrackerService lobbyTrackerService,
    SessionHelper sessionHelper,
    CharacterService characterService,
    DiscordGameEventService discordGameEventService,
    ILogger<GetLobbyDisconnectHandler> logger) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // Read the name before the lobby bookkeeping: it is the session's
        // character identifier that names it, and leaving must not depend on a
        // lookup that can fail. A name that cannot be read still gets a message,
        // because a silent departure is the one case the channel cannot infer.
        if (session.CharacterIdentifier is { } characterIdentifier)
        {
            var characterName = await ReadCharacterNameAsync(characterIdentifier, cancellationToken);
            await discordGameEventService.PostPlayerDisconnectedAsync(characterName, cancellationToken);
        }

        lobbyTrackerService.LeaveLobby(session);
        await lobbyTrackerService.SynchronizeAllLobbyCountsAsync(cancellationToken);
        await sessionHelper.SendResultAsync(session, CommandConstants.GetLobbyDisconnectResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }

    /// <summary>
    /// Reads a character's name, falling back to its identifier when the read
    /// fails so a departure is never dropped for want of a lookup.
    /// </summary>
    /// <param name="characterIdentifier">Identifier of the departing character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<string> ReadCharacterNameAsync(int characterIdentifier, CancellationToken cancellationToken)
    {
        try
        {
            var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
            return string.IsNullOrWhiteSpace(character?.Name)
                ? $"Player_{characterIdentifier}"
                : character.Name;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not read character name for {CharacterIdentifier}", characterIdentifier);
            return $"Player_{characterIdentifier}";
        }
    }
}

/// <summary>Answers a training-session connection with its fixed payload.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class TrainingConnectHandler(SessionHelper sessionHelper) : ICommandHandler
{
    private static readonly byte[] Payload =
    [
        0x00, 0x0a, 0x00, 0x15, 0x00, 0x3a, 0x00, 0x08, 0x00, 0x61,
    ];

    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        sessionHelper.SendPacketAsync(session, CommandConstants.TrainingConnectResult, Payload, cancellationToken);
}
