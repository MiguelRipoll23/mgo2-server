using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Users;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Serves the connect burst: the character header followed by every catalogue
/// the client needs, in the order it expects them.
/// </summary>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="userService">Service that owns the accounts.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetCharacterInfoHandler(
    CharacterService characterService,
    UserService userService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Fixed grid the client's parser consumes.</summary>
    private const int InfoPayloadSize = 0x142;

    /// <summary>Number of identifiers in each friend and blocked array.</summary>
    private const int MaximumListIdentifiers = 32;

    /// <summary>The four dead 16-bit constants that follow the name.</summary>
    private static readonly byte[] CharacterInfoFixedBytes =
    [
        0x16, 0xae, 0x03, 0x38, 0x01, 0x3e, 0x01, 0x50,
    ];

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var characterIdentifier = session.CharacterIdentifier ?? 0;

        var character = characterIdentifier > 0
            ? await characterService.FindByIdAsync(characterIdentifier, cancellationToken)
            : null;
        var user = session.UserIdentifier is { } userIdentifier
            ? await userService.FindByIdAsync(userIdentifier, cancellationToken)
            : null;
        var friendsAndBlocked = characterIdentifier > 0
            ? await characterService.GetFriendsAndBlockedAsync(characterIdentifier, cancellationToken)
            : [];

        var friends = friendsAndBlocked.Where(entry => entry.Type == 0).Select(entry => entry.TargetIdentifier).ToList();
        var blocked = friendsAndBlocked.Where(entry => entry.Type == 1).Select(entry => entry.TargetIdentifier).ToList();

        var experience = user?.MainExperience ?? 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var lastLogin = character?.CreationTime ?? (int)now;

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetCharacterInfoResult,
            BuildCharacterInfoPayload(characterIdentifier, character?.Name ?? string.Empty, experience, now, lastLogin, friends, blocked),
            cancellationToken);

        // The gameplay options must be populated here: an empty payload makes
        // the client's validator reset every setting to its hardcoded default.
        var storedOptions = GameplayOptionsCodec.ParseStored(character?.GameplayOptions);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetGameplayOptionsResult,
            GameplayOptionsCodec.BuildPayload(storedOptions),
            cancellationToken);

        var macros = characterIdentifier > 0
            ? await characterService.GetChatMacrosAsync(characterIdentifier, cancellationToken)
            : [];

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

        var skillSets = characterIdentifier > 0
            ? await characterService.GetSkillSetsAsync(characterIdentifier, cancellationToken)
            : [];
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetSkillSets,
            CharacterPayloadBuilder.BuildSkillSetsPayload(skillSets),
            cancellationToken);

        var gearSets = characterIdentifier > 0
            ? await characterService.GetGearSetsAsync(characterIdentifier, cancellationToken)
            : [];
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
            CharacterPayloadBuilder.BuildPersonalInfoPayload(character, appearance, skills, clan, characterIdentifier),
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

        writer.WritePadding(InfoPayloadSize - writer.Size);
        // One byte past the grid: the parser reads its four low bits as
        // separate feature flags, and the expansion content is gated on them.
        writer.WriteUInt8(FeatureFlags.ExpansionByte);
        return writer.Build();
    }
}

/// <summary>Feature bits the client reads one byte past the character grid.</summary>
public static class FeatureFlags
{
    /// <summary>Lets the client offer expansion content such as Team Sneaking.</summary>
    public const int ExpansionByte = 0x0f;
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
            CharacterPayloadBuilder.BuildPersonalInfoPayload(character, appearance, skills, clan, characterIdentifier),
            cancellationToken);
    }
}
