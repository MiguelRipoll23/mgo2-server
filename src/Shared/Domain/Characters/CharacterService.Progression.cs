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

    /// <summary>
    /// Stores the rank a character wears. The value comes from the title service,
    /// which latches it: a rank that was earned is never taken away by a later bad
    /// week, so this is not a recomputation of anything.
    /// </summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="rank">Rank identifier to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SetRankAsync(
        int characterIdentifier,
        int rank,
        CancellationToken cancellationToken = default)
    {
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
    /// Largest experience a character can hold: the round report carries the total
    /// as a zero-extended <c>u16</c>, so nothing above it could be reported back.
    /// </summary>
    public const int MaximumExperience = 65535;

    /// <summary>
    /// Stores the experience of a character: the reported total, or sixty points less
    /// than the stored value for an aborted round.
    /// <para>
    /// The reported value is an absolute total, not a delta, so it is stored as it
    /// arrives — and a decrease is legitimate, because experience in this game can go
    /// down. It is clamped to the wire's own range rather than trusted, which is also
    /// what the schema states as a check constraint. The aborted dock is operator
    /// policy rather than protocol and is knowingly kept.
    /// </para>
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

        var experience = aborted
            ? Math.Max(0, character.Experience - 60)
            : Math.Clamp(amount, 0, MaximumExperience);

        await using var context = await CreateContextAsync(cancellationToken);
        await context.Characters
            .Where(row => row.Identifier == characterIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(row => row.Experience, experience),
                cancellationToken);
    }
}
