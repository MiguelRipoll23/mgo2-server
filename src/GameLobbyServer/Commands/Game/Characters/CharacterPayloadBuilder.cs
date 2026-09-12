using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Builds the fixed-layout payloads that describe a character's skills, saved
/// sets and chat macros. The connect burst and the on-demand commands share
/// them so a field added in one place reaches both.
/// </summary>
public static class CharacterPayloadBuilder
{
    /// <summary>Length of a chat macro text field.</summary>
    public const int MacroTextLength = 64;

    /// <summary>Number of macros per page.</summary>
    public const int MacrosPerType = 12;

    /// <summary>Exact size of a chat-macro page payload.</summary>
    public const int MacroPayloadSize = 0x301;

    /// <summary>Length of a saved set name field.</summary>
    public const int SetNameLength = 63;

    /// <summary>Number of saved skill and gear sets.</summary>
    public const int SetCount = 3;

    /// <summary>Builds the skill catalogue payload: every skill at its maximum level.</summary>
    public static byte[] BuildSkillsPayload()
    {
        var skills = CharacterSkillCatalogue.CreateMaximumLevelSkills();
        var writer = new PacketWriter();
        writer.WriteUInt32((uint)skills.Count);
        foreach (var skill in skills)
        {
            writer.WriteUInt8(skill.SkillIdentifier);
            writer.WriteUInt16(skill.Experience);
            writer.WriteUInt8(skill.Flag);
        }

        return writer.Build();
    }

    /// <summary>Builds one page of chat macros.</summary>
    /// <param name="macros">Stored macros of the character.</param>
    /// <param name="type">Page index to build.</param>
    public static byte[] BuildChatMacrosPayload(IReadOnlyList<CharacterChatMacro> macros, int type)
    {
        var writer = new PacketWriter();
        writer.WriteUInt8(type);

        var pageMacros = macros
            .Where(macro => macro.Type == type)
            .OrderBy(macro => macro.Index)
            .ToList();

        for (var index = 0; index < MacrosPerType; index++)
        {
            var macro = pageMacros.FirstOrDefault(entry => entry.Index == index);
            writer.WriteFixedString(macro?.Text ?? string.Empty, MacroTextLength);
        }

        writer.WritePadding(MacroPayloadSize - 1 - MacrosPerType * MacroTextLength);
        return writer.Build();
    }

    /// <summary>Builds the saved skill sets payload.</summary>
    /// <param name="sets">Stored skill sets of the character.</param>
    public static byte[] BuildSkillSetsPayload(IReadOnlyList<CharacterSkillSet> sets)
    {
        var writer = new PacketWriter();

        for (var index = 0; index < SetCount; index++)
        {
            var set = sets.FirstOrDefault(entry => entry.Index == index);
            writer.WriteUInt32((uint)(set?.Modes ?? 0));
            writer.WriteUInt8(set?.Skill1 ?? 0);
            writer.WriteUInt8(set?.Skill2 ?? 0);
            writer.WriteUInt8(set?.Skill3 ?? 0);
            writer.WriteUInt8(set?.Skill4 ?? 0);
            writer.WriteUInt8(0);
            // Levels are not read back from storage: every skill is at its
            // maximum, so each assigned slot serves that maximum.
            writer.WriteUInt8(set is null ? 0 : CharacterSkillCatalogue.SkillLevelAtMaximum(set.Skill1));
            writer.WriteUInt8(set is null ? 0 : CharacterSkillCatalogue.SkillLevelAtMaximum(set.Skill2));
            writer.WriteUInt8(set is null ? 0 : CharacterSkillCatalogue.SkillLevelAtMaximum(set.Skill3));
            writer.WriteUInt8(set is null ? 0 : CharacterSkillCatalogue.SkillLevelAtMaximum(set.Skill4));
            writer.WriteUInt8(0);
            writer.WriteFixedString(set?.Name ?? string.Empty, SetNameLength);
        }

        return writer.Build();
    }

    /// <summary>Fixed block that follows the clan header of the personal-information payload.</summary>
    private static readonly byte[] PersonalInfoFixedBytes =
    [
        0x01, 0x00, 0x00, 0x00, 0x0c, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,
    ];

    /// <summary>Trailing bytes of the personal-information payload.</summary>
    private static readonly byte[] PersonalInfoTrailer = [0x00, 0xa7, 0x00, 0x0d];

    /// <summary>Builds the personal-information payload.</summary>
    /// <param name="character">Character the payload describes, or <c>null</c>.</param>
    /// <param name="appearance">Appearance of the character, when it has one.</param>
    /// <param name="skills">Equipped skills of the character, when it has any.</param>
    /// <param name="clan">Clan of the character, when it belongs to one.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    public static byte[] BuildPersonalInfoPayload(
        Character? character,
        CharacterAppearance? appearance,
        CharacterEquippedSkill? skills,
        CharacterClanInformation? clan,
        int characterIdentifier)
    {
        var writer = new PacketWriter();

        // Clan header: identifier plus name, zeroed when the character has none.
        writer.WriteUInt32((uint)(clan?.ClanIdentifier ?? 0));
        writer.WriteFixedString(clan?.ClanName ?? string.Empty, 16);
        writer.WriteBytes(PersonalInfoFixedBytes);
        writer.WriteUInt32((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        if (appearance is not null)
        {
            writer.WriteUInt8(appearance.Gender);
            writer.WriteUInt8(appearance.Face);
            writer.WriteUInt8(appearance.Upper);
            writer.WriteUInt8(appearance.Lower);
            writer.WriteUInt8(appearance.FacePaint);
            writer.WriteUInt8(appearance.UpperColor);
            writer.WriteUInt8(appearance.LowerColor);
            writer.WriteUInt8(appearance.Voice);
            writer.WriteUInt8(appearance.Pitch);
            writer.WritePadding(4);
            writer.WriteUInt8(appearance.Head);
            writer.WriteUInt8(appearance.Chest);
            writer.WriteUInt8(appearance.Hands);
            writer.WriteUInt8(appearance.Waist);
            writer.WriteUInt8(appearance.Feet);
            writer.WriteUInt8(appearance.Accessory1);
            writer.WriteUInt8(appearance.Accessory2);
            writer.WriteUInt8(appearance.HeadColor);
            writer.WriteUInt8(appearance.ChestColor);
            writer.WriteUInt8(appearance.HandsColor);
            writer.WriteUInt8(appearance.WaistColor);
            writer.WriteUInt8(appearance.FeetColor);
            writer.WriteUInt8(appearance.Accessory1Color);
            writer.WriteUInt8(appearance.Accessory2Color);
        }
        else
        {
            writer.WritePadding(24);
        }

        if (skills is not null)
        {
            writer.WriteUInt8(skills.Skill1);
            writer.WriteUInt8(skills.Skill2);
            writer.WriteUInt8(skills.Skill3);
            writer.WriteUInt8(skills.Skill4);
            writer.WriteUInt8(0);
            // Levels are clamped to the catalogue maximum: the client zeroes
            // any slot whose level exceeds the experience it is sent, and the
            // experience below is always the maximum.
            writer.WriteUInt8(ClampLevel(skills.Skill1, skills.Level1));
            writer.WriteUInt8(ClampLevel(skills.Skill2, skills.Level2));
            writer.WriteUInt8(ClampLevel(skills.Skill3, skills.Level3));
            writer.WriteUInt8(ClampLevel(skills.Skill4, skills.Level4));
            writer.WriteUInt8(0);
        }
        else
        {
            writer.WritePadding(10);
        }

        for (var slot = 0; slot < 4; slot++)
        {
            writer.WriteUInt32((uint)CharacterSkillCatalogue.MaximumSkillExperience);
        }

        writer.WritePadding(5);
        writer.WriteUInt32((uint)characterIdentifier);
        writer.WriteFixedString(character?.Comment ?? string.Empty, 128);
        writer.WriteUInt8(character?.Rank ?? 0);
        // Clan emblem flag: three when the clan published an emblem.
        writer.WriteUInt8(clan?.HasEmblem == true ? 3 : 0);
        writer.WriteBytes(PersonalInfoTrailer);
        return writer.Build();
    }

    /// <summary>Clamps a stored skill level to the catalogue maximum for its skill.</summary>
    /// <param name="skillIdentifier">Identifier of the skill.</param>
    /// <param name="level">Stored level.</param>
    private static int ClampLevel(int skillIdentifier, int level) =>
        Math.Min(Math.Max(level, 0), CharacterSkillCatalogue.SkillLevelAtMaximum(skillIdentifier));

    /// <summary>Builds the saved gear sets payload.</summary>
    /// <param name="sets">Stored gear sets of the character.</param>
    public static byte[] BuildGearSetsPayload(IReadOnlyList<CharacterGearSet> sets)
    {
        var writer = new PacketWriter();

        for (var index = 0; index < SetCount; index++)
        {
            var set = sets.FirstOrDefault(entry => entry.Index == index);
            writer.WriteUInt32((uint)(set?.Stages ?? 0));
            writer.WriteUInt8(set?.Face ?? 0);
            writer.WriteUInt8(set?.Head ?? 0);
            writer.WriteUInt8(set?.Upper ?? 0);
            writer.WriteUInt8(set?.Lower ?? 0);
            writer.WriteUInt8(set?.Chest ?? 0);
            writer.WriteUInt8(set?.Waist ?? 0);
            writer.WriteUInt8(set?.Hands ?? 0);
            writer.WriteUInt8(set?.Feet ?? 0);
            writer.WriteUInt8(set?.Accessory1 ?? 0);
            writer.WriteUInt8(set?.Accessory2 ?? 0);
            writer.WriteUInt8(set?.HeadColor ?? 0);
            writer.WriteUInt8(set?.UpperColor ?? 0);
            writer.WriteUInt8(set?.LowerColor ?? 0);
            writer.WriteUInt8(set?.ChestColor ?? 0);
            writer.WriteUInt8(set?.WaistColor ?? 0);
            writer.WriteUInt8(set?.HandsColor ?? 0);
            writer.WriteUInt8(set?.FeetColor ?? 0);
            writer.WriteUInt8(set?.Accessory1Color ?? 0);
            writer.WriteUInt8(set?.Accessory2Color ?? 0);
            writer.WriteUInt8(set?.FacePaint ?? 0);
            writer.WriteFixedString(set?.Name ?? string.Empty, SetNameLength);
        }

        return writer.Build();
    }
}
