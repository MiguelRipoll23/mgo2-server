using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Owns the formed event teams: their creation and the projection of a team into
/// the active-game snapshot the client caches.
/// <para>
/// A team is either a row or a simulated one held in memory. Every query merges
/// the two, so a caller — the matchmaker included — reads one store and never has
/// to know where a team came from. A row is authoritative for everything the
/// client can see; a simulated team is a test's own, and it is gone when the
/// process is.
/// </para>
/// <para>
/// Every operation reads or writes its half, so a restart frees nothing that was
/// persisted and a second process sees the same rows. The snapshot is built at
/// send time; it is never the storage.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="memoryService">Store that owns the simulated teams and members.</param>
public sealed class EventTeamService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    EventTeamMemoryService memoryService)
    : DomainService(contextFactory)
{
    /// <summary>Option bit marking a password-protected team.</summary>
    public const int PasswordProtectedFlag = 0x01;

    /// <summary>Creates a team and its leader slot.</summary>
    /// <param name="ownerCharacterIdentifier">Character that leads the team.</param>
    /// <param name="ownerName">Name of the leading character.</param>
    /// <param name="experience">Experience of the leading character.</param>
    /// <param name="name">Team name.</param>
    /// <param name="comment">Team comment.</param>
    /// <param name="flagBits">Option bits.</param>
    /// <param name="password">Join password, or empty.</param>
    /// <param name="matchType">Match type, which is the lobby selector.</param>
    /// <param name="lobbyIdentifier">Lobby the team forms in.</param>
    /// <param name="eventIdentifier">Event the team entered.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The created team, without its members loaded.</returns>
    public async Task<EventTeam> CreateAsync(
        int ownerCharacterIdentifier,
        string ownerName,
        int experience,
        string name,
        string comment,
        int flagBits,
        string password,
        int matchType,
        int lobbyIdentifier,
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var team = new EventTeam
        {
            OwnerCharacterIdentifier = ownerCharacterIdentifier,
            LobbyIdentifier = lobbyIdentifier,
            EventIdentifier = eventIdentifier,
            MatchType = matchType,
            Name = name,
            Comment = comment,
            FlagBits = flagBits,
            Password = password,
            State = EventConstants.TeamJoinableState,
            Sequence = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
        team.Members.Add(new EventTeamMember
        {
            Slot = 0,
            CharacterIdentifier = ownerCharacterIdentifier,
            Name = ownerName,
            State = EventConstants.ParticipantPendingState,
            Experience = Math.Max(0, experience),
        });

        await using var context = await CreateContextAsync(cancellationToken);
        context.EventTeams.Add(team);
        await context.SaveChangesAsync(cancellationToken);
        return team;
    }

    /// <summary>
    /// Creates a simulated team whose whole roster is simulated. It is never
    /// written to the database and it disappears when the process does, which is
    /// what makes it usable as an opponent without touching real state.
    /// </summary>
    /// <param name="name">Display name of the team.</param>
    /// <param name="matchType">Match type, which is the lobby selector.</param>
    /// <param name="lobbyIdentifier">Lobby the team belongs to.</param>
    /// <param name="eventIdentifier">Event the team is filed under.</param>
    /// <param name="roster">Test characters the roster is filled from.</param>
    public EventTeam CreateInMemory(
        string name,
        int matchType,
        int lobbyIdentifier,
        int eventIdentifier,
        IReadOnlyList<Character> roster) =>
        memoryService.CreateTeam(name, matchType, lobbyIdentifier, eventIdentifier, roster);

    /// <summary>Finds one team with its roster, real or simulated.</summary>
    /// <param name="teamIdentifier">Team to find.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventTeam?> FindAsync(int teamIdentifier, CancellationToken cancellationToken = default)
    {
        if (memoryService.FindTeam(teamIdentifier) is { } memoryTeam)
        {
            return memoryTeam;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var team = await context.EventTeams
            .AsNoTracking()
            .Include(candidate => candidate.Members)
            .FirstOrDefaultAsync(candidate => candidate.Identifier == teamIdentifier, cancellationToken);
        return AttachSimulatedMembers(team);
    }

    /// <summary>
    /// Finds the team one character owns in a lobby. The owner is the leader, so
    /// this is the roster a repeat entry belongs to rather than any team the
    /// character happens to be a member of.
    /// </summary>
    /// <param name="ownerCharacterIdentifier">Character to look for.</param>
    /// <param name="lobbyIdentifier">Lobby to look in.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventTeam?> FindOwnedInLobbyAsync(
        int ownerCharacterIdentifier,
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (ownerCharacterIdentifier <= 0)
        {
            return null;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var team = await context.EventTeams
            .AsNoTracking()
            .Include(candidate => candidate.Members)
            .FirstOrDefaultAsync(
                candidate => candidate.OwnerCharacterIdentifier == ownerCharacterIdentifier
                    && candidate.LobbyIdentifier == lobbyIdentifier,
                cancellationToken);

        return AttachSimulatedMembers(team)
            ?? memoryService.FindOwnedInLobby(ownerCharacterIdentifier, lobbyIdentifier);
    }

    /// <summary>Lists the joinable teams of a lobby, real and simulated.</summary>
    /// <param name="lobbyIdentifier">Lobby to list.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<EventTeam>> FindJoinableAsync(
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var teams = await context.EventTeams
            .AsNoTracking()
            .Include(candidate => candidate.Members)
            .Where(candidate => candidate.LobbyIdentifier == lobbyIdentifier
                && candidate.State == EventConstants.TeamJoinableState)
            .OrderBy(candidate => candidate.Identifier)
            .ToListAsync(cancellationToken);

        teams.AddRange(memoryService.FindJoinable(lobbyIdentifier));
        return teams;
    }

    /// <summary>
    /// Lists the teams one event's list is made of. The lobby alone is not
    /// enough: a lobby may be publishing several events, and each is shown with
    /// its own entrants.
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby the teams were formed in.</param>
    /// <param name="eventIdentifier">Event whose list is being built.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<EventTeam>> FindByLobbyAndEventAsync(
        int lobbyIdentifier,
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var teams = await context.EventTeams
            .AsNoTracking()
            .Include(candidate => candidate.Members)
            .Where(candidate => candidate.LobbyIdentifier == lobbyIdentifier
                && candidate.EventIdentifier == eventIdentifier)
            .OrderBy(candidate => candidate.Identifier)
            .ToListAsync(cancellationToken);

        foreach (var team in teams)
        {
            AttachSimulatedMembers(team);
        }

        teams.AddRange(memoryService.FindByLobbyAndEvent(lobbyIdentifier, eventIdentifier));
        return teams;
    }

    /// <summary>
    /// Projects a team and its roster into an active-game snapshot.
    /// </summary>
    /// <param name="team">Team to project; its members must be loaded.</param>
    /// <returns>The snapshot.</returns>
    public static EventSnapshot BuildSnapshot(EventTeam team)
    {
        ArgumentNullException.ThrowIfNull(team);

        var snapshot = new EventSnapshot
        {
            SnapshotIdentifier = team.Identifier,
            Sequence = team.Sequence,
            State = team.State,
            Name = team.Name,
            Comment = team.Comment,
            FlagBits = team.FlagBits,
            JoinPassword = team.Password,
            LobbyIdentifier = team.LobbyIdentifier,
            MatchType = team.MatchType,
            EventIdentifier = team.EventIdentifier,
            HostIdentifier = team.OwnerCharacterIdentifier,
            ConsecutiveWins = team.ConsecutiveWins,
            PaidReward = team.PaidReward,
        };

        foreach (var member in team.Members.OrderBy(member => member.Slot))
        {
            if (member.Slot < 0 || member.Slot >= snapshot.Participants.Length)
            {
                continue;
            }

            var participant = snapshot.Participants[member.Slot];
            participant.CharacterIdentifier = member.CharacterIdentifier;
            participant.Name = member.Name;
            participant.State = member.State;
            participant.Experience = member.Experience;
            snapshot.ParticipantStates[member.Slot] = (byte)member.State;

            if (member.Slot == 0)
            {
                snapshot.HostName = member.Name;
            }
        }

        snapshot.PrimaryEquipmentType = 0;
        return snapshot;
    }

    /// <summary>
    /// Merges the simulated members a real team carries into its roster. The
    /// entity is detached, so a member that exists only in memory cannot be
    /// written back by accident.
    /// </summary>
    /// <param name="team">Team to merge into, when there is one.</param>
    private EventTeam? AttachSimulatedMembers(EventTeam? team)
    {
        if (team is null)
        {
            return null;
        }

        foreach (var member in memoryService.MembersFor(team.Identifier))
        {
            team.Members.Add(member);
        }

        return team;
    }
}
