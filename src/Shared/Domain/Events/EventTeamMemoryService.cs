using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The in-memory half of the event team store. It holds the teams and members
/// that exist only for the duration of a test — a simulated opponent team, and
/// the simulated players a real team is padded with — so they can be mixed with
/// the rows without ever being written to the database.
/// <para>
/// The two halves are told apart by identifier: a real row owns an identifier
/// from a database sequence, so every simulated team gets one from the test
/// range. That makes routing a lookup a range test rather than a table scan, and
/// it keeps a simulated member's identifier positive, which is what the client
/// expects to resolve a roster entry by.
/// </para>
/// <para>
/// Members added to a real team are kept beside it rather than in it, because the
/// real team is a row that is loaded fresh for every read. The team service
/// merges them back when it projects a team, so a caller sees one roster whether
/// a slot came from a row or from here.
/// </para>
/// <para>
/// The state is instance state guarded by one lock, the same shape the invitation
/// and automatch stores use: a test is a handful of writes made at once from one
/// connection, and a reader must never see half of them.
/// </para>
/// </summary>
public sealed class EventTeamMemoryService
{
    private readonly Lock gate = new();
    private readonly Dictionary<int, EventTeam> teams = [];
    private readonly Dictionary<int, List<EventTeamMember>> attachedMembers = [];
    private int nextTeamIdentifier = TestIdentifierUtils.FirstIdentifier;

    /// <summary>Whether an identifier names a team held here.</summary>
    /// <param name="teamIdentifier">Identifier to test.</param>
    public bool Holds(int teamIdentifier)
    {
        lock (gate)
        {
            return teams.ContainsKey(teamIdentifier);
        }
    }

    /// <summary>Returns a simulated team with its roster, when one is held.</summary>
    /// <param name="teamIdentifier">Team to find.</param>
    public EventTeam? FindTeam(int teamIdentifier)
    {
        lock (gate)
        {
            return teams.GetValueOrDefault(teamIdentifier);
        }
    }

    /// <summary>
    /// Returns the simulated members attached to a team, whether that team is
    /// simulated or a real row. A real row's members are not in here.
    /// </summary>
    /// <param name="teamIdentifier">Team to read.</param>
    public IReadOnlyList<EventTeamMember> MembersFor(int teamIdentifier)
    {
        lock (gate)
        {
            if (teams.TryGetValue(teamIdentifier, out var team))
            {
                return [.. team.Members];
            }

            return attachedMembers.TryGetValue(teamIdentifier, out var members) ? [.. members] : [];
        }
    }

    /// <summary>Finds the simulated team one character owns in a lobby.</summary>
    /// <param name="ownerCharacterIdentifier">Character to look for.</param>
    /// <param name="lobbyIdentifier">Lobby to look in.</param>
    public EventTeam? FindOwnedInLobby(int ownerCharacterIdentifier, int lobbyIdentifier)
    {
        lock (gate)
        {
            return teams.Values.FirstOrDefault(team =>
                team.OwnerCharacterIdentifier == ownerCharacterIdentifier
                && team.LobbyIdentifier == lobbyIdentifier);
        }
    }

    /// <summary>Lists the simulated teams of a lobby that are joinable.</summary>
    /// <param name="lobbyIdentifier">Lobby to list.</param>
    public List<EventTeam> FindJoinable(int lobbyIdentifier)
    {
        lock (gate)
        {
            return [.. teams.Values
                .Where(team => team.LobbyIdentifier == lobbyIdentifier
                    && team.State == EventConstants.TeamJoinableState)
                .OrderBy(team => team.Identifier)];
        }
    }

    /// <summary>Lists the simulated teams of a lobby filed under one event.</summary>
    /// <param name="lobbyIdentifier">Lobby to list.</param>
    /// <param name="eventIdentifier">Event to filter on.</param>
    public List<EventTeam> FindByLobbyAndEvent(int lobbyIdentifier, int eventIdentifier)
    {
        lock (gate)
        {
            return [.. teams.Values
                .Where(team => team.LobbyIdentifier == lobbyIdentifier
                    && team.EventIdentifier == eventIdentifier)
                .OrderBy(team => team.Identifier)];
        }
    }

    /// <summary>
    /// Creates a simulated team whose roster is entirely simulated. Every slot is
    /// ready, because a team that is created to be paired has nothing left to
    /// decide.
    /// </summary>
    /// <param name="name">Display name of the team.</param>
    /// <param name="matchType">Match type, which is the lobby selector.</param>
    /// <param name="lobbyIdentifier">Lobby the team belongs to.</param>
    /// <param name="eventIdentifier">Event the team is filed under.</param>
    /// <param name="roster">Test characters the roster is filled from.</param>
    public EventTeam CreateTeam(
        string name,
        int matchType,
        int lobbyIdentifier,
        int eventIdentifier,
        IReadOnlyList<Character> roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            var team = new EventTeam
            {
                Identifier = AllocateTeamIdentifierLocked(),
                LobbyIdentifier = lobbyIdentifier,
                EventIdentifier = eventIdentifier,
                MatchType = matchType,
                Name = name,
                Comment = string.Empty,
                FlagBits = 0,
                Password = string.Empty,
                State = EventConstants.TeamJoinableState,
                Sequence = 1,
                CreatedAt = now,
                UpdatedAt = now,
            };

            for (var slot = 0; slot < roster.Count; slot++)
            {
                team.Members.Add(CreateMemberLocked(
                    team.Identifier,
                    slot,
                    roster[slot],
                    EventConstants.ParticipantReadyState));
            }

            team.OwnerCharacterIdentifier = team.Members.FirstOrDefault()?.CharacterIdentifier ?? 0;
            teams[team.Identifier] = team;
            return team;
        }
    }

    /// <summary>
    /// Adds a simulated member to a team, whether the team is simulated or a real
    /// row. The slot is chosen by the caller because a real team's occupied slots
    /// are in the database, which this service does not read.
    /// </summary>
    /// <param name="teamIdentifier">Team the member is added to.</param>
    /// <param name="slot">Roster slot to fill.</param>
    /// <param name="character">Test character filling the slot.</param>
    /// <param name="state">Participant state to record.</param>
    public EventTeamMember AddMember(
        int teamIdentifier,
        int slot,
        Character character,
        int state)
    {
        ArgumentNullException.ThrowIfNull(character);

        lock (gate)
        {
            var member = CreateMemberLocked(teamIdentifier, slot, character, state);
            if (teams.TryGetValue(teamIdentifier, out var team))
            {
                team.Members.Add(member);
                team.UpdatedAt = DateTimeOffset.UtcNow;
                return member;
            }

            if (!attachedMembers.TryGetValue(teamIdentifier, out var members))
            {
                members = [];
                attachedMembers[teamIdentifier] = members;
            }

            members.Add(member);
            return member;
        }
    }

    /// <summary>Moves a simulated team into a new state.</summary>
    /// <param name="teamIdentifier">Team to move.</param>
    /// <param name="state">State to record.</param>
    /// <returns>Whether the team is held here.</returns>
    public bool SetTeamState(int teamIdentifier, int state)
    {
        lock (gate)
        {
            if (!teams.TryGetValue(teamIdentifier, out var team))
            {
                return false;
            }

            team.State = state;
            team.UpdatedAt = DateTimeOffset.UtcNow;
            return true;
        }
    }

    /// <summary>Moves a simulated member into a new state.</summary>
    /// <param name="teamIdentifier">Team the member belongs to.</param>
    /// <param name="characterIdentifier">Test character to move.</param>
    /// <param name="state">State to record.</param>
    /// <returns>The slot the member occupies, or null when it is not here.</returns>
    public int? SetMemberState(int teamIdentifier, int characterIdentifier, int state)
    {
        lock (gate)
        {
            var member = FindMemberLocked(teamIdentifier, characterIdentifier);
            if (member is null)
            {
                return null;
            }

            member.State = state;
            if (teams.TryGetValue(teamIdentifier, out var team))
            {
                team.UpdatedAt = DateTimeOffset.UtcNow;
            }

            return member.Slot;
        }
    }

    /// <summary>Removes a simulated member from a team.</summary>
    /// <param name="teamIdentifier">Team the member belongs to.</param>
    /// <param name="characterIdentifier">Test character to remove.</param>
    /// <returns>Whether a member was removed.</returns>
    public bool RemoveMember(int teamIdentifier, int characterIdentifier)
    {
        lock (gate)
        {
            if (teams.TryGetValue(teamIdentifier, out var team))
            {
                var member = team.Members.FirstOrDefault(candidate =>
                    candidate.CharacterIdentifier == characterIdentifier);
                if (member is null)
                {
                    return false;
                }

                team.Members.Remove(member);
                team.UpdatedAt = DateTimeOffset.UtcNow;
                return true;
            }

            if (!attachedMembers.TryGetValue(teamIdentifier, out var members))
            {
                return false;
            }

            var attached = members.FirstOrDefault(candidate =>
                candidate.CharacterIdentifier == characterIdentifier);
            if (attached is null)
            {
                return false;
            }

            members.Remove(attached);
            if (members.Count == 0)
            {
                attachedMembers.Remove(teamIdentifier);
            }

            return true;
        }
    }

    /// <summary>Removes a simulated team with its roster.</summary>
    /// <param name="teamIdentifier">Team to remove.</param>
    /// <returns>Whether the team was held here.</returns>
    public bool RemoveTeam(int teamIdentifier)
    {
        lock (gate)
        {
            return teams.Remove(teamIdentifier);
        }
    }

    /// <summary>
    /// Forgets every simulated team and member. Each test run begins here, so a
    /// second run does not stack on the roster the first one built.
    /// </summary>
    public void Reset()
    {
        lock (gate)
        {
            teams.Clear();
            attachedMembers.Clear();
        }
    }

    private EventTeamMember? FindMemberLocked(int teamIdentifier, int characterIdentifier)
    {
        if (teams.TryGetValue(teamIdentifier, out var team))
        {
            return team.Members.FirstOrDefault(candidate =>
                candidate.CharacterIdentifier == characterIdentifier);
        }

        return attachedMembers.TryGetValue(teamIdentifier, out var members)
            ? members.FirstOrDefault(candidate => candidate.CharacterIdentifier == characterIdentifier)
            : null;
    }

    private static EventTeamMember CreateMemberLocked(
        int teamIdentifier,
        int slot,
        Character character,
        int state) =>
        new()
        {
            TeamIdentifier = teamIdentifier,
            Slot = slot,
            CharacterIdentifier = character.Identifier,
            Name = character.Name,
            State = state,
            Experience = Math.Max(0, character.Experience),
        };

    private int AllocateTeamIdentifierLocked() => nextTeamIdentifier++;
}
