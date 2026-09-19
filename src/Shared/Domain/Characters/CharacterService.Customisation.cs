using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// The customisation half of the character service: the loadouts and settings a
/// character saves, which the client re-reads on every connect.
/// </summary>
public sealed partial class CharacterService
{
    /// <summary>Lists the saved skill sets of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<CharacterSkillSet>> GetSkillSetsAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.CharacterSkillSets
            .AsNoTracking()
            .Where(skillSet => skillSet.CharacterIdentifier == characterIdentifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Replaces the saved skill sets of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="sets">Skill sets to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdateSkillSetsAsync(
        int characterIdentifier,
        IEnumerable<CharacterSkillSet> sets,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.CharacterSkillSets
            .Where(skillSet => skillSet.CharacterIdentifier == characterIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var set in sets)
        {
            set.CharacterIdentifier = characterIdentifier;
            context.CharacterSkillSets.Add(set);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Lists the saved gear sets of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<CharacterGearSet>> GetGearSetsAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.CharacterGearSets
            .AsNoTracking()
            .Where(gearSet => gearSet.CharacterIdentifier == characterIdentifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Replaces the saved gear sets of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="sets">Gear sets to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdateGearSetsAsync(
        int characterIdentifier,
        IEnumerable<CharacterGearSet> sets,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.CharacterGearSets
            .Where(gearSet => gearSet.CharacterIdentifier == characterIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var set in sets)
        {
            set.CharacterIdentifier = characterIdentifier;
            context.CharacterGearSets.Add(set);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Lists the chat macros of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<CharacterChatMacro>> GetChatMacrosAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.CharacterChatMacros
            .AsNoTracking()
            .Where(macro => macro.CharacterIdentifier == characterIdentifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Replaces the chat macros of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="macros">Macros to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdateChatMacrosAsync(
        int characterIdentifier,
        IEnumerable<CharacterChatMacro> macros,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.CharacterChatMacros
            .Where(macro => macro.CharacterIdentifier == characterIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var macro in macros)
        {
            macro.CharacterIdentifier = characterIdentifier;
            context.CharacterChatMacros.Add(macro);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Lists the host settings a character saved.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<CharacterHostSettings>> GetHostSettingsAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.CharacterHostSettings
            .AsNoTracking()
            .Where(settings => settings.CharacterIdentifier == characterIdentifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Replaces the host settings of one game type for a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="type">Game type the settings apply to.</param>
    /// <param name="settings">Host settings entity to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdateHostSettingsAsync(
        int characterIdentifier,
        short type,
        CharacterHostSettings settings,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.CharacterHostSettings
            .Where(row => row.CharacterIdentifier == characterIdentifier && row.Type == type)
            .ExecuteDeleteAsync(cancellationToken);

        settings.CharacterIdentifier = characterIdentifier;
        settings.Type = type;
        context.CharacterHostSettings.Add(settings);

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the gameplay options of a character, or <c>null</c> when it has never
    /// stored any — which is a different state from having stored the defaults.
    /// </summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<CharacterGameplayOptions?> GetGameplayOptionsAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.CharacterGameplayOptions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                options => options.CharacterIdentifier == characterIdentifier,
                cancellationToken);
    }

    /// <summary>
    /// Stores the gameplay options of a character, replacing every row it has: the
    /// write-back the client sends is the whole of its settings, so a merge would keep
    /// a setting the player had just cleared.
    /// </summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="options">Settings to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdateGameplayOptionsAsync(
        int characterIdentifier,
        CharacterGameplayOptions options,
        CancellationToken cancellationToken = default)
    {
        options.CharacterIdentifier = characterIdentifier;

        await using var context = await CreateContextAsync(cancellationToken);
        await context.CharacterGameplayOptions
            .Where(row => row.CharacterIdentifier == characterIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        context.CharacterGameplayOptions.Add(options);
        await context.SaveChangesAsync(cancellationToken);
    }
}
