using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// One real team as the testing tools are told about it.
/// <para>
/// It is a summary rather than a snapshot because a tool needs to recognise a
/// team by what a player would recognise it by — its name, its leader and how
/// full it is — and not to render it. The client protocol's own projection stays
/// where the protocol is.
/// </para>
/// </summary>
/// <param name="Identifier">Row identifier, which a player never sees.</param>
/// <param name="Name">Display name of the team.</param>
/// <param name="State">Lifecycle state the entry pipeline is holding it in.</param>
/// <param name="LeaderName">Name of the character in slot zero.</param>
/// <param name="MemberCount">How many players the roster holds, leader included.</param>
public readonly record struct EventTeamSummary(
    int Identifier,
    string Name,
    int State,
    string LeaderName,
    int MemberCount);

/// <summary>
/// Lists the formed teams of a lobby, in every state, for the event testing
/// tools.
/// <para>
/// It is a service of its own rather than another method on
/// <see cref="EventTeamService"/> because that file is already at the project's
/// line limit, and because the question it answers is a different one: a team
/// list the client opens is built from the joinable rows alone, while a
/// moderator needs the queued and entered ones beside them — a team that is
/// waiting for an opponent is exactly the row a tool is asked about.
/// </para>
/// <para>
/// The in-memory teams are not here. They live in the lobby's own process and
/// have no row, so the lobby is asked for those separately and the two answers
/// are shown as the two things they are.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class EventTeamListingService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
{
    /// <summary>Lists every team of a lobby, whatever state it is in.</summary>
    /// <param name="lobbyIdentifier">Lobby to list.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The teams, oldest first, so the order a player formed them in is kept.</returns>
    public async Task<IReadOnlyList<EventTeamSummary>> ListAsync(
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (lobbyIdentifier <= 0)
        {
            return [];
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var teams = await context.EventTeams
            .Include(team => team.Members)
            .Where(team => team.LobbyIdentifier == lobbyIdentifier)
            .OrderBy(team => team.Identifier)
            .ToListAsync(cancellationToken);

        return [.. teams.Select(team => new EventTeamSummary(
            team.Identifier,
            team.Name,
            team.State,
            LeaderOf(team),
            team.Members.Count))];
    }

    /// <summary>
    /// The name of the character in slot zero, which is the leader. A row whose
    /// slot zero is empty is reported as unnamed rather than as a blank column,
    /// because a team with no leader is a state the tools can show but not one
    /// a player can act on.
    /// </summary>
    /// <param name="team">Team to read the leader of.</param>
    private static string LeaderOf(EventTeam team)
    {
        var leader = team.Members.FirstOrDefault(member => member.Slot == 0);
        return leader is null || string.IsNullOrEmpty(leader.Name)
            ? "(no leader)"
            : leader.Name;
    }
}
