namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>One entry of the maximum-level skill catalogue.</summary>
/// <param name="SkillIdentifier">Identifier of the skill.</param>
/// <param name="Experience">Experience the catalogue advertises for the skill.</param>
/// <param name="Flag">Trailing flag carried by the wire entry.</param>
public readonly record struct MaximumLevelSkill(int SkillIdentifier, int Experience, int Flag);

/// <summary>
/// The skill catalogue every character is served. Skill levels are no longer
/// persisted, so this catalogue is the character's entire skill state: each
/// defined skill is advertised at its maximum level.
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

    /// <summary>Experience advertised for skills that have no meaningful progression path.</summary>
    public const int SkillExperienceWithoutPath = 0x2000;

    /// <summary>
    /// Skills with no meaningful experience path: the instructor skill, which
    /// renders without a level bar, and the two unlockable expert skills.
    /// </summary>
    private static readonly int[] SkillsWithoutPath = [17, 20, 22];

    /// <summary>Builds the catalogue: every defined skill at its maximum level.</summary>
    public static List<MaximumLevelSkill> CreateMaximumLevelSkills()
    {
        var skills = new List<MaximumLevelSkill>(DefinedSkillCount);
        for (var index = 0; index < DefinedSkillCount; index++)
        {
            var skillIdentifier = index + 1;
            skills.Add(new MaximumLevelSkill(
                skillIdentifier,
                HasProgressionPath(skillIdentifier) ? MaximumSkillExperience : SkillExperienceWithoutPath,
                0));
        }

        return skills;
    }

    /// <summary>Derives the level the client shows for a skill experience value.</summary>
    /// <param name="experience">Experience of the skill.</param>
    public static int SkillLevelFromExperience(int experience) =>
        Math.Min(Math.Max(0, experience) >> 13, SkillLevelCap);

    /// <summary>Maximum level of a skill identifier; zero for unassigned slots and undefined identifiers.</summary>
    /// <param name="skillIdentifier">Identifier of the skill.</param>
    public static int SkillLevelAtMaximum(int skillIdentifier)
    {
        if (skillIdentifier < 1 || skillIdentifier > DefinedSkillCount)
        {
            return 0;
        }

        return HasProgressionPath(skillIdentifier)
            ? SkillLevelCap
            : SkillExperienceWithoutPath >> 13;
    }

    /// <summary>Returns whether a skill accumulates experience towards its level.</summary>
    /// <param name="skillIdentifier">Identifier of the skill.</param>
    public static bool HasProgressionPath(int skillIdentifier) => !SkillsWithoutPath.Contains(skillIdentifier);
}
