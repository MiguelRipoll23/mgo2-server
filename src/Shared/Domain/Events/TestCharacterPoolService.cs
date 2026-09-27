using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>One test character in the pool, and whether a team holds it.</summary>
/// <param name="Identifier">Row identifier of the character.</param>
/// <param name="Name">Name the character is shown with.</param>
/// <param name="InTeam">Whether any team's roster currently holds it.</param>
public readonly record struct TestCharacterPoolEntry(int Identifier, string Name, bool InTeam);

/// <summary>
/// The pool of characters the testing tools draw on: real rows, named with the
/// marker prefix, owned by the server account.
/// <para>
/// The pool is managed rather than implicit. Creating a team or filling one
/// takes the players it needs from here instead of minting new characters every
/// time, so the same characters are reused across a whole testing session and
/// the character table does not grow with every attempt. A character in a team
/// is reported as taken; one whose team was removed is free again.
/// </para>
/// <para>
/// The characters belong to the server account — the same account the gameplay
/// server presents — so nothing here needs the account to have an owner who can
/// sign in, and a test character never lands in a player's own account.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="options">Options naming the server account.</param>
public sealed class TestCharacterPoolService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    IOptions<ServerOptions> options)
    : DomainService(contextFactory)
{
    /// <summary>Experience test characters are shown with, roughly a mid-level rank.</summary>
    public const int TestExperience = 18_000;

    private readonly string accountName = options.Value.GameplayServerAccountName;

    /// <summary>Lists the pool, oldest first, marking the characters a team holds.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<IReadOnlyList<TestCharacterPoolEntry>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var characters = await context.Characters
            .AsNoTracking()
            .Where(character => character.Name.StartsWith(TestPlayerNameUtils.Prefix))
            .OrderBy(character => character.Identifier)
            .Select(character => new { character.Identifier, character.Name })
            .ToListAsync(cancellationToken);

        var taken = await TakenAsync(context, cancellationToken);

        return
        [
            .. characters.Select(character => new TestCharacterPoolEntry(
                character.Identifier,
                character.Name,
                taken.Contains(character.Identifier)))
        ];
    }

    /// <summary>
    /// Adds test characters to the pool under the server account, named with the
    /// next free numbers.
    /// </summary>
    /// <param name="count">How many to add.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>How many were added, or zero when the account is missing or the count is refused.</returns>
    public async Task<int> AddAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count < 1 || count > TestPlayerNameUtils.MaximumPerAdd)
        {
            return 0;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var accountIdentifier = await context.Accounts
            .Where(account => account.DisplayName == accountName)
            .Select(account => account.Identifier)
            .FirstOrDefaultAsync(cancellationToken);
        if (accountIdentifier <= 0)
        {
            return 0;
        }

        var used = await context.Characters
            .Where(character => character.Name.StartsWith(TestPlayerNameUtils.Prefix))
            .Select(character => character.Name)
            .ToListAsync(cancellationToken);
        var taken = new HashSet<string>(used, StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;
        var number = 1;
        var created = 0;
        for (var index = 0; index < count; index++)
        {
            while (taken.Contains(TestPlayerNameUtils.ComposeName(number)))
            {
                number++;
            }

            var name = TestPlayerNameUtils.ComposeName(number);
            taken.Add(name);
            context.Characters.Add(new Character
            {
                AccountIdentifier = accountIdentifier,
                Name = name,
                CreatedAt = now,
                Experience = TestExperience,
            });
            created++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <summary>
    /// Removes one test character from the pool, and from any roster that holds
    /// it.
    /// </summary>
    /// <param name="characterIdentifier">Character to remove.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether a test character of that identifier was removed.</returns>
    public async Task<bool> DeleteAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (characterIdentifier <= 0)
        {
            return false;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var character = await context.Characters
            .FirstOrDefaultAsync(
                candidate => candidate.Identifier == characterIdentifier
                    && candidate.Name.StartsWith(TestPlayerNameUtils.Prefix),
                cancellationToken);
        if (character is null)
        {
            return false;
        }

        // The rows that name the character are removed first, because the
        // character is the one thing they point at rather than the other way
        // round. A roster row left behind would name a character that is gone.
        context.EventTeamMembers.RemoveRange(
            await context.EventTeamMembers
                .Where(member => member.CharacterIdentifier == characterIdentifier)
                .ToListAsync(cancellationToken));
        context.GamePlayers.RemoveRange(
            await context.GamePlayers
                .Where(player => player.CharacterIdentifier == characterIdentifier)
                .ToListAsync(cancellationToken));
        context.TournamentRosterMembers.RemoveRange(
            await context.TournamentRosterMembers
                .Where(member => member.CharacterIdentifier == characterIdentifier)
                .ToListAsync(cancellationToken));

        context.Characters.Remove(character);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Takes up to <paramref name="count"/> free characters from the pool.</summary>
    /// <param name="count">How many to take.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The characters taken, or fewer when the pool holds fewer free.</returns>
    public async Task<IReadOnlyList<TestCharacterPoolEntry>> TakeAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count < 1)
        {
            return [];
        }

        var pool = await ListAsync(cancellationToken);
        return [.. pool.Where(entry => !entry.InTeam).Take(count)];
    }

    private static async Task<HashSet<int>> TakenAsync(
        Mgo2DatabaseContext context,
        CancellationToken cancellationToken)
    {
        var identifiers = await context.EventTeamMembers
            .AsNoTracking()
            .Select(member => member.CharacterIdentifier)
            .Distinct()
            .ToListAsync(cancellationToken);
        return [.. identifiers];
    }
}
