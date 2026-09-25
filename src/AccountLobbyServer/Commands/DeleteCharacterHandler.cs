using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Domain.Users;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.AccountLobbyServer.Commands;

/// <summary>
/// Deletes a character the signed-in account owns. The delete is soft: the row
/// stays and its name is released. A character younger than the cooldown cannot be
/// deleted yet, and one that leads a clan cannot be deleted at all.
/// </summary>
/// <param name="characterService">Service that owns the characters.</param>
/// <param name="userService">Service that owns the accounts.</param>
/// <param name="clanService">Service that owns the clans the character may lead.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class DeleteCharacterHandler(
    CharacterService characterService,
    UserService userService,
    ClanService clanService,
    SessionHelper sessionHelper,
    ILogger<DeleteCharacterHandler> logger) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.AccountIdentifier is null)
        {
            await sessionHelper.SendErrorAsync(session, 0x3106, ErrorCodeConstants.ErrorInvalidSession, cancellationToken);
            return;
        }

        var accountIdentifier = session.AccountIdentifier.Value;

        var reader = new PacketReader(packet.Payload);
        var slotIndex = reader.ReadUInt8();

        var user = await userService.FindByIdAsync(accountIdentifier, cancellationToken);
        if (user is null)
        {
            await sessionHelper.SendErrorAsync(session, 0x3106, ErrorCodeConstants.ErrorInvalidSession, cancellationToken);
            return;
        }

        var characters = await characterService.FindByAccountIdentifierAsync(accountIdentifier, cancellationToken);
        if (characters.Count == 0)
        {
            await sessionHelper.SendResultAsync(
                session,
                0x3106,
                ErrorCodeConstants.ResultCharacterDoesNotExist,
                cancellationToken);
            return;
        }

        var sortedCharacters = SelectCharacterHandler.OrderMainFirst(characters, user.MainCharacterIdentifier);

        // The client sends the slot it picked from the list it was served, and that list
        // is worthless to us if the account changed while the screen sat open. An index
        // past the end therefore selects the last character rather than nothing: falling
        // back to the first would delete a character other than the one under the cursor,
        // which is the one way this command can destroy the wrong thing.
        var selectedIndex = slotIndex < sortedCharacters.Count ? slotIndex : sortedCharacters.Count - 1;
        var characterToDelete = sortedCharacters[selectedIndex];

        if (!CharacterService.CanDelete(characterToDelete.CreatedAt, DateTimeOffset.UtcNow))
        {
            logger.LogInformation(
                "[tcp][account] 0x3105 character {CharacterIdentifier} is too young to delete",
                characterToDelete.Identifier);
            // The official code, sent unmasked: the client compares it against a
            // literal, and its own pre-check raises the same dialog with the same
            // value, so this backstop reads exactly like the refusal it makes.
            await sessionHelper.SendResultAsync(
                session,
                0x3106,
                ErrorCodeConstants.ResultCharacterCannotDeleteYet,
                cancellationToken);
            return;
        }

        // A clan leader cannot delete the character they lead. The refusal is not
        // courtesy: the delete is soft, so the clan's leader reference is never cleared
        // by the foreign key, and letting this through would leave the clan pointing at
        // a character that can no longer be signed into. Disbanding the clan and handing
        // the leadership to another member are both already in the game, and the code
        // below is the one that names them.
        var membership = await clanService.FindMembershipByCharacterAsync(characterToDelete.Identifier, cancellationToken);
        if (membership is { IsLeader: true })
        {
            logger.LogInformation(
                "[tcp][account] 0x3105 character {CharacterIdentifier} leads clan {ClanIdentifier}, which it cannot do and be deleted",
                characterToDelete.Identifier,
                membership.ClanIdentifier);
            await sessionHelper.SendResultAsync(
                session,
                0x3106,
                ErrorCodeConstants.ResultCharacterIsClanLeader,
                cancellationToken);
            return;
        }

        await characterService.SoftDeleteAsync(accountIdentifier, characterToDelete.Identifier, cancellationToken);

        await sessionHelper.SendStartEndPacketAsync(session, 0x3106, cancellationToken);
    }
}
