using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>Outcome of joining a team.</summary>
public enum EventJoinOutcome
{
    /// <summary>The character is now a member.</summary>
    Joined,

    /// <summary>No such team exists in the lobby.</summary>
    TeamNotFound,

    /// <summary>The team is protected and the supplied password is wrong.</summary>
    WrongPassword,

    /// <summary>The team has no free slot.</summary>
    TeamFull,

    /// <summary>The character is already a member.</summary>
    AlreadyMember,
}

/// <summary>Result of removing a member, which decides whether the team survives.</summary>
public enum EventLeaveOutcome
{
    /// <summary>A non-leader member left and the team remains.</summary>
    MemberLeft,

    /// <summary>The leader left and the team is gone.</summary>
    TeamDisbanded,

    /// <summary>The character was not a member.</summary>
    NotAMember,
}

/// <summary>
/// Owns the formed event teams: their creation, roster changes and the
/// projection of a team into the active-game snapshot the client caches.
/// <para>
/// Every operation reads or writes the row, so a restart frees nothing and a
/// second process sees the same teams. The snapshot is built at send time; it is
/// never the storage.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class EventTeamService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
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

    /// <summary>Finds one team with its roster.</summary>
    /// <param name="teamIdentifier">Team to find.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventTeam?> FindAsync(int teamIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventTeams
            .Include(team => team.Members)
            .FirstOrDefaultAsync(team => team.Identifier == teamIdentifier, cancellationToken);
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
        return await context.EventTeams
            .Include(team => team.Members)
            .FirstOrDefaultAsync(
                team => team.OwnerCharacterIdentifier == ownerCharacterIdentifier
                    && team.LobbyIdentifier == lobbyIdentifier,
                cancellationToken);
    }

    /// <summary>Lists the joinable teams of a lobby.</summary>
    /// <param name="lobbyIdentifier">Lobby to list.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<EventTeam>> FindJoinableAsync(
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventTeams
            .Include(team => team.Members)
            .Where(team => team.LobbyIdentifier == lobbyIdentifier
                && team.State == EventConstants.TeamJoinableState)
            .OrderBy(team => team.Identifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Lists every team of a lobby, in any state.</summary>
    /// <param name="lobbyIdentifier">Lobby to list.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
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
        return await context.EventTeams
            .Include(team => team.Members)
            .Where(team => team.LobbyIdentifier == lobbyIdentifier
                && team.EventIdentifier == eventIdentifier)
            .OrderBy(team => team.Identifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Adds a character to a team.</summary>
    /// <param name="teamIdentifier">Team to join.</param>
    /// <param name="lobbyIdentifier">Lobby the caller is in.</param>
    /// <param name="characterIdentifier">Character joining.</param>
    /// <param name="characterName">Name of the character joining.</param>
    /// <param name="experience">Experience of the character joining.</param>
    /// <param name="password">Password supplied for a protected team.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<(EventJoinOutcome Outcome, EventTeam? Team)> JoinAsync(
        int teamIdentifier,
        int lobbyIdentifier,
        int characterIdentifier,
        string characterName,
        int experience,
        string password,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var team = await context.EventTeams
            .Include(candidate => candidate.Members)
            .FirstOrDefaultAsync(
                candidate => candidate.Identifier == teamIdentifier
                    && candidate.LobbyIdentifier == lobbyIdentifier,
                cancellationToken);

        if (team is null)
        {
            return (EventJoinOutcome.TeamNotFound, null);
        }

        // A join that is refused leaves the team untouched rather than mutating
        // it and rolling back, so the row the client was shown stays valid.
        if (team.Members.Any(member => member.CharacterIdentifier == characterIdentifier))
        {
            return (EventJoinOutcome.AlreadyMember, team);
        }

        if ((team.FlagBits & PasswordProtectedFlag) != 0
            && !string.Equals(team.Password, password ?? string.Empty, StringComparison.Ordinal))
        {
            return (EventJoinOutcome.WrongPassword, team);
        }

        if (team.Members.Count >= EventConstants.TeamMemberLimit)
        {
            return (EventJoinOutcome.TeamFull, team);
        }

        var slot = NextFreeSlot(team);
        if (slot < 0)
        {
            return (EventJoinOutcome.TeamFull, team);
        }

        team.Members.Add(new EventTeamMember
        {
            Slot = slot,
            CharacterIdentifier = characterIdentifier,
            Name = characterName,
            State = EventConstants.ParticipantPendingState,
            Experience = Math.Max(0, experience),
        });

        // The sequence is left alone. It is the serial the members' clients are
        // holding, and the notification that fills this slot is discarded unless
        // it carries exactly that one — see EventTeam.Sequence. The roster
        // changes; the identity the client reconciles it against does not.
        team.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return (EventJoinOutcome.Joined, team);
    }

    /// <summary>Removes a member, dismantling the team when its leader leaves.</summary>
    /// <param name="teamIdentifier">Team to change.</param>
    /// <param name="characterIdentifier">Character leaving.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventLeaveOutcome> LeaveAsync(
        int teamIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var team = await context.EventTeams
            .Include(candidate => candidate.Members)
            .FirstOrDefaultAsync(candidate => candidate.Identifier == teamIdentifier, cancellationToken);

        if (team is null)
        {
            return EventLeaveOutcome.NotAMember;
        }

        if (team.OwnerCharacterIdentifier == characterIdentifier)
        {
            context.EventTeams.Remove(team);
            await context.SaveChangesAsync(cancellationToken);
            return EventLeaveOutcome.TeamDisbanded;
        }

        var member = team.Members.FirstOrDefault(candidate => candidate.CharacterIdentifier == characterIdentifier);
        if (member is null)
        {
            return EventLeaveOutcome.NotAMember;
        }

        context.EventTeamMembers.Remove(member);

        // Left alone for the same reason a join leaves it alone: the removal is
        // announced against the serial the remaining members are holding.
        team.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return EventLeaveOutcome.MemberLeft;
    }

    /// <summary>Sets one member's entry decision.</summary>
    /// <param name="teamIdentifier">Team to change.</param>
    /// <param name="characterIdentifier">Character whose decision changed.</param>
    /// <param name="decision">One to ready the member, zero to hold them.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The changed slot index, or minus one when the character is not a member.</returns>
    public async Task<int> SetDecisionAsync(
        int teamIdentifier,
        int characterIdentifier,
        int decision,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var team = await context.EventTeams
            .Include(candidate => candidate.Members)
            .FirstOrDefaultAsync(candidate => candidate.Identifier == teamIdentifier, cancellationToken);

        var member = team?.Members.FirstOrDefault(candidate => candidate.CharacterIdentifier == characterIdentifier);
        if (team is null || member is null)
        {
            return -1;
        }

        member.State = decision == 1
            ? EventConstants.ParticipantReadyState
            : EventConstants.ParticipantPendingState;
        team.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return member.Slot;
    }

    /// <summary>Projects a team and its roster into an active-game snapshot.</summary>
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

    private static int NextFreeSlot(EventTeam team)
    {
        for (var slot = 1; slot < EventConstants.TeamRosterSize; slot++)
        {
            if (!team.Members.Any(member => member.Slot == slot))
            {
                return slot;
            }
        }

        return -1;
    }
}
