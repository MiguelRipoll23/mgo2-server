using Mgo2Server.GameLobbyServer.Coordination;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Chat;

/// <summary>
/// Records the team the client reports for itself.
/// <para>
/// The request is one byte: the sender's own team, 1-based. The client reads its
/// own replicated character record and sends <c>v == 1 ? 2 : 1</c>, so 1 and 2 are
/// the only values it can build and everything else — the third role, the
/// "no team" sentinel — collapses onto the first team. This is the only place a
/// team reaches us outside the host's register, and answering it with a result and
/// nothing else is why every roster row stayed on its default and team chat had no
/// team to narrow to.
/// </para>
/// </summary>
/// <param name="gameService">Service the reported slot is stored with.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class SetTeamHandler(
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.GameIdentifier is { } gameIdentifier &&
            session.CharacterIdentifier is { } characterIdentifier &&
            packet.Payload.Length >= 1)
        {
            var team = TeamSlotUtils.FromOneBasedTeam(packet.Payload[0]);
            await gameService.SetPlayerTeamAsync(gameIdentifier, characterIdentifier, team, cancellationToken);
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.SetTeamResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>Broadcasts a chat message to everyone in the same room.</summary>
/// <param name="activeGameSessions">Connections currently in the lobby.</param>
/// <param name="gameService">Service the room's teams are read from.</param>
/// <param name="survivalTest">Service the Survival self-test runs through.</param>
/// <param name="lobbyIdentity">Service that knows this lobby's own game type.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class SendChatHandler(
    ActiveGameSessionsService activeGameSessions,
    GameService gameService,
    SurvivalTestService survivalTest,
    LobbyIdentityService lobbyIdentity,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Prefix a client may not impersonate.</summary>
    private const string ServerPrefix = "Server | ";

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is null)
        {
            return;
        }

        var request = ChatPayloadBuilder.ParseRequest(packet.Payload);
        if (request is null)
        {
            return;
        }

        var characterIdentifier = session.CharacterIdentifier.Value;

        // The self-test rides on chat because the client has no command for it.
        // It is served only by a lobby whose own game type is Survival, so a
        // /test typed into any other lobby stays ordinary text.
        if (SurvivalTestService.IsCommand(request.Text)
            && await lobbyIdentity.ResolveModeAsync(cancellationToken) == LobbySubtypeConstants.Survival)
        {
            await survivalTest.RunAsync(session, cancellationToken);
            return;
        }

        // A client may not impersonate the server.
        if (request.Text.StartsWith(ServerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.SendChatResult,
                ChatPayloadBuilder.BuildReply(
                    characterIdentifier,
                    request with { Text = ServerPrefix + "You can't send server messages." }),
                cancellationToken);
            return;
        }

        var payload = ChatPayloadBuilder.BuildReply(characterIdentifier, request);

        // Not in a game: answer the sender alone so their own line still renders.
        // The client has no local echo, so a fanned-out reply is the only route
        // the message has to the screen, its author's included.
        if (session.GameIdentifier is not { } gameIdentifier)
        {
            await sessionHelper.SendPacketAsync(session, CommandConstants.SendChatResult, payload, cancellationToken);
            return;
        }

        // Team chat is the one channel that is not the whole room, so it is the only
        // one that pays for reading the roster. The teams are the ones the clients
        // reported; a sender the roster holds nothing for keeps the room-wide
        // delivery rather than falling silent.
        var teams = ChatPayloadBuilder.IsTeamChannel(request)
            ? await gameService.GetPlayerTeamsAsync(gameIdentifier, cancellationToken)
            : null;

        short? senderTeam = teams is not null && teams.TryGetValue(characterIdentifier, out var team)
            ? team
            : null;

        foreach (var target in activeGameSessions.List())
        {
            if (target.GameIdentifier == gameIdentifier &&
                ChatRecipientUtils.ReachesRecipient(target.CharacterIdentifier, teams, senderTeam))
            {
                await sessionHelper.SendPacketAsync(target, CommandConstants.SendChatResult, payload, cancellationToken);
            }
        }
    }
}
