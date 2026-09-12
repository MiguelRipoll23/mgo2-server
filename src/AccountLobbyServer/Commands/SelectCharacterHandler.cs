using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Users;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.AccountLobbyServer.Commands;

/// <summary>
/// Selects the character the client will play as. The payload carries the index
/// of the slot in the list the client was served, where the main character
/// always occupies the first slot.
/// </summary>
/// <param name="characterService">Service that owns the characters.</param>
/// <param name="userService">Service that owns the accounts.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class SelectCharacterHandler(
    CharacterService characterService,
    UserService userService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.UserIdentifier is null)
        {
            await sessionHelper.SendErrorAsync(session, 0x3104, ErrorCodeConstants.ErrorInvalidSession, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var slotIndex = reader.ReadUInt8();

        var user = await userService.FindByIdAsync(session.UserIdentifier.Value, cancellationToken);
        if (user is null)
        {
            await sessionHelper.SendErrorAsync(session, 0x3104, ErrorCodeConstants.ErrorInvalidSession, cancellationToken);
            return;
        }

        var characters = await characterService.FindByUserIdentifierAsync(session.UserIdentifier.Value, cancellationToken);
        var sortedCharacters = OrderMainFirst(characters, user.MainCharacterIdentifier);

        if (slotIndex >= sortedCharacters.Count)
        {
            await sessionHelper.SendErrorAsync(session, 0x3104, ErrorCodeConstants.ErrorInvalidSession, cancellationToken);
            return;
        }

        session.CharacterIdentifier = sortedCharacters[slotIndex].Identifier;

        await sessionHelper.SendStartEndPacketAsync(session, 0x3104, cancellationToken);
    }

    /// <summary>Orders a character list so the main character occupies the first slot.</summary>
    /// <param name="characters">Characters to order.</param>
    /// <param name="mainCharacterIdentifier">Identifier of the account's main character.</param>
    internal static List<Character> OrderMainFirst(
        IReadOnlyCollection<Character> characters,
        int? mainCharacterIdentifier) =>
    [
        .. characters.Where(character => character.Identifier == mainCharacterIdentifier),
        .. characters.Where(character => character.Identifier != mainCharacterIdentifier),
    ];
}
