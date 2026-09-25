using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>
/// Answers the game-entry information screen, which the lobby-select screen and
/// the post-assignment event screen both open.
/// <para>
/// The request carries no payload and no identifier — the client sends nothing
/// and the reply is about whoever asked — so the entry is read from the caller's
/// own team and character rather than from the packet.
/// </para>
/// <para>
/// A character with neither an assignment nor a place is answered with the four
/// empty slots rather than an error: that is the grid the lobby-select screen
/// shows, and it is what the record's zero key means.
/// </para>
/// </summary>
/// <param name="gameEntryService">Service that assembles the caller's entry.</param>
/// <param name="sessionHelper">Helper used to write the reply.</param>
public sealed class GetGameEntryInfoHandler(
    EventGameEntryService gameEntryService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // Without a character there is nothing to describe, and an answer of
        // empty slots would claim the caller holds no entry when the server does
        // not know who is asking.
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // The entries are the lobby's, so a connection that is in none has no
        // grid to fill.
        if (session.LobbyIdentifier is not { } lobbyIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var entries = await gameEntryService.ListForCharacterAsync(
            characterIdentifier,
            session.EventTeamIdentifier ?? 0,
            lobbyIdentifier,
            cancellationToken);

        var writer = new PacketWriter();
        EventGameEntryUtils.WriteGameEntryInfo(writer, entries);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetGameEntryInfoResult,
            writer.Build(),
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.GetGameEntryInfoResult,
            EventConstants.ResultActiveStateMismatch,
            cancellationToken);
}
