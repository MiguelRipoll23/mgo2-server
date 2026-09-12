using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Users;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.AccountLobbyServer.Commands;

/// <summary>
/// Deletes a character the signed-in account owns. A character younger than
/// seven days cannot be deleted yet.
/// </summary>
/// <param name="characterService">Service that owns the characters.</param>
/// <param name="userService">Service that owns the accounts.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class DeleteCharacterHandler(
    CharacterService characterService,
    UserService userService,
    SessionHelper sessionHelper,
    ILogger<DeleteCharacterHandler> logger) : ICommandHandler
{
    private const int SecondsPerDay = 86400;
    private const int MinimumAgeDays = 7;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.UserIdentifier is null)
        {
            await sessionHelper.SendErrorAsync(session, 0x3106, ErrorCodeConstants.ErrorInvalidSession, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var slotIndex = reader.ReadUInt8();

        var user = await userService.FindByIdAsync(session.UserIdentifier.Value, cancellationToken);
        if (user is null)
        {
            await sessionHelper.SendErrorAsync(session, 0x3106, ErrorCodeConstants.ErrorInvalidSession, cancellationToken);
            return;
        }

        var characters = await characterService.FindByUserIdentifierAsync(session.UserIdentifier.Value, cancellationToken);
        var sortedCharacters = SelectCharacterHandler.OrderMainFirst(characters, user.MainCharacterIdentifier);

        if (slotIndex >= sortedCharacters.Count)
        {
            await sessionHelper.SendErrorAsync(session, 0x3106, ErrorCodeConstants.ErrorInvalidSession, cancellationToken);
            return;
        }

        var characterToDelete = sortedCharacters[slotIndex];
        var currentTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var ageSeconds = currentTimeSeconds - characterToDelete.CreationTime;

        if (ageSeconds < MinimumAgeDays * SecondsPerDay)
        {
            logger.LogInformation(
                "[tcp][account] 0x3105 character {CharacterIdentifier} is too young to delete",
                characterToDelete.Identifier);
            await sessionHelper.SendErrorAsync(session, 0x3106, ErrorCodeConstants.ErrorCharacterCannotDeleteYet, cancellationToken);
            return;
        }

        await characterService.SoftDeleteAsync(characterToDelete.Identifier, cancellationToken);

        await sessionHelper.SendStartEndPacketAsync(session, 0x3106, cancellationToken);
    }
}
