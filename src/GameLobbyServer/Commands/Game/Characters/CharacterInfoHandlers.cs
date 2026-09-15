using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Users;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Serves the connect burst: the character header followed by every catalogue
/// the client needs, in the order it expects them.
/// </summary>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="userService">Service that owns the accounts.</param>
/// <param name="titleService">Service that latches the titles the character has earned.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class GetCharacterInfoHandler(
    CharacterService characterService,
    UserService userService,
    CharacterTitleService titleService,
    SessionHelper sessionHelper,
    ILogger<GetCharacterInfoHandler> logger) : ICommandHandler
{
    /// <summary>
    /// Offset of the sixteen-byte map and rule availability mask, one past the
    /// tail byte that follows the friend and blocked grids.
    /// </summary>
    private const int ContentMaskOffset = 0x22a;

    /// <summary>Offset of the trailing feature byte, one past the fixed grid.</summary>
    private const int FeatureByteOffset = 0x242;

    /// <summary>
    /// Number of identifiers in each friend and blocked array. The client reads
    /// 64 (its loops compare against 0x40), so each array is 256 bytes: a short
    /// payload shifts the feature byte into the friend grid.
    /// </summary>
    private const int MaximumListIdentifiers = 64;

    /// <summary>The four dead 16-bit constants that follow the name.</summary>
    private static readonly byte[] CharacterInfoFixedBytes =
    [
        0x16, 0xae, 0x03, 0x38, 0x01, 0x3e, 0x01, 0x50,
    ];

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // The refusal is silence, and that is deliberate. 0x4101 has no result
        // word: its first field is the character identifier and its parser branches
        // only on its read primitive, never on a field value. A four-byte reply is
        // therefore read as a record whose identifier is whatever we put in the
        // result word, which the client then carries into every later packet; a
        // full-length reply with a zero identifier is no better. The client's only
        // symptom is a timeout on the connect screen, so the reason is logged here.
        // The check-session refuses these conditions first, which makes reaching one
        // a race or lost connection state rather than an ordinary rejection.
        if (session.CharacterIdentifier is not { } characterIdentifier || characterIdentifier <= 0)
        {
            logger.LogWarning(
                "0x4100 cannot be served: the session holds no character. Answering nothing; the client will time out on the connect screen");
            return;
        }

        var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
        if (character is null || character.Active != 1)
        {
            logger.LogWarning(
                "0x4100 cannot be served: character {CharacterIdentifier} is gone or suspended. Answering nothing",
                characterIdentifier);
            return;
        }

        // Stamping the visit also re-tests the titles — a character who qualified
        // while away is told on the way in — and it runs before the payloads below
        // are built, so a title earned since the last visit is already worn by the
        // record this burst describes.
        var unlocked = await titleService.EvaluateAsync(characterIdentifier, cancellationToken: cancellationToken);
        if (unlocked.Count > 0)
        {
            logger.LogInformation(
                "Character {CharacterIdentifier} unlocked title {Titles} on entering the lobby",
                characterIdentifier,
                string.Join(',', unlocked));
            character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken) ?? character;
        }

        var user = session.UserIdentifier is { } userIdentifier
            ? await userService.FindByIdAsync(userIdentifier, cancellationToken)
            : null;
        var friendsAndBlocked = await characterService.GetFriendsAndBlockedAsync(characterIdentifier, cancellationToken);

        var friends = friendsAndBlocked.Where(entry => entry.Type == 0).Select(entry => entry.TargetIdentifier).ToList();
        var blocked = friendsAndBlocked.Where(entry => entry.Type == 1).Select(entry => entry.TargetIdentifier).ToList();

        var experience = user?.MainExperience ?? 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var lastLogin = character.CreationTime;

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetCharacterInfoResult,
            BuildCharacterInfoPayload(characterIdentifier, character.Name, experience, now, lastLogin, friends, blocked),
            cancellationToken);

        // The gameplay options must be populated here: an empty payload makes
        // the client's validator reset every setting to its hardcoded default.
        var storedOptions = GameplayOptionsCodec.ParseStored(character.GameplayOptions);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetGameplayOptionsResult,
            GameplayOptionsCodec.BuildPayload(storedOptions),
            cancellationToken);

        var macros = await characterService.GetChatMacrosAsync(characterIdentifier, cancellationToken);

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetChatMacrosResult,
            CharacterPayloadBuilder.BuildChatMacrosPayload(macros, 0),
            cancellationToken);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetChatMacrosResult,
            CharacterPayloadBuilder.BuildChatMacrosPayload(macros, 1),
            cancellationToken);

        await SendPersonalInfoAsync(session, characterIdentifier, character, cancellationToken);

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetGear,
            GearCatalogue.Payload,
            cancellationToken);

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetSkills,
            CharacterPayloadBuilder.BuildSkillsPayload(),
            cancellationToken);

        var skillSets = await characterService.GetSkillSetsAsync(characterIdentifier, cancellationToken);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetSkillSets,
            CharacterPayloadBuilder.BuildSkillSetsPayload(skillSets),
            cancellationToken);

        var gearSets = await characterService.GetGearSetsAsync(characterIdentifier, cancellationToken);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetGearSets,
            CharacterPayloadBuilder.BuildGearSetsPayload(gearSets),
            cancellationToken);
    }

    private async Task SendPersonalInfoAsync(
        TcpSession session,
        int characterIdentifier,
        Shared.Persistence.Entities.Character? character,
        CancellationToken cancellationToken)
    {
        if (characterIdentifier <= 0)
        {
            await sessionHelper.SendPacketAsync(session, CommandConstants.GetPersonalInfo, null, cancellationToken);
            return;
        }

        var appearance = await characterService.GetAppearanceAsync(characterIdentifier, cancellationToken);
        var skills = await characterService.GetEquippedSkillsAsync(characterIdentifier, cancellationToken);
        var clan = await characterService.GetClanInformationAsync(characterIdentifier, cancellationToken);

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetPersonalInfo,
            CharacterPayloadBuilder.BuildPersonalInfoPayload(
                character,
                appearance,
                skills,
                clan,
                characterIdentifier),
            cancellationToken);
    }

    private static byte[] BuildCharacterInfoPayload(
        int characterIdentifier,
        string characterName,
        int experience,
        long lastLogin,
        int secondLastLogin,
        IReadOnlyList<int> friends,
        IReadOnlyList<int> blocked)
    {
        var writer = new PacketWriter();
        writer.WriteUInt32((uint)characterIdentifier);
        writer.WriteFixedString(characterName, 16);
        writer.WriteBytes(CharacterInfoFixedBytes);
        writer.WriteUInt32((uint)experience);
        // The client shows the previous login alongside the current one.
        writer.WriteUInt32((uint)secondLastLogin);
        writer.WriteUInt32((uint)lastLogin);
        writer.WriteUInt8(0);

        for (var index = 0; index < MaximumListIdentifiers; index++)
        {
            writer.WriteUInt32((uint)(index < friends.Count ? friends[index] : 0));
        }

        for (var index = 0; index < MaximumListIdentifiers; index++)
        {
            writer.WriteUInt32((uint)(index < blocked.Count ? blocked[index] : 0));
        }

        // Tail the client reads past the two grids: a u8, the map and rule
        // availability mask, two reserved u32s and the feature byte.
        writer.WritePadding(ContentMaskOffset - writer.Size);
        writer.WriteBytes(FeatureFlags.ContentMask);
        writer.WritePadding(FeatureByteOffset - writer.Size);
        // The parser reads this byte's four low bits as separate feature flags
        // and greys out the expansion maps and modes when they are clear.
        writer.WriteUInt8(FeatureFlags.ExpansionByte);
        return writer.Build();
    }
}

/// <summary>Feature bits the client reads one byte past the character grid.</summary>
public static class FeatureFlags
{
    /// <summary>Lets the client offer expansion content such as Team Sneaking.</summary>
    public const int ExpansionByte = 0x0f;

    /// <summary>
    /// Map, rule and expansion availability mask. The client reads it as a bit
    /// field in which bit 0 through bit 55 each stand for one selectable map or
    /// rule, and it offers the real row for a set bit and a greyed row whose
    /// name is the shipped <c>????</c> translation for a clear one. Every bit is
    /// set so the whole catalogue is offered; the trailing nine bytes are past
    /// the highest bit the client ever tests.
    /// </summary>
    public static readonly byte[] ContentMask =
    [
        0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];
}

/// <summary>Serves the personal-information screen data for the session character.</summary>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetPersonalInfoHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            await sessionHelper.SendPacketAsync(session, CommandConstants.GetPersonalInfo, null, cancellationToken);
            return;
        }

        var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
        var appearance = await characterService.GetAppearanceAsync(characterIdentifier, cancellationToken);
        var skills = await characterService.GetEquippedSkillsAsync(characterIdentifier, cancellationToken);
        var clan = await characterService.GetClanInformationAsync(characterIdentifier, cancellationToken);

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetPersonalInfo,
            CharacterPayloadBuilder.BuildPersonalInfoPayload(
                character,
                appearance,
                skills,
                clan,
                characterIdentifier),
            cancellationToken);
    }
}
