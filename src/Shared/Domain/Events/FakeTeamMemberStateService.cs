using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>Why setting the member state of a real team's fake players did not happen.</summary>
public enum FakeTeamMemberStateOutcome
{
    /// <summary>The fake members' state was set.</summary>
    Changed,

    /// <summary>No team of that name exists in the lobby.</summary>
    TeamNotFound,

    /// <summary>The team holds no fake player whose state could be set.</summary>
    NoFakeMembers,
}

/// <summary>What setting the member state of a real team did.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="TeamIdentifier">Team that was changed.</param>
/// <param name="Snapshot">Roster after the change, so the caller can push it.</param>
/// <param name="ChangedSlots">Slots whose member state moved.</param>
/// <param name="ChangedCount">How many fake members were changed.</param>
public readonly record struct FakeTeamMemberStateResult(
    FakeTeamMemberStateOutcome Outcome,
    int TeamIdentifier,
    EventSnapshot? Snapshot,
    IReadOnlyList<int> ChangedSlots,
    int ChangedCount);

/// <summary>
/// Sets the entry-decision byte on the fake players of a real team — a row a
/// player formed, or one a pairing request wrote out.
/// <para>
/// Adding fake players to a real team writes them in the state the team's own
/// state implies, which for a team that is open is the pending byte the client
/// paints "NG". There is nobody to press the button that would change it: a
/// fake player has no client, and a real player only ever decides for
/// themselves. So the roster is left showing undecided members that cannot be
/// un-undecided, and the testing tool is the only thing that can move them.
/// </para>
/// <para>
/// Only the fake members move. A real member's decision is a person's own, and
/// the tool has no business writing it: doing so could pull somebody into a
/// match they never accepted. The team's own state is likewise left alone, since
/// the entry pipeline owns it.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class FakeTeamMemberStateService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>
    /// Sets the member state of every fake player in a real team.
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby the team is in.</param>
    /// <param name="teamName">Name of the team to change.</param>
    /// <param name="memberState">State to store on each fake member.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<FakeTeamMemberStateResult> SetAsync(
        int lobbyIdentifier,
        string teamName,
        int memberState,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return new FakeTeamMemberStateResult(
                FakeTeamMemberStateOutcome.TeamNotFound, 0, null, [], 0);
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var trimmed = teamName.Trim();
        var lowered = trimmed.ToLowerInvariant();
        var team = await context.EventTeams
            .Include(candidate => candidate.Members)
            .Where(candidate => candidate.LobbyIdentifier == lobbyIdentifier
                && candidate.Name.ToLower() == lowered)
            .OrderBy(candidate => candidate.State == EventConstants.TeamJoinableState ? 0 : 1)
            .ThenBy(candidate => candidate.Identifier)
            .FirstOrDefaultAsync(cancellationToken);

        if (team is null)
        {
            return new FakeTeamMemberStateResult(
                FakeTeamMemberStateOutcome.TeamNotFound, 0, null, [], 0);
        }

        var changedSlots = new List<int>();
        foreach (var member in team.Members)
        {
            if (!FakePlayerIdentifierUtils.IsFake(member.CharacterIdentifier)
                || member.State == memberState)
            {
                continue;
            }

            member.State = memberState;
            changedSlots.Add(member.Slot);
        }

        if (changedSlots.Count == 0)
        {
            return new FakeTeamMemberStateResult(
                FakeTeamMemberStateOutcome.NoFakeMembers,
                team.Identifier,
                null,
                [],
                0);
        }

        // The sequence is not advanced, for the same reason a roster change does
        // not advance it: the clients holding this team cached its serial, and
        // every notification about it is discarded unless it carries that same
        // one. See EventTeam.Sequence.
        team.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return new FakeTeamMemberStateResult(
            FakeTeamMemberStateOutcome.Changed,
            team.Identifier,
            EventTeamService.BuildSnapshot(team),
            changedSlots,
            changedSlots.Count);
    }
}
