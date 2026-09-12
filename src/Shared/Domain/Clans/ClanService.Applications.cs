using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Clans;

/// <summary>
/// The application half of the clan service: it owns the pending applications
/// a clan receives and the decision the leader makes about each of them.
/// </summary>
public sealed partial class ClanService
{
    /// <summary>
    /// Records a pending application. A re-application is absorbed by the
    /// unique pair and is not an error on the wire.
    /// </summary>
    /// <param name="characterIdentifier">Character that applies.</param>
    /// <param name="clanIdentifier">Clan applied to.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns><c>false</c> when an application already existed.</returns>
    public async Task<bool> ApplyAsync(
        int characterIdentifier,
        int clanIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var existing = await context.ClanApplications
            .AsNoTracking()
            .AnyAsync(
                application => application.ClanIdentifier == clanIdentifier &&
                    application.CharacterIdentifier == characterIdentifier,
                cancellationToken);

        if (existing)
        {
            return false;
        }

        context.ClanApplications.Add(new ClanApplication
        {
            ClanIdentifier = clanIdentifier,
            CharacterIdentifier = characterIdentifier,
        });

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Lists the pending applicants of a clan, oldest first.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<ClanApplicant>> GetApplicantsAsync(
        int clanIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.ClanApplications
            .AsNoTracking()
            .Where(application => application.ClanIdentifier == clanIdentifier)
            .OrderBy(application => application.AppliedAt)
            .Select(application => new ClanApplicant(
                application.CharacterIdentifier,
                application.Character != null ? application.Character.Name : string.Empty,
                application.AppliedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Accepts a pending application: consumes the row and adds the member.
    /// </summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="characterIdentifier">Applicant to accept.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns><c>false</c> when there was no pending application.</returns>
    public async Task<bool> ApproveAsync(
        int clanIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var consumed = await context.ClanApplications
            .Where(application => application.ClanIdentifier == clanIdentifier &&
                application.CharacterIdentifier == characterIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        if (consumed == 0)
        {
            return false;
        }

        var alreadyMember = await context.ClanMembers
            .AsNoTracking()
            .AnyAsync(
                member => member.ClanIdentifier == clanIdentifier &&
                    member.CharacterIdentifier == characterIdentifier,
                cancellationToken);

        if (!alreadyMember)
        {
            context.ClanMembers.Add(new ClanMember
            {
                ClanIdentifier = clanIdentifier,
                CharacterIdentifier = characterIdentifier,
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>Declines a pending application.</summary>
    /// <param name="clanIdentifier">Identifier of the clan.</param>
    /// <param name="characterIdentifier">Applicant to decline.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns><c>false</c> when there was no pending application.</returns>
    public async Task<bool> DeclineAsync(
        int clanIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var consumed = await context.ClanApplications
            .Where(application => application.ClanIdentifier == clanIdentifier &&
                application.CharacterIdentifier == characterIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        return consumed > 0;
    }

    /// <summary>Withdraws every pending application of a character.</summary>
    /// <param name="characterIdentifier">Character whose applications are withdrawn.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task WithdrawAllApplicationsAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.ClanApplications
            .Where(application => application.CharacterIdentifier == characterIdentifier)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
