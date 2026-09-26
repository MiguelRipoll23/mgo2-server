using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>Why a request to add fake players to a team could not be carried out.</summary>
public enum FakeTeamFillOutcome
{
    /// <summary>The players were added.</summary>
    Filled,

    /// <summary>No team of that name exists in the lobby.</summary>
    TeamNotFound,

    /// <summary>The team's roster has no free slot.</summary>
    TeamFull,
}

/// <summary>What filling a team did.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Snapshot">Roster after the change, when players were added.</param>
/// <param name="AddedSlots">Slots that were filled, in order.</param>
/// <param name="TeamIdentifier">Team that was filled.</param>
/// <param name="InMemory">Whether the team lives only in this process.</param>
public readonly record struct FakeTeamFillResult(
    FakeTeamFillOutcome Outcome,
    EventSnapshot? Snapshot,
    IReadOnlyList<int> AddedSlots,
    int TeamIdentifier,
    bool InMemory);

/// <summary>
/// Holds the teams and players that exist only in this lobby's memory, and adds
/// fake players to a team that already exists — a real row a player formed, or
/// an in-memory team this service created.
/// <para>
/// A fake player has no account, no character and nothing anybody could log in
/// as; its identifier is handed out from a range no character row can occupy, so
/// a fake identifier in a roster is never mistaken for a real one. A fake team
/// is the memory twin of an <c>event_teams</c> row: the team screens read it the
/// same way, but nothing about it is persisted.
/// </para>
/// <para>
/// Filling a real team is the one part that writes. It appends the roster slots
/// the client would have filled by joining, directly and already ready, because
/// there is nobody to press the button that joins a team — and it returns the
/// slots it filled so the caller can tell the team's clients.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class FakeTeamService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Most players one team may hold.</summary>
    public const int MaximumPerTeam = EventConstants.TeamMemberLimit;

    /// <summary>
    /// First identifier a fake team or player is given. It sits far above any
    /// character a client could have been assigned, so a fake identifier in a
    /// roster is never mistaken for a real one and never collides with one.
    /// <para>
    /// It is public so the parts that decide whether a roster may enter — the
    /// matchmaking readiness rule in particular — can tell a player who has no
    /// button to press from one who does.
    /// </para>
    /// </summary>
    public const int FirstFakeIdentifier = 1_000_000_000;

    private readonly Lock gate = new();
    private readonly Dictionary<int, FakeTeam> teams = [];
    private readonly Dictionary<int, FakePlayer> players = [];
    private int nextIdentifier = FirstFakeIdentifier;

    /// <summary>Teams this process is holding in one lobby, in the order they were created.</summary>
    /// <param name="lobbyIdentifier">Lobby to list.</param>
    public IReadOnlyList<FakeTeam> ListTeams(int lobbyIdentifier)
    {
        lock (gate)
        {
            return [.. teams.Values
                .Where(team => team.LobbyIdentifier == lobbyIdentifier)
                .OrderBy(team => team.Identifier)];
        }
    }

    /// <summary>Returns one in-memory team, when it is held here.</summary>
    /// <param name="teamIdentifier">Team to find.</param>
    public FakeTeam? FindTeam(int teamIdentifier)
    {
        lock (gate)
        {
            return teams.TryGetValue(teamIdentifier, out var team) ? team : null;
        }
    }

    /// <summary>
    /// Changes the state of an in-memory team of a lobby, and optionally forces a
    /// state on its whole roster.
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby the team is in.</param>
    /// <param name="teamName">Name of the in-memory team.</param>
    /// <param name="state">Team state to store.</param>
    /// <param name="participantState">Member state to force, or null to let each member follow the team.</param>
    /// <returns>The changed team, or null when no in-memory team of that name is held.</returns>
    public FakeTeam? SetState(
        int lobbyIdentifier,
        string teamName,
        int state,
        int? participantState)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return null;
        }

        var team = FindTeamByName(lobbyIdentifier, teamName);
        if (team is null)
        {
            return null;
        }

        team.SetState(state);
        team.SetParticipantState(participantState);
        return team;
    }

    /// <summary>Removes an in-memory team of a lobby and forgets its players.</summary>
    /// <param name="lobbyIdentifier">Lobby the team is in.</param>
    /// <param name="teamName">Name of the in-memory team.</param>
    /// <returns>The team that was removed, or null when no in-memory team of that name is held.</returns>
    public FakeTeam? RemoveTeam(int lobbyIdentifier, string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return null;
        }

        var team = FindTeamByName(lobbyIdentifier, teamName);
        if (team is null)
        {
            return null;
        }

        lock (gate)
        {
            teams.Remove(team.Identifier);
            foreach (var member in team.Members)
            {
                players.Remove(member.CharacterIdentifier);
            }
        }

        return team;
    }

    /// <summary>Creates a team that lives only in this process.</summary>
    /// <param name="mode">Lobby mode the team is formed in.</param>
    /// <param name="lobbyIdentifier">Lobby the team is formed in.</param>
    /// <param name="eventIdentifier">Event the team is filed under.</param>
    /// <param name="teamName">Name to give the team, or blank for a composed one.</param>
    /// <param name="playerPrefix">Name the players are shown with, or blank.</param>
    /// <param name="playerCount">How many players the team holds, leader included.</param>
    /// <returns>The team that was created, or null when the count was refused.</returns>
    public FakeTeam? CreateTeam(
        int mode,
        int lobbyIdentifier,
        int eventIdentifier,
        string teamName,
        string playerPrefix,
        int playerCount)
    {
        if (playerCount < 1 || playerCount > MaximumPerTeam)
        {
            return null;
        }

        var team = new FakeTeam
        {
            Identifier = NextIdentifier(),
            LobbyIdentifier = lobbyIdentifier,
            EventIdentifier = eventIdentifier,
            Mode = mode,
            Name = string.IsNullOrWhiteSpace(teamName) ? ComposeTeamName() : teamName.Trim(),
        };

        for (var slot = 0; slot < playerCount; slot++)
        {
            var identifier = NextIdentifier();
            var name = ComposePlayerName(playerPrefix, slot);
            if (!team.TryAddMember(identifier, name))
            {
                break;
            }

            Record(new FakePlayer(identifier, name, team.Identifier));
        }

        lock (gate)
        {
            teams[team.Identifier] = team;
        }

        return team;
    }

    /// <summary>
    /// Adds fake players to the team of a lobby, whether it is an in-memory team
    /// or a real row. The in-memory teams are resolved first, so a name they hold
    /// is never answered from a real team that happens to share it.
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby the team is in.</param>
    /// <param name="teamName">Name of the team to fill.</param>
    /// <param name="count">How many players to add.</param>
    /// <param name="playerPrefix">Name the players are shown with, or blank.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<FakeTeamFillResult> FillTeamAsync(
        int lobbyIdentifier,
        string teamName,
        int count,
        string playerPrefix,
        CancellationToken cancellationToken = default)
    {
        if (count < 1 || count > MaximumPerTeam || string.IsNullOrWhiteSpace(teamName))
        {
            return new FakeTeamFillResult(FakeTeamFillOutcome.TeamNotFound, null, [], 0, false);
        }

        var inMemory = FindTeamByName(lobbyIdentifier, teamName);
        if (inMemory is not null)
        {
            var fakeSlots = new List<int>();
            for (var added = 0; added < count; added++)
            {
                var identifier = NextIdentifier();
                var name = ComposePlayerName(playerPrefix, inMemory.Members.Count);
                if (!inMemory.TryAddMember(identifier, name))
                {
                    break;
                }

                Record(new FakePlayer(identifier, name, inMemory.Identifier));
                fakeSlots.Add(inMemory.Members.Count - 1);
            }

            return fakeSlots.Count == 0
                ? new FakeTeamFillResult(FakeTeamFillOutcome.TeamFull, null, [], inMemory.Identifier, true)
                : new FakeTeamFillResult(
                    FakeTeamFillOutcome.Filled,
                    inMemory.BuildSnapshot(),
                    fakeSlots,
                    inMemory.Identifier,
                    true);
        }

        return await FillRealTeamAsync(
            lobbyIdentifier,
            teamName,
            count,
            playerPrefix,
            cancellationToken);
    }

    /// <summary>Players this process is holding, in the order they were created.</summary>
    public IReadOnlyList<FakePlayer> ListPlayers()
    {
        lock (gate)
        {
            return [.. players.Values.OrderBy(player => player.CharacterIdentifier)];
        }
    }

    private FakeTeam? FindTeamByName(int lobbyIdentifier, string teamName)
    {
        lock (gate)
        {
            return teams.Values.FirstOrDefault(team =>
                team.LobbyIdentifier == lobbyIdentifier
                && string.Equals(team.Name, teamName.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task<FakeTeamFillResult> FillRealTeamAsync(
        int lobbyIdentifier,
        string teamName,
        int count,
        string playerPrefix,
        CancellationToken cancellationToken)
    {
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
            return new FakeTeamFillResult(FakeTeamFillOutcome.TeamNotFound, null, [], 0, false);
        }

        var addedSlots = new List<int>();
        for (var added = 0; added < count; added++)
        {
            var slot = NextFreeSlot(team);
            if (slot < 0)
            {
                break;
            }

            var identifier = NextIdentifier();
            var name = ComposePlayerName(playerPrefix, team.Members.Count);
            team.Members.Add(new EventTeamMember
            {
                Slot = slot,
                CharacterIdentifier = identifier,
                Name = name,
                State = EventTeamRegistrationUtils.ParticipantStateFor(team.State),
                Experience = FakeTeam.DefaultExperience,
            });
            addedSlots.Add(slot);
            Record(new FakePlayer(identifier, name, team.Identifier));
        }

        if (addedSlots.Count == 0)
        {
            return new FakeTeamFillResult(FakeTeamFillOutcome.TeamFull, null, [], team.Identifier, false);
        }

        // The sequence is not advanced for the roster change. The clients
        // holding this team cached its serial from the reply that created it,
        // and every 0x4918 is discarded unless it carries that same serial, so
        // moving it here is what makes an added player invisible. See
        // EventTeam.Sequence.
        team.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return new FakeTeamFillResult(
            FakeTeamFillOutcome.Filled,
            EventTeamService.BuildSnapshot(team),
            addedSlots,
            team.Identifier,
            false);
    }

    private int NextIdentifier()
    {
        lock (gate)
        {
            return nextIdentifier++;
        }
    }

    private void Record(FakePlayer player)
    {
        lock (gate)
        {
            players[player.CharacterIdentifier] = player;
        }
    }

    private static int NextFreeSlot(EventTeam team)
    {
        for (var slot = 1; slot < EventConstants.TeamMemberLimit; slot++)
        {
            if (!team.Members.Any(member => member.Slot == slot))
            {
                return slot;
            }
        }

        return -1;
    }

    private static string ComposeTeamName() =>
        $"FAKE {Random.Shared.Next(100, 1000)}";

    private static string ComposePlayerName(string prefix, int slot)
    {
        var stem = string.IsNullOrWhiteSpace(prefix) ? "Fake" : prefix.Trim();
        return $"{stem} {slot + 1}";
    }
}
