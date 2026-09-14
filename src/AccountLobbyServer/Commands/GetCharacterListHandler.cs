using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Users;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.AccountLobbyServer.Commands;

/// <summary>
/// Serves the character list of the signed-in account by answering the request
/// with the fixed grid built by <see cref="CharacterListPayloadBuilder"/>.
/// </summary>
/// <param name="characterService">Service that owns the characters.</param>
/// <param name="userService">Service that owns the accounts.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class GetCharacterListHandler(
    CharacterService characterService,
    UserService userService,
    SessionHelper sessionHelper,
    ILogger<GetCharacterListHandler> logger) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.UserIdentifier is null)
        {
            await sessionHelper.SendErrorAsync(session, 0x3049, ErrorCodeConstants.ErrorInvalidSession, cancellationToken);
            return;
        }

        var user = await userService.FindByIdAsync(session.UserIdentifier.Value, cancellationToken);
        if (user is null)
        {
            await sessionHelper.SendErrorAsync(session, 0x3049, ErrorCodeConstants.ErrorInvalidSession, cancellationToken);
            return;
        }

        var characters = await characterService.FindByUserIdentifierAsync(session.UserIdentifier.Value, cancellationToken);
        var orderedCharacters = SelectCharacterHandler.OrderMainFirst(characters, user.MainCharacterIdentifier);
        var entries = await LoadEntriesAsync(orderedCharacters, user.MainCharacterIdentifier, cancellationToken);

        var payload = CharacterListPayloadBuilder.Build(user.Slots, entries);
        if (payload.Length != CharacterListPayloadBuilder.PayloadSize)
        {
            // A wrong grid size desynchronises the trailer, so the mismatch is
            // reported while the packet still goes out.
            logger.LogError(
                "[tcp][account] 0x3049 payload is {Length} bytes, expected {Expected}",
                payload.Length,
                CharacterListPayloadBuilder.PayloadSize);
        }

        await sessionHelper.SendPacketAsync(session, 0x3049, payload, cancellationToken);
    }

    /// <summary>
    /// Reads the appearance of every character the grid holds, in the order it is
    /// served, and pairs it with the character and its main-character flag. The
    /// characters past the grid are left out so no appearance is loaded for them.
    /// </summary>
    /// <param name="characters">Characters of the account, main first.</param>
    /// <param name="mainCharacterIdentifier">Identifier of the account's main character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<List<CharacterListPayloadBuilder.Entry>> LoadEntriesAsync(
        IReadOnlyList<Character> characters,
        int? mainCharacterIdentifier,
        CancellationToken cancellationToken)
    {
        // TODO: characters past the eighth are dropped without a trace. The client
        // grid cannot carry them (its parser fills eight records), so an account
        // whose User.Slots is raised past that silently loses the selection of the
        // extra characters. Either cap Slots at CharacterListPayloadBuilder.SlotCount
        // or report the overflow here.
        var served = characters.Take(CharacterListPayloadBuilder.SlotCount);
        var entries = new List<CharacterListPayloadBuilder.Entry>();

        foreach (var character in served)
        {
            var appearance = await characterService.GetAppearanceAsync(character.Identifier, cancellationToken);
            var isMain = character.Identifier == mainCharacterIdentifier;
            entries.Add(new CharacterListPayloadBuilder.Entry(character, appearance, isMain));
        }

        return entries;
    }
}
