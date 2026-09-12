using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// The progression half of the character service: the equipped skills, the
/// experience a round earns, the rank derived from it and the clan a character
/// belongs to.
/// </summary>
public sealed partial class CharacterService
{
    /// <summary>Returns the equipped skills of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<CharacterEquippedSkill?> GetEquippedSkillsAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.CharacterEquippedSkills
            .AsNoTracking()
            .FirstOrDefaultAsync(skills => skills.CharacterIdentifier == characterIdentifier, cancellationToken);
    }

    /// <summary>Creates or replaces the equipped skills of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="update">Changes to apply to the equipped skills.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdateEquippedSkillsAsync(
        int characterIdentifier,
        Action<CharacterEquippedSkill> update,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var skills = await context.CharacterEquippedSkills
            .FirstOrDefaultAsync(row => row.CharacterIdentifier == characterIdentifier, cancellationToken);

        if (skills is null)
        {
            skills = new CharacterEquippedSkill { CharacterIdentifier = characterIdentifier };
            update(skills);
            context.CharacterEquippedSkills.Add(skills);
        }
        else
        {
            update(skills);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Recalculates and stores the animal rank of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="statistics">Statistics the rank is derived from.</param>
    /// <param name="daysSinceLastLogin">Days since the character last logged in.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdateRankAsync(
        int characterIdentifier,
        CharacterStatistics statistics,
        int daysSinceLastLogin = 0,
        CancellationToken cancellationToken = default)
    {
        var rank = AnimalRankService.CalculateRank(statistics, daysSinceLastLogin);

        await using var context = await CreateContextAsync(cancellationToken);
        await context.Characters
            .Where(character => character.Identifier == characterIdentifier)
            .ExecuteUpdateAsync(setters => setters.SetProperty(character => character.Rank, rank), cancellationToken);
    }

    /// <summary>Returns the clan of a character, when it belongs to one.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<CharacterClanInformation?> GetClanInformationAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.ClanMembers
            .AsNoTracking()
            .Where(member => member.CharacterIdentifier == characterIdentifier)
            .Join(
                context.Clans,
                member => member.ClanIdentifier,
                clan => clan.Identifier,
                (_, clan) => new CharacterClanInformation(clan.Identifier, clan.Name, clan.Emblem != null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Stores the experience of a character: the reported amount, or sixty
    /// points less than the stored value for an aborted round.
    /// </summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="amount">Experience reported for the round.</param>
    /// <param name="aborted">Whether the round was aborted.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task AddExperienceAsync(
        int characterIdentifier,
        int amount,
        bool aborted = false,
        CancellationToken cancellationToken = default)
    {
        var character = await FindByIdAsync(characterIdentifier, cancellationToken);
        if (character is null)
        {
            return;
        }

        var experience = aborted ? Math.Max(0, character.Experience - 60) : amount;

        await using var context = await CreateContextAsync(cancellationToken);
        await context.Characters
            .Where(row => row.Identifier == characterIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(row => row.Experience, experience),
                cancellationToken);
    }

    /// <summary>Derives the level shown for an experience total.</summary>
    /// <param name="experience">Experience of the character.</param>
    public static int CalculateLevel(int experience) => experience switch
    {
        < 125 => 0,
        < 250 => 1,
        < 375 => 2,
        < 500 => 3,
        < 650 => 4,
        < 800 => 5,
        < 950 => 6,
        < 1100 => 7,
        < 1250 => 8,
        < 1400 => 9,
        < 1550 => 10,
        < 1700 => 11,
        < 1850 => 12,
        < 2000 => 13,
        < 2175 => 14,
        < 2350 => 15,
        < 2525 => 16,
        < 2725 => 17,
        < 2925 => 18,
        < 3275 => 19,
        _ => 20,
    };
}
