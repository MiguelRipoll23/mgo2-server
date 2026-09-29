using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;
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
/// Owns the roster half of an event team: who is in it, which slot they hold and
/// whether they have decided to play.
/// <para>
/// It is a service of its own rather than another set of methods on
/// <see cref="EventTeamService"/> because a team's identity changes with its
/// roster, and because the roster is the one part that is mixed: a real row's
/// roster is a table, while the players a test pads it with are held in memory.
/// Every operation therefore routes on the team's identifier and the character's:
/// a simulated team or character is served from <see cref="EventTeamMemoryService"/>,
/// and a real one from the row.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="memoryService">Store that owns the simulated teams and members.</param>
public sealed class EventTeamMemberService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    EventTeamMemoryService memoryService)
    : DomainService(contextFactory)
{
    /// <summary>
    /// Adds a simulated member to a team without touching the database. It is the
    /// write a test makes on a real team, so the team the client is looking at
    /// gains a roster row that exists only in this process.
    /// </summary>
    /// <param name="teamIdentifier">Team to pad.</param>
    /// <param name="slot">Roster slot to fill.</param>
    /// <param name="character">Test character filling the slot.</param>
    /// <returns>The added member, pending like a player who has not decided.</returns>
    public EventTeamMember AddSimulatedMember(
        int teamIdentifier,
        int slot,
        Character character) =>
        memoryService.AddMember(
            teamIdentifier,
            slot,
            character,
            EventConstants.ParticipantPendingState);

    /// <summary>Adds a character to a team, real or simulated.</summary>
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
        if (memoryService.FindTeam(teamIdentifier) is { } memoryTeam)
        {
            return JoinMemoryTeam(memoryTeam, characterIdentifier, characterName, experience, password);
        }

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

        if ((team.FlagBits & EventTeamService.PasswordProtectedFlag) != 0
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
        if (memoryService.FindTeam(teamIdentifier) is { } memoryTeam)
        {
            if (memoryTeam.OwnerCharacterIdentifier == characterIdentifier)
            {
                memoryService.RemoveTeam(teamIdentifier);
                return EventLeaveOutcome.TeamDisbanded;
            }

            return memoryService.RemoveMember(teamIdentifier, characterIdentifier)
                ? EventLeaveOutcome.MemberLeft
                : EventLeaveOutcome.NotAMember;
        }

        // A test player is never in the table, so a test character is served
        // from memory even when the team itself is a row.
        if (TestIdentifierUtils.IsTest(characterIdentifier))
        {
            return memoryService.RemoveMember(teamIdentifier, characterIdentifier)
                ? EventLeaveOutcome.MemberLeft
                : EventLeaveOutcome.NotAMember;
        }

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

    /// <summary>Sets one member's entry decision, real or simulated.</summary>
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
        var state = decision == 1
            ? EventConstants.ParticipantReadyState
            : EventConstants.ParticipantPendingState;

        if (memoryService.Holds(teamIdentifier) || TestIdentifierUtils.IsTest(characterIdentifier))
        {
            return memoryService.SetMemberState(teamIdentifier, characterIdentifier, state) ?? -1;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var team = await context.EventTeams
            .Include(candidate => candidate.Members)
            .FirstOrDefaultAsync(candidate => candidate.Identifier == teamIdentifier, cancellationToken);

        var member = team?.Members.FirstOrDefault(candidate => candidate.CharacterIdentifier == characterIdentifier);
        if (team is null || member is null)
        {
            return -1;
        }

        member.State = state;
        team.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return member.Slot;
    }

    private static (EventJoinOutcome Outcome, EventTeam? Team) JoinMemoryTeam(
        EventTeam team,
        int characterIdentifier,
        string characterName,
        int experience,
        string password)
    {
        if (team.Members.Any(member => member.CharacterIdentifier == characterIdentifier))
        {
            return (EventJoinOutcome.AlreadyMember, team);
        }

        if ((team.FlagBits & EventTeamService.PasswordProtectedFlag) != 0
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

        return (EventJoinOutcome.Joined, team);
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
