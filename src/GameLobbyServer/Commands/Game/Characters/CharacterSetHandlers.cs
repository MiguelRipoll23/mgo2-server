using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>Sends the gear catalogue, echoed when an outfit is committed.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class CommitOutfitHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is null)
        {
            // The reply is a table, not a result code: staying silent stalls
            // the screen, but a session with no character cannot reach it.
            return;
        }

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.CommitOutfitResult,
            GearCatalogue.Payload,
            cancellationToken);
    }
}

