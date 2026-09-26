using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Owns the one field matchmaking writes outside the team itself: the state that
/// says whether a team is up for grabs or already waiting for an opponent.
/// <para>
/// It is a service of its own rather than another method on
/// <see cref="EventTeamService"/> because that file is already at the project's
/// line limit, and because the write belongs to the queue rather than to the
/// team's identity: nothing else in the team may change with it.
/// </para>
/// <para>
/// The state is persisted rather than held in memory because it is read from
/// outside the process — the joinable list is built from it — so a waiting team
/// has to be marked in the one store every lobby reads.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory of the database contexts.</param>
public sealed class EventTeamStateService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
{
    /// <summary>
    /// Moves a team into <paramref name="state"/>, and reports whether the row
    /// was there to move. A team that has been disbanded underneath the queue is
    /// not an error: the queue is told about it by the next reconcile, and there
    /// is nothing left to mark.
    /// </summary>
    /// <param name="teamIdentifier">Team to move.</param>
    /// <param name="state">State the team should hold.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether the team exists and was written.</returns>
    public async Task<bool> SetAsync(
        int teamIdentifier,
        int state,
        CancellationToken cancellationToken = default)
    {
        if (teamIdentifier <= 0)
        {
            return false;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var team = await context.EventTeams
            .FirstOrDefaultAsync(candidate => candidate.Identifier == teamIdentifier, cancellationToken);
        if (team is null)
        {
            return false;
        }

        team.State = state;
        team.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
