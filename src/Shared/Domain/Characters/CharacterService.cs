using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>Fields accepted when a character is created.</summary>
/// <param name="AccountIdentifier">Account the character belongs to.</param>
/// <param name="Name">Name of the character.</param>
/// <param name="CreatedAt">Timestamp the character was created at.</param>
public sealed record CharacterCreateInput(
    int AccountIdentifier,
    string Name,
    DateTimeOffset CreatedAt);

/// <summary>A friends or blocked entry together with the name it points at.</summary>
/// <param name="TargetIdentifier">Character the entry refers to.</param>
/// <param name="TargetName">Name of the character the entry refers to.</param>
/// <param name="Type">Kind of entry: friend or blocked.</param>
public sealed record CharacterFriendEntry(int TargetIdentifier, string TargetName, int Type);

/// <summary>Clan membership summary of a character.</summary>
/// <param name="ClanIdentifier">Identifier of the clan.</param>
/// <param name="ClanName">Name of the clan.</param>
/// <param name="HasEmblem">Whether the clan published an emblem.</param>
public sealed record CharacterClanInformation(int ClanIdentifier, string ClanName, bool HasEmblem);

/// <summary>
/// Owns the character records and everything attached to them: appearance,
/// friends, skill and gear sets, host settings, chat macros and experience. The
/// customisation and progression operations live in the other halves of this
/// class.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed partial class CharacterService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Lists the active characters of an account.</summary>
    /// <param name="accountIdentifier">Identifier of the account.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<Character>> FindByAccountIdentifierAsync(
        int accountIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Characters
            .AsNoTracking()
            .Where(character => character.AccountIdentifier == accountIdentifier && character.Active)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Finds a character by its identifier.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Character?> FindByIdAsync(int characterIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(character => character.Identifier == characterIdentifier, cancellationToken);
    }

    /// <summary>Finds a character by its name.</summary>
    /// <param name="name">Name of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Character?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(character => character.Name == name, cancellationToken);
    }

    /// <summary>
    /// Level every newly registered character starts at.
    /// <para>
    /// Operator policy rather than protocol, and the whole of the rule: the game owns
    /// only the table a level is derived from, so there is no starting level in it to
    /// read. A lobby marked beginners-only still refuses whoever is past its own
    /// ceiling, which is three, so a character born here is past that door on purpose.
    /// </para>
    /// </summary>
    public const int StartingLevel = 12;

    /// <summary>
    /// Experience a newly registered character is given: the first total that displays
    /// as <see cref="StartingLevel"/>, so a character is the level it is meant to be
    /// from the moment it exists rather than a value near it.
    /// </summary>
    public static readonly int StartingExperience = LevelUtils.ExperienceAtLevel(StartingLevel);

    /// <summary>
    /// Creates a character together with its appearance. The character is born at the
    /// starting experience rather than at zero, which is the only place a level is
    /// granted without a round being played for it.
    /// </summary>
    /// <param name="input">Fields of the new character.</param>
    /// <param name="appearance">Fields of the new appearance.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Character> CreateAsync(
        CharacterCreateInput input,
        Action<CharacterAppearance> appearance,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var character = new Character
        {
            AccountIdentifier = input.AccountIdentifier,
            Name = input.Name,
            CreatedAt = input.CreatedAt,
            Experience = StartingExperience,
        };

        context.Characters.Add(character);
        await context.SaveChangesAsync(cancellationToken);

        var createdAppearance = new CharacterAppearance { CharacterIdentifier = character.Identifier };
        appearance(createdAppearance);
        context.CharacterAppearances.Add(createdAppearance);
        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return character;
    }

    /// <summary>
    /// Prefix of the placeholder name a deleted character is parked under, which also
    /// keeps players from claiming a name a tombstone already holds.
    /// </summary>
    public const string DeletedNamePrefix = ":#";

    /// <summary>
    /// How long a character has to have existed before it can be deleted.
    /// <para>
    /// The client is told the remaining time rather than only being refused — the
    /// character list carries it per slot, and the client draws its own "you must wait"
    /// screen from it — so the two have to agree on the span as well as on the check.
    /// </para>
    /// </summary>
    public static readonly TimeSpan DeletionCooldown = TimeSpan.FromDays(7);

    /// <summary>Seconds until a character may be deleted, or zero once it already may be.</summary>
    /// <param name="createdAt">Moment the character was created.</param>
    /// <param name="now">Moment the question is asked at.</param>
    public static int SecondsUntilDeletable(DateTimeOffset createdAt, DateTimeOffset now)
    {
        // Measured as the age elapsed rather than as the instant it expires, because the
        // latter would overflow for a creation time near the end of the range — and this
        // runs while a reply is being built, where a throw costs the whole list rather
        // than one badly-counted field.
        var age = now - createdAt;
        if (age >= DeletionCooldown)
        {
            return 0;
        }

        // Clamped rather than cast: a row whose creation time reads as far in the future
        // would otherwise wrap into a negative wait that the client renders as already
        // expired.
        return (int)Math.Min((long)(DeletionCooldown - age).TotalSeconds, int.MaxValue);
    }

    /// <summary>Whether a character has existed long enough to be deleted.</summary>
    /// <param name="createdAt">Moment the character was created.</param>
    /// <param name="now">Moment the question is asked at.</param>
    public static bool CanDelete(DateTimeOffset createdAt, DateTimeOffset now) =>
        SecondsUntilDeletable(createdAt, now) == 0;

    /// <summary>
    /// Marks a character as deleted. The row stays, hidden and renamed, and the name it
    /// held becomes available again; the original is kept in
    /// <see cref="Character.OldName"/>.
    /// <para>
    /// The account's pointers to the character are cleared in the same transaction. They
    /// are what the list orders by and marks the selection with, and a soft delete never
    /// trips the foreign key that would otherwise clear them: left behind, the account
    /// would go on naming a character nobody can sign into again.
    /// </para>
    /// </summary>
    /// <param name="accountIdentifier">Identifier of the account owning the character.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SoftDeleteAsync(
        int accountIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var character = await context.Characters
            .FirstOrDefaultAsync(row => row.Identifier == characterIdentifier, cancellationToken);

        if (character is null)
        {
            return;
        }

        character.Active = false;
        character.OldName = character.Name;
        character.Name = $"{DeletedNamePrefix}{characterIdentifier}";

        var account = await context.Accounts
            .FirstOrDefaultAsync(row => row.Identifier == accountIdentifier, cancellationToken);

        if (account is not null)
        {
            if (account.MainCharacterIdentifier == characterIdentifier)
            {
                account.MainCharacterIdentifier = null;
            }

            if (account.CurrentCharacterIdentifier == characterIdentifier)
            {
                account.CurrentCharacterIdentifier = null;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Returns the appearance of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<CharacterAppearance?> GetAppearanceAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.CharacterAppearances
            .AsNoTracking()
            .FirstOrDefaultAsync(appearance => appearance.CharacterIdentifier == characterIdentifier, cancellationToken);
    }

    /// <summary>Creates or replaces the appearance of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="update">Changes to apply to the appearance.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdateAppearanceAsync(
        int characterIdentifier,
        Action<CharacterAppearance> update,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var appearance = await context.CharacterAppearances
            .FirstOrDefaultAsync(row => row.CharacterIdentifier == characterIdentifier, cancellationToken);

        if (appearance is null)
        {
            appearance = new CharacterAppearance { CharacterIdentifier = characterIdentifier };
            update(appearance);
            context.CharacterAppearances.Add(appearance);
        }
        else
        {
            update(appearance);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stores the free-text comment shown on a character's card.
    /// <para>
    /// This is written by the same write-back as the appearance, and it has to be
    /// stored there for the same reason: the reply echoes what was sent, so an
    /// unstored comment looks correct on the screen that set it and is gone on the
    /// next connect burst, which reads the record rather than the echo.
    /// </para>
    /// </summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="comment">Comment to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdateCommentAsync(
        int characterIdentifier,
        string comment,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Characters
            .Where(character => character.Identifier == characterIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(character => character.Comment, comment),
                cancellationToken);
    }

    /// <summary>Lists the friends and blocked entries of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<CharacterFriend>> GetFriendsAndBlockedAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.CharacterFriends
            .AsNoTracking()
            .Where(friend => friend.CharacterIdentifier == characterIdentifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Lists the friends and blocked entries of a character with the names they point at.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<CharacterFriendEntry>> GetFriendsAndBlockedWithNamesAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.CharacterFriends
            .AsNoTracking()
            .Where(friend => friend.CharacterIdentifier == characterIdentifier)
            .Join(
                context.Characters,
                friend => friend.TargetIdentifier,
                character => character.Identifier,
                (friend, character) => new CharacterFriendEntry(
                    friend.TargetIdentifier,
                    character.Name,
                    friend.Type))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Adds a friend or blocked entry.</summary>
    /// <param name="characterIdentifier">Identifier of the character owning the entry.</param>
    /// <param name="targetIdentifier">Character the entry refers to.</param>
    /// <param name="type">Kind of entry.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task AddFriendOrBlockedAsync(
        int characterIdentifier,
        int targetIdentifier,
        int type,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        context.CharacterFriends.Add(new CharacterFriend
        {
            CharacterIdentifier = characterIdentifier,
            TargetIdentifier = targetIdentifier,
            Type = type,
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Removes a friend or blocked entry.</summary>
    /// <param name="characterIdentifier">Identifier of the character owning the entry.</param>
    /// <param name="targetIdentifier">Character the entry refers to.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task RemoveFriendOrBlockedAsync(
        int characterIdentifier,
        int targetIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.CharacterFriends
            .Where(friend => friend.CharacterIdentifier == characterIdentifier && friend.TargetIdentifier == targetIdentifier)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
