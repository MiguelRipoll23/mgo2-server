using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Instructors;
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
/// <param name="titleService">Service that latches the titles the character has earned.</param>
/// <param name="instructorService">Service that owns the saved instructor, which the payload announces.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class GetCharacterInfoHandler(
    CharacterService characterService,
    CharacterTitleService titleService,
    InstructorService instructorService,
    SessionHelper sessionHelper,
    ILogger<GetCharacterInfoHandler> logger) : ICommandHandler
{
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
        // A session with no character and one holding a zero identifier are the
        // same refusal, because neither names a character to serve.
        var characterIdentifier = session.CharacterIdentifier ?? 0;
        if (characterIdentifier <= 0)
        {
            logger.LogWarning(
                "0x4100 cannot be served: the session holds no character. Answering nothing; the client will time out on the connect screen");
            return;
        }

        var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
        if (character is null || !character.Active)
        {
            logger.LogWarning(
                "0x4100 cannot be served: character {CharacterIdentifier} is gone or suspended. Answering nothing",
                characterIdentifier);
            return;
        }

        // The login the character arrived with is what the payload below reports as the
        // previous one, so it is read before the stamp replaces it. The reload the title
        // pass may do would otherwise hand back the stamp this visit just wrote.
        var previousLoginTime = (int)(character.LastSeenAt?.ToUnixTimeSeconds() ?? 0);

        // The visit is stamped first, and the gap it reports is what the title pass is
        // given: one title is unlocked by an absence, so it is measured against the login
        // the character had recorded, which the stamp has just replaced.
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var daysSinceLastLogin = await characterService.RecordLoginAsync(
            characterIdentifier,
            (int)now,
            cancellationToken);

        // Stamping the visit also re-tests the titles — a character who qualified
        // while away is told on the way in — and it runs before the payloads below
        // are built, so a title earned since the last visit is already worn by the
        // record this burst describes.
        var unlocked = await titleService.EvaluateAsync(
            characterIdentifier,
            daysSinceLastLogin,
            cancellationToken);
        if (unlocked.Count > 0)
        {
            logger.LogInformation(
                "Character {CharacterIdentifier} unlocked title {Titles} on entering the lobby, {DaysSinceLastLogin} days after the login before this one",
                characterIdentifier,
                string.Join(',', unlocked),
                daysSinceLastLogin);
            character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken) ?? character;
        }

        var friendsAndBlocked = await characterService.GetFriendsAndBlockedAsync(characterIdentifier, cancellationToken);

        var friends = friendsAndBlocked.Where(entry => entry.Type == 0).Select(entry => entry.TargetIdentifier).ToList();
        var blocked = friendsAndBlocked.Where(entry => entry.Type == 1).Select(entry => entry.TargetIdentifier).ToList();

        // The experience is the character's own, never the account's. The field is the
        // only thing the client derives the displayed level from, and the wire carries
        // it per character, so a pool shared by an account's alts moved every one of
        // their levels together.
        //
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetCharacterInfoResult,
            CharacterInfoPayloadBuilder.Build(
                characterIdentifier,
                character.Name,
                character.Experience,
                previousLoginTime,
                (int)now,
                friends,
                blocked),
            cancellationToken);

        // The gameplay options must be populated here: an empty payload makes
        // the client's validator reset every setting to its hardcoded default. A
        // character with no stored row is served the game's own defaults, which is
        // what the codec does with a null.
        var storedOptions = await characterService.GetGameplayOptionsAsync(characterIdentifier, cancellationToken);
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
        var instructor = await instructorService.FindInstructorAsync(characterIdentifier, cancellationToken);

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetPersonalInfo,
            CharacterPayloadBuilder.BuildPersonalInfoPayload(
                character,
                appearance,
                skills,
                clan,
                instructor?.InstructorCharacterIdentifier ?? CharacterPayloadBuilder.NoSavedInstructor),
            cancellationToken);
    }
}

