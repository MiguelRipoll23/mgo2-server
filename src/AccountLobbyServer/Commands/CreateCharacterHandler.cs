using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Users;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.AccountLobbyServer.Commands;

/// <summary>
/// Creates a character for the signed-in account. The payload carries the name
/// followed by the appearance the player assembled in the creator.
/// </summary>
/// <param name="characterService">Service that owns the characters.</param>
/// <param name="userService">Service that owns the accounts.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class CreateCharacterHandler(
    CharacterService characterService,
    UserService userService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Field length of a character name.</summary>
    private const int CharacterNameLength = 16;

    /// <summary>Prefixes reserved for system characters.</summary>
    private static readonly string[] ReservedPrefixes = [":#", "GM_", "GM-", "GM.", "GM,"];

    /// <summary>Names reserved for system characters.</summary>
    private static readonly string[] ReservedNames = ["SaveMGO"];

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.UserIdentifier is null)
        {
            await sessionHelper.SendErrorAsync(session, 0x3102, ErrorCodeConstants.ErrorInvalidSession, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var name = reader.ReadFixedString(CharacterNameLength);
        var gender = reader.ReadUInt8();
        var face = reader.ReadUInt8();
        var upper = reader.ReadUInt8();
        var lower = reader.ReadUInt8();
        var facePaint = reader.ReadUInt8();
        var upperColor = reader.ReadUInt8();
        var lowerColor = reader.ReadUInt8();
        var voice = reader.ReadUInt8();
        var pitch = reader.ReadUInt8();
        reader.Skip(4);
        var head = reader.ReadUInt8();
        var chest = reader.ReadUInt8();
        var hands = reader.ReadUInt8();
        var waist = reader.ReadUInt8();
        var feet = reader.ReadUInt8();
        var accessory1 = reader.ReadUInt8();
        var accessory2 = reader.ReadUInt8();
        var headColor = reader.ReadUInt8();
        var chestColor = reader.ReadUInt8();
        var handsColor = reader.ReadUInt8();
        var waistColor = reader.ReadUInt8();
        var feetColor = reader.ReadUInt8();
        var accessory1Color = reader.ReadUInt8();
        var accessory2Color = reader.ReadUInt8();

        if (!StringUtility.IsValidName(name))
        {
            await sessionHelper.SendErrorAsync(session, 0x3102, ErrorCodeConstants.ErrorCharacterNameInvalid, cancellationToken);
            return;
        }

        if (ReservedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
        {
            await sessionHelper.SendErrorAsync(session, 0x3102, ErrorCodeConstants.ErrorCharacterNamePrefix, cancellationToken);
            return;
        }

        if (ReservedNames.Contains(name))
        {
            await sessionHelper.SendErrorAsync(session, 0x3102, ErrorCodeConstants.ErrorCharacterNameReserved, cancellationToken);
            return;
        }

        var existingCharacter = await characterService.FindByNameAsync(name, cancellationToken);
        if (existingCharacter is not null)
        {
            await sessionHelper.SendErrorAsync(session, 0x3102, ErrorCodeConstants.ErrorCharacterNameTaken, cancellationToken);
            return;
        }

        var user = await userService.FindByIdAsync(session.UserIdentifier.Value, cancellationToken);
        if (user is null)
        {
            await sessionHelper.SendErrorAsync(session, 0x3102, ErrorCodeConstants.ErrorInvalidSession, cancellationToken);
            return;
        }

        var existingCharacters = await characterService.FindByUserIdentifierAsync(session.UserIdentifier.Value, cancellationToken);
        if (existingCharacters.Count >= user.Slots)
        {
            await sessionHelper.SendErrorAsync(session, 0x3102, ErrorCodeConstants.ErrorGeneral, cancellationToken);
            return;
        }

        var creationTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var created = await characterService.CreateAsync(
            new CharacterCreateInput(session.UserIdentifier.Value, name, creationTime),
            appearance =>
            {
                appearance.Gender = gender;
                appearance.Face = face;
                appearance.Voice = voice;
                appearance.Pitch = pitch;
                appearance.Head = head;
                appearance.HeadColor = headColor;
                appearance.Upper = upper;
                appearance.UpperColor = upperColor;
                appearance.Lower = lower;
                appearance.LowerColor = lowerColor;
                appearance.Chest = chest;
                appearance.ChestColor = chestColor;
                appearance.Waist = waist;
                appearance.WaistColor = waistColor;
                appearance.Hands = hands;
                appearance.HandsColor = handsColor;
                appearance.Feet = feet;
                appearance.FeetColor = feetColor;
                appearance.Accessory1 = accessory1;
                appearance.Accessory1Color = accessory1Color;
                appearance.Accessory2 = accessory2;
                appearance.Accessory2Color = accessory2Color;
                appearance.FacePaint = facePaint;
            },
            cancellationToken);

        var responseWriter = new PacketWriter();
        responseWriter.WritePadding(4);
        responseWriter.WriteUInt32((uint)created.Identifier);

        await sessionHelper.SendPacketAsync(session, 0x3102, responseWriter.Build(), cancellationToken);
    }
}
