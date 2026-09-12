using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.AccountLobbyServer.Commands;

/// <summary>
/// Pre-checks a character name before the creator submits it. The same
/// taken-name rule the create applies is enforced here, because a pre-check that
/// answers more leniently would only move the failure one screen later.
/// </summary>
/// <param name="characterService">Service that owns the characters.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class CheckCharacterNameHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Field length of a character name.</summary>
    private const int NameLength = 16;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.UserIdentifier is null)
        {
            await sessionHelper.SendResultAsync(session, 0x3108, ErrorCodeConstants.ResultInvalidSession, cancellationToken);
            return;
        }

        if (packet.Payload.Length < NameLength)
        {
            await sessionHelper.SendResultAsync(session, 0x3108, ErrorCodeConstants.ResultGeneral, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var name = reader.ReadFixedString(NameLength).Trim();

        if (name.Length == 0)
        {
            await sessionHelper.SendResultAsync(session, 0x3108, ErrorCodeConstants.ResultNameInvalid, cancellationToken);
            return;
        }

        var taken = await characterService.FindByNameAsync(name, cancellationToken);
        await sessionHelper.SendResultAsync(
            session,
            0x3108,
            taken is not null ? ErrorCodeConstants.ResultNameTaken : ErrorCodeConstants.ResultNone,
            cancellationToken);
    }
}
