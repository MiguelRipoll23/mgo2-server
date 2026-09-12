using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Clans;

/// <summary>A clan list row joined with its leader's character name.</summary>
/// <param name="ClanIdentifier">Identifier of the clan.</param>
/// <param name="ClanName">Name of the clan.</param>
/// <param name="LeaderCharacterIdentifier">Character that leads the clan, or zero.</param>
/// <param name="LeaderCharacterName">Name of the leader, or empty.</param>
/// <param name="HasEmblem">Whether the clan published an emblem.</param>
public sealed record ClanListEntry(
    int ClanIdentifier,
    string ClanName,
    int LeaderCharacterIdentifier,
    string LeaderCharacterName,
    bool HasEmblem);

/// <summary>Full details of one clan, as shown on its card.</summary>
/// <param name="Identifier">Identifier of the clan.</param>
/// <param name="Name">Name of the clan.</param>
/// <param name="Comment">Short comment of the clan.</param>
/// <param name="HasEmblem">Whether the clan published an emblem.</param>
/// <param name="LeaderCharacterIdentifier">Character that leads the clan, or zero.</param>
/// <param name="LeaderCharacterName">Name of the leader, or empty.</param>
/// <param name="MemberCount">Number of members.</param>
public sealed record ClanDetails(
    int Identifier,
    string Name,
    string Comment,
    bool HasEmblem,
    int LeaderCharacterIdentifier,
    string LeaderCharacterName,
    int MemberCount);

/// <summary>A member of a clan joined with its character name.</summary>
/// <param name="MemberIdentifier">Identifier of the membership row.</param>
/// <param name="CharacterIdentifier">Character that is a member.</param>
/// <param name="CharacterName">Name of the member.</param>
public sealed record ClanMemberEntry(int MemberIdentifier, int CharacterIdentifier, string CharacterName);

/// <summary>The clan a character belongs to.</summary>
/// <param name="MemberIdentifier">Identifier of the membership row.</param>
/// <param name="ClanIdentifier">Identifier of the clan.</param>
/// <param name="ClanName">Name of the clan.</param>
/// <param name="IsLeader">Whether the character leads the clan.</param>
/// <param name="HasEmblem">Whether the clan published an emblem.</param>
/// <param name="LeaderMemberIdentifier">Membership row of the leader.</param>
public sealed record ClanMembership(
    int MemberIdentifier,
    int ClanIdentifier,
    string ClanName,
    bool IsLeader,
    bool HasEmblem,
    int? LeaderMemberIdentifier);

/// <summary>A pending application to join a clan.</summary>
/// <param name="CharacterIdentifier">Character that applied.</param>
/// <param name="Name">Name of the applicant.</param>
/// <param name="AppliedAt">Moment the application was submitted.</param>
public sealed record ClanApplicant(int CharacterIdentifier, string Name, DateTime AppliedAt);

/// <summary>
/// Owns the clans, their membership rows, their emblems and the pending
/// applications to join them. The membership, query and application operations
/// live in the other halves of this class.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed partial class ClanService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Lists clans, oldest first.</summary>
    /// <param name="offset">Number of rows to skip.</param>
    /// <param name="limit">Maximum number of rows to return.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<Clan>> FindAllAsync(
        int offset = 0,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Clans
            .AsNoTracking()
            .OrderBy(clan => clan.Identifier)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Finds one clan.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Clan?> FindByIdAsync(int clanIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Clans
            .AsNoTracking()
            .FirstOrDefaultAsync(clan => clan.Identifier == clanIdentifier, cancellationToken);
    }

    /// <summary>Finds one clan by name.</summary>
    /// <param name="name">Name of the clan.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Clan?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Clans
            .AsNoTracking()
            .FirstOrDefaultAsync(clan => clan.Name == name, cancellationToken);
    }

    /// <summary>
    /// Creates a clan and seats its first member as the leader. The three
    /// writes are one transaction, because an interrupted call would otherwise
    /// leave a clan nobody can administer.
    /// </summary>
    /// <param name="name">Name of the clan.</param>
    /// <param name="leaderCharacterIdentifier">Character that leads the new clan.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Clan> CreateAsync(
        string name,
        int leaderCharacterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var clan = new Clan { Name = name };
        context.Clans.Add(clan);
        await context.SaveChangesAsync(cancellationToken);

        var member = new ClanMember
        {
            ClanIdentifier = clan.Identifier,
            CharacterIdentifier = leaderCharacterIdentifier,
        };

        context.ClanMembers.Add(member);
        await context.SaveChangesAsync(cancellationToken);

        clan.LeaderIdentifier = member.Identifier;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return clan;
    }
}
