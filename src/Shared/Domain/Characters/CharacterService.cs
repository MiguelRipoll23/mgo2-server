using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>Fields accepted when a character is created.</summary>
/// <param name="UserIdentifier">Account the character belongs to.</param>
/// <param name="Name">Name of the character.</param>
/// <param name="CreationTime">Unix timestamp the character was created at.</param>
/// <param name="LobbyIdentifier">Lobby the character starts in, when known.</param>
public sealed record CharacterCreateInput(
    int UserIdentifier,
    string Name,
    int CreationTime,
    int? LobbyIdentifier = null);

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
    /// <param name="userIdentifier">Identifier of the account.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<Character>> FindByUserIdentifierAsync(
        int userIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Characters
            .AsNoTracking()
            .Where(character => character.UserIdentifier == userIdentifier && character.Active == 1)
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

    /// <summary>Creates a character together with its appearance.</summary>
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
            UserIdentifier = input.UserIdentifier,
            Name = input.Name,
            CreationTime = input.CreationTime,
            LobbyIdentifier = input.LobbyIdentifier,
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
    /// Marks a character as deleted, preserving its name so the name cannot be
    /// reused while the row remains.
    /// </summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SoftDeleteAsync(int characterIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var character = await context.Characters
            .FirstOrDefaultAsync(row => row.Identifier == characterIdentifier, cancellationToken);

        if (character is null)
        {
            return;
        }

        character.Active = 0;
        character.OldName = character.Name;
        character.Name = $":#{characterIdentifier}";
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Stores the lobby a character is currently in.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="lobbyIdentifier">Identifier of the lobby, or <c>null</c> to clear it.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SetLobbyAsync(
        int characterIdentifier,
        int? lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Characters
            .Where(character => character.Identifier == characterIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(character => character.LobbyIdentifier, lobbyIdentifier),
                cancellationToken);
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
