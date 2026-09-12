using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Clans;

/// <summary>
/// The membership half of the clan service: it owns the membership rows, the
/// leadership hand-over, the clan text and the emblems.
/// </summary>
public sealed partial class ClanService
{
    /// <summary>Disbands a clan, removing its members first.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task DisbandAsync(int clanIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await context.ClanMembers
            .Where(member => member.ClanIdentifier == clanIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        await context.Clans
            .Where(clan => clan.Identifier == clanIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Finds the membership row of a character in a clan.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="characterIdentifier">Character to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<ClanMember?> GetMemberAsync(
        int clanIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.ClanMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                member => member.ClanIdentifier == clanIdentifier &&
                    member.CharacterIdentifier == characterIdentifier,
                cancellationToken);
    }

    /// <summary>Lists the membership rows of a clan.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<ClanMember>> GetMembersAsync(
        int clanIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.ClanMembers
            .AsNoTracking()
            .Where(member => member.ClanIdentifier == clanIdentifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Adds a character to a clan.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="characterIdentifier">Character to add.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<ClanMember> AddMemberAsync(
        int clanIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var member = new ClanMember
        {
            ClanIdentifier = clanIdentifier,
            CharacterIdentifier = characterIdentifier,
        };

        context.ClanMembers.Add(member);
        await context.SaveChangesAsync(cancellationToken);
        return member;
    }

    /// <summary>Removes a character from a clan.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="characterIdentifier">Character to remove.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task RemoveMemberAsync(
        int clanIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.ClanMembers
            .Where(member => member.ClanIdentifier == clanIdentifier &&
                member.CharacterIdentifier == characterIdentifier)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Transfers clan leadership to a membership row.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="memberIdentifier">Membership row of the new leader.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SetLeaderAsync(
        int clanIdentifier,
        int memberIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Clans
            .Where(clan => clan.Identifier == clanIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(clan => clan.LeaderIdentifier, memberIdentifier),
                cancellationToken);
    }

    /// <summary>Assigns the emblem editor to a membership row.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="memberIdentifier">Membership row of the editor, or <c>null</c> to close it.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SetEmblemEditorAsync(
        int clanIdentifier,
        int? memberIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Clans
            .Where(clan => clan.Identifier == clanIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(clan => clan.EmblemEditorIdentifier, memberIdentifier),
                cancellationToken);
    }

    /// <summary>Stores the comment of a clan.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="comment">Comment to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdateCommentAsync(
        int clanIdentifier,
        string comment,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Clans
            .Where(clan => clan.Identifier == clanIdentifier)
            .ExecuteUpdateAsync(setters => setters.SetProperty(clan => clan.Comment, comment), cancellationToken);
    }

    /// <summary>Stores the notice of a clan and the member that wrote it.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="notice">Notice to store.</param>
    /// <param name="writerIdentifier">Membership row of the author.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdateNoticeAsync(
        int clanIdentifier,
        string notice,
        int writerIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var secondsSinceEpoch = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await context.Clans
            .Where(clan => clan.Identifier == clanIdentifier)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(clan => clan.Notice, notice)
                    .SetProperty(clan => clan.NoticeWriterIdentifier, writerIdentifier)
                    .SetProperty(clan => clan.NoticeTime, secondsSinceEpoch),
                cancellationToken);
    }

    /// <summary>Stores the published emblem of a clan.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="emblem">Emblem bytes to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SetEmblemAsync(
        int clanIdentifier,
        byte[] emblem,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Clans
            .Where(clan => clan.Identifier == clanIdentifier)
            .ExecuteUpdateAsync(setters => setters.SetProperty(clan => clan.Emblem, emblem), cancellationToken);
    }

    /// <summary>Stores the emblem a clan is currently editing.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="emblem">Emblem bytes to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SetEmblemWorkInProgressAsync(
        int clanIdentifier,
        byte[] emblem,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Clans
            .Where(clan => clan.Identifier == clanIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(clan => clan.EmblemWorkInProgress, emblem),
                cancellationToken);
    }
}
