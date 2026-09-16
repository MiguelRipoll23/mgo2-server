namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>One entry of the maximum-level skill catalogue.</summary>
/// <param name="SkillIdentifier">Identifier of the skill.</param>
/// <param name="Experience">Experience the catalogue advertises for the skill.</param>
/// <param name="Flag">Trailing flag carried by the wire entry.</param>
public readonly record struct MaximumLevelSkill(int SkillIdentifier, int Experience, int Flag);

/// <summary>
/// The skill catalogue every character is served: every skill this build defines,
/// at the highest experience the client accepts.
/// <para>
/// Skill progression is deliberately not persisted, so the catalogue is the whole
/// of a character's skill state rather than a floor under a stored value. Because
/// the maximum is also the value the client clamps to, a stored report could never
/// be served differently from what is advertised here, which is why no table for
/// one exists.
/// </para>
/// <para>
/// Three skills used to be served below the maximum — the instructor skill, which
/// renders without a level bar, and the two unlockable expert skills — on a list
/// inherited from another server with no evidence behind it. Skill 17 is the one
/// every training and graduation check reads, so serving it below the maximum was
/// the most plausible reason a graduation requirement could fail. Nothing
/// distinguishes those three any more; see <c>BACKLOG.md</c>.
/// </para>
/// </summary>
public static class CharacterSkillCatalogue
{
    /// <summary>
    /// Highest skill experience the client accepts. Its validator zeroes any
    /// skill record above this value, which is exactly level three.
    /// </summary>
    public const int MaximumSkillExperience = 24576;

    /// <summary>Number of skills this client build defines.</summary>
    public const int DefinedSkillCount = 25;

    /// <summary>Highest skill level, derived from the maximum experience.</summary>
    public const int SkillLevelCap = 3;

    /// <summary>
    /// Flag byte carried by every catalogue entry. Skill 17 is the only entry the
    /// client reads it for, where zero is what enables the training menu.
    /// </summary>
    private const int CatalogueFlag = 0;

    /// <summary>Builds the catalogue: every defined skill at its maximum level.</summary>
    public static List<MaximumLevelSkill> CreateMaximumLevelSkills()
    {
        var skills = new List<MaximumLevelSkill>(DefinedSkillCount);
        for (var index = 0; index < DefinedSkillCount; index++)
        {
            skills.Add(new MaximumLevelSkill(index + 1, MaximumSkillExperience, CatalogueFlag));
        }

        return skills;
    }

    /// <summary>Derives the level the client shows for a skill experience value.</summary>
    /// <param name="experience">Experience of the skill.</param>
    public static int SkillLevelFromExperience(int experience) =>
        Math.Min(Math.Max(0, experience) >> 13, SkillLevelCap);

    /// <summary>Level of a skill identifier in the catalogue; zero for unassigned slots and undefined identifiers.</summary>
    /// <param name="skillIdentifier">Identifier of the skill.</param>
    public static int SkillLevelAtMaximum(int skillIdentifier) =>
        skillIdentifier < 1 || skillIdentifier > DefinedSkillCount ? 0 : SkillLevelCap;
}
