using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// One half of a pairing, as the testing tools are told about it.
/// </summary>
/// <param name="Identifier">Row identifier of the team.</param>
/// <param name="Name">Display name of the team.</param>
/// <param name="MemberCount">How many players the roster holds.</param>
/// <param name="IsFake">Whether every member is one the testing tools made.</param>
public readonly record struct EventMatchSide(
    int Identifier,
    string Name,
    int MemberCount,
    bool IsFake);

/// <summary>
/// One pairing, as the testing tools are told about it.
/// </summary>
/// <param name="Identifier">Row identifier of the pairing.</param>
/// <param name="State">Lifecycle state of the pairing.</param>
/// <param name="First">First of the two paired teams.</param>
/// <param name="Second">Second of the two paired teams.</param>
/// <param name="RoomName">Room the match is leased to, or empty while it waits for one.</param>
public readonly record struct EventMatchSummary(
    int Identifier,
    int State,
    EventMatchSide First,
    EventMatchSide Second,
    string RoomName);

/// <summary>
/// Lists the pairings a lobby is holding right now, for the event testing tools.
/// <para>
/// It exists because a pairing is otherwise invisible until it is announced. A
/// match is only pushed to its teams once a room has been leased for it, and a
/// pairing with no eligible room waits there — so a moderator who queued two
/// teams and saw nothing happen could not tell a queue that had not paired from
/// a pair waiting for a host, because neither writes anything a client reads.
/// This is the read that tells the two apart.
/// </para>
/// <para>
/// Only the live states are listed. A completed or cancelled pairing is history
/// the reward ledger and the battle list already answer for, and a tool showing
/// it would suggest a match is still going.
/// </para>
/// <para>
/// The team is recognised as a testing one by its roster rather than by its
/// identifier, so a real team a player formed is never mistaken for one and a
/// pairing of two of them still reads as a pairing of real teams.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class EventMatchListingService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
{
    /// <summary>Lists the live pairings of a lobby, oldest first.</summary>
    /// <param name="lobbyIdentifier">Lobby to list.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<IReadOnlyList<EventMatchSummary>> ListAsync(
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (lobbyIdentifier <= 0)
        {
            return [];
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var matches = await context.EventMatches
            .Where(match => match.LobbyIdentifier == lobbyIdentifier
                && (match.State == EventConstants.MatchPairedState
                    || match.State == EventConstants.MatchAssignedState))
            .OrderBy(match => match.Identifier)
            .ToListAsync(cancellationToken);
        if (matches.Count == 0)
        {
            return [];
        }

        var teams = await context.EventTeams
            .Include(team => team.Members)
            .Where(team => matches.Select(match => match.FirstTeamIdentifier)
                    .Concat(matches.Select(match => match.SecondTeamIdentifier))
                .Contains(team.Identifier))
            .ToListAsync(cancellationToken);

        var leases = await context.EventHostLeases
            .Where(lease => lease.LobbyIdentifier == lobbyIdentifier
                && lease.Status == EventConstants.LeaseActiveState)
            .ToDictionaryAsync(lease => lease.MatchIdentifier, cancellationToken);

        var roomIdentifiers = leases.Values.Select(lease => lease.GameIdentifier).Distinct().ToList();
        var rooms = await context.Games
            .Where(game => roomIdentifiers.Contains(game.Identifier))
            .ToDictionaryAsync(game => game.Identifier, game => game.Name, cancellationToken);

        return
        [
            .. matches.Select(match => new EventMatchSummary(
                match.Identifier,
                match.State,
                SideOf(teams, match.FirstTeamIdentifier),
                SideOf(teams, match.SecondTeamIdentifier),
                RoomOf(leases, rooms, match.Identifier)))
        ];
    }

    /// <summary>
    /// Reads one half of a pairing. A team the row no longer names is reported as
    /// an unnamed side rather than omitted, because a pairing whose team cannot be
    /// read is exactly the fault the list exists to make visible.
    /// </summary>
    /// <param name="teams">Teams the pairings name, loaded with their rosters.</param>
    /// <param name="teamIdentifier">Team to read.</param>
    private static EventMatchSide SideOf(List<EventTeam> teams, int teamIdentifier)
    {
        var team = teams.FirstOrDefault(candidate => candidate.Identifier == teamIdentifier);
        if (team is null)
        {
            return new EventMatchSide(teamIdentifier, "(missing)", 0, false);
        }

        var fake = team.Members.Count > 0
            && team.Members.All(member =>
                FakePlayerIdentifierUtils.IsFake(member.CharacterIdentifier));

        return new EventMatchSide(
            team.Identifier,
            team.Name,
            team.Members.Count,
            fake);
    }

    /// <summary>
    /// The room a pairing is holding, or an empty name while it has none. A lease
    /// whose room is gone is reported as no room rather than as a blank one, so
    /// the tools say "waiting for a host" instead of naming nothing.
    /// </summary>
    /// <param name="leases">Active leases of the lobby, by match.</param>
    /// <param name="rooms">Room names, by room identifier.</param>
    /// <param name="matchIdentifier">Pairing to read the room of.</param>
    private static string RoomOf(
        Dictionary<int, EventHostLease> leases,
        Dictionary<int, string> rooms,
        int matchIdentifier) =>
        leases.TryGetValue(matchIdentifier, out var lease)
            && rooms.TryGetValue(lease.GameIdentifier, out var roomName)
                ? roomName
                : string.Empty;
}
