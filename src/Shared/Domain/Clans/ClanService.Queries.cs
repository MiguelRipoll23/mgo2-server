using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Clans;

/// <summary>
/// The read half of the clan service: the projections that join a clan with its
/// leader and its members, which the client screens consume directly.
/// </summary>
public sealed partial class ClanService
{
    /// <summary>Searches the clans whose name contains a fragment.</summary>
    /// <param name="query">Fragment to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<Clan>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Clans
            .AsNoTracking()
            .Where(clan => EF.Functions.Like(clan.Name, $"%{query}%"))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Lists clans joined with the name of their leader.</summary>
    /// <param name="offset">Number of rows to skip.</param>
    /// <param name="limit">Maximum number of rows to return.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<ClanListEntry>> FindAllWithLeaderAsync(
        int offset = 0,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var rows = await context.Clans
            .AsNoTracking()
            .OrderBy(clan => clan.Identifier)
            .Skip(offset)
            .Take(limit)
            .Select(clan => new
            {
                clan.Identifier,
                clan.Name,
                clan.Emblem,
                LeaderCharacterIdentifier = clan.Leader != null ? clan.Leader.CharacterIdentifier : 0,
                LeaderCharacterName = clan.Leader != null && clan.Leader.Character != null
                    ? clan.Leader.Character.Name
                    : string.Empty,
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new ClanListEntry(
            row.Identifier,
            row.Name,
            row.LeaderCharacterIdentifier,
            row.LeaderCharacterName,
            row.Emblem != null))];
    }

    /// <summary>Returns the details of one clan, or <c>null</c> when it is gone.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<ClanDetails?> FindByIdFullAsync(
        int clanIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var details = await context.Clans
            .AsNoTracking()
            .Where(clan => clan.Identifier == clanIdentifier)
            .Select(clan => new
            {
                clan.Identifier,
                clan.Name,
                clan.Comment,
                clan.Emblem,
                LeaderCharacterIdentifier = clan.Leader != null ? clan.Leader.CharacterIdentifier : 0,
                LeaderCharacterName = clan.Leader != null && clan.Leader.Character != null
                    ? clan.Leader.Character.Name
                    : string.Empty,
                MemberCount = clan.Members.Count,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return details is null
            ? null
            : new ClanDetails(
                details.Identifier,
                details.Name,
                details.Comment,
                details.Emblem != null,
                details.LeaderCharacterIdentifier,
                details.LeaderCharacterName,
                details.MemberCount);
    }

    /// <summary>Lists the members of a clan joined with their character names.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<ClanMemberEntry>> GetMembersWithNamesAsync(
        int clanIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.ClanMembers
            .AsNoTracking()
            .Where(member => member.ClanIdentifier == clanIdentifier)
            .OrderBy(member => member.Identifier)
            .Select(member => new ClanMemberEntry(
                member.Identifier,
                member.CharacterIdentifier,
                member.Character != null ? member.Character.Name : string.Empty))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Returns the clan a character belongs to, or <c>null</c> when it belongs to none.</summary>
    /// <param name="characterIdentifier">Character to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<ClanMembership?> FindMembershipByCharacterAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var row = await context.ClanMembers
            .AsNoTracking()
            .Where(member => member.CharacterIdentifier == characterIdentifier)
            .Select(member => new
            {
                member.Identifier,
                ClanIdentifier = member.ClanIdentifier,
                ClanName = member.Clan != null ? member.Clan.Name : string.Empty,
                member.Clan!.LeaderIdentifier,
                member.Clan!.Emblem,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new ClanMembership(
                row.Identifier,
                row.ClanIdentifier,
                row.ClanName,
                row.LeaderIdentifier == row.Identifier,
                row.Emblem != null,
                row.LeaderIdentifier);
    }

    /// <summary>Resolves a membership row to the character it belongs to.</summary>
    /// <param name="memberIdentifier">Identifier of the membership row.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<int?> GetCharacterIdentifierByMemberAsync(
        int memberIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var row = await context.ClanMembers
            .AsNoTracking()
            .Where(member => member.Identifier == memberIdentifier)
            .Select(member => (int?)member.CharacterIdentifier)
            .FirstOrDefaultAsync(cancellationToken);
        return row;
    }
}
