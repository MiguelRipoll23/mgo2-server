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
/// Serves the character list of the signed-in account. The reply is a fixed
/// 471-byte grid regardless of how many characters exist: a header, eight
/// fifty-two byte slots and a thirty-two byte trailer.
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
    /// <summary>Size of the reply header.</summary>
    private const int ListHeaderSize = 23;

    /// <summary>Number of character slots the client shows.</summary>
    private const int ListSlots = 8;

    /// <summary>Size of one character entry.</summary>
    private const int ListEntrySize = 52;

    /// <summary>Offset of the trailer inside the reply.</summary>
    private const int ListTrailerOffset = ListHeaderSize + (ListSlots * ListEntrySize);

    /// <summary>Total size of the reply.</summary>
    private const int ListPayloadSize = 0x1d7;

    /// <summary>Size of the trailer, which carries the account entitlements.</summary>
    private const int TrailerSize = 32;

    /// <summary>Field length of a character name.</summary>
    private const int NameLength = 16;

    /// <summary>
    /// Per-account entitlement values. Index 3 defaults to 0x03 and index 1 to
    /// 0x07: bit 0 of index 3 is the understood bit, which unlocks the gated
    /// entries of the codec pack, and the remaining set bits are kept because
    /// they are what this server has always sent.
    /// </summary>
    private const byte EntitlementsIndex1Default = 0x07;

    /// <summary>Entitlement byte whose bit 0 is the codec pack unlock.</summary>
    private const byte EntitlementsIndex3Default = 0x03;

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
        var mainCharacterIdentifier = user.MainCharacterIdentifier;

        var sortedCharacters = characters
            .Where(character => character.Identifier == mainCharacterIdentifier)
            .Concat(characters.Where(character => character.Identifier != mainCharacterIdentifier))
            .ToList();

        var shownCount = Math.Min(sortedCharacters.Count, ListSlots);
        var firstIsMain = sortedCharacters.Count > 0 && sortedCharacters[0].Identifier == mainCharacterIdentifier;
        var selectedName = sortedCharacters.Count > 0 ? sortedCharacters[0].Name : string.Empty;

        var writer = new PacketWriter();
        writer.WriteUInt32(0);
        writer.WriteUInt8(user.Slots);
        writer.WriteUInt8(shownCount);
        writer.WriteUInt8(0);
        writer.WriteFixedString(
            firstIsMain ? $"*{selectedName}"[..Math.Min(NameLength, selectedName.Length + 1)] : selectedName,
            NameLength);

        for (var index = 0; index < shownCount; index++)
        {
            var character = sortedCharacters[index];
            var appearance = await characterService.GetAppearanceAsync(character.Identifier, cancellationToken);
            WriteCharacterEntry(writer, character, appearance, index, character.Identifier == mainCharacterIdentifier);
        }

        // Pad to the fixed trailer offset: the grid is fixed regardless of the
        // character count, and the client copies the trailer from its end.
        writer.WritePadding(ListTrailerOffset - writer.Size);
        writer.WriteUInt8(EntitlementsIndex1Default);
        writer.WriteUInt8(0);
        writer.WriteUInt8(EntitlementsIndex3Default);
        writer.WritePadding(TrailerSize - 3);

        var payload = writer.Build();
        if (payload.Length != ListPayloadSize)
        {
            // A wrong grid size desynchronises the trailer, so the mismatch is
            // reported while the packet still goes out.
            logger.LogError(
                "[tcp][account] 0x3049 payload is {Length} bytes, expected {Expected}",
                payload.Length,
                ListPayloadSize);
        }

        await sessionHelper.SendPacketAsync(session, 0x3049, payload, cancellationToken);
    }

    /// <summary>
    /// Writes one fifty-two byte entry: the slot index, the character
    /// identifier, the name and the appearance block, whose trailing word is the
    /// deletion cooldown in seconds.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="character">Character to write.</param>
    /// <param name="appearance">Appearance of the character, when it has one.</param>
    /// <param name="slotIndex">Index of the slot.</param>
    /// <param name="isMain">Whether this is the account's main character.</param>
    private static void WriteCharacterEntry(
        PacketWriter writer,
        Character character,
        CharacterAppearance? appearance,
        int slotIndex,
        bool isMain)
    {
        var displayName = isMain ? $"*{character.Name}" : character.Name;

        writer.WriteUInt8(slotIndex);
        writer.WriteUInt32((uint)character.Identifier);
        writer.WriteFixedString(displayName[..Math.Min(NameLength, displayName.Length)], NameLength);

        writer.WriteUInt8(appearance?.Gender ?? 0);
        writer.WriteUInt8(appearance?.Face ?? 0);
        writer.WriteUInt8(appearance?.Upper ?? 0);
        writer.WriteUInt8(appearance?.Lower ?? 0);
        writer.WriteUInt8(appearance?.FacePaint ?? 0);
        writer.WriteUInt8(appearance?.UpperColor ?? 0);
        writer.WriteUInt8(appearance?.LowerColor ?? 0);
        writer.WriteUInt8(appearance?.Voice ?? 0);
        writer.WriteUInt8(appearance?.Pitch ?? 0);
        writer.WritePadding(4);
        writer.WriteUInt8(appearance?.Head ?? 0);
        writer.WriteUInt8(appearance?.Chest ?? 0);
        writer.WriteUInt8(appearance?.Hands ?? 0);
        writer.WriteUInt8(appearance?.Waist ?? 0);
        writer.WriteUInt8(appearance?.Feet ?? 0);
        writer.WriteUInt8(appearance?.Accessory1 ?? 0);
        writer.WriteUInt8(appearance?.Accessory2 ?? 0);
        writer.WriteUInt8(appearance?.HeadColor ?? 0);
        writer.WriteUInt8(appearance?.ChestColor ?? 0);
        writer.WriteUInt8(appearance?.HandsColor ?? 0);
        writer.WriteUInt8(appearance?.WaistColor ?? 0);
        writer.WriteUInt8(appearance?.FeetColor ?? 0);
        writer.WriteUInt8(appearance?.Accessory1Color ?? 0);
        writer.WriteUInt8(appearance?.Accessory2Color ?? 0);

        // The trailing word is the number of seconds until the character may be
        // deleted. No cooldown is enforced, so it is zero.
        writer.WriteUInt32(0);
    }
}
