using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>Why a request to create or fill a testing team could not be carried out.</summary>
public enum FakeTeamFillOutcome
{
    /// <summary>The team was created or filled.</summary>
    Filled,

    /// <summary>No team of that name exists in the lobby.</summary>
    TeamNotFound,

    /// <summary>The team's roster has no free slot.</summary>
    TeamFull,

    /// <summary>The test-character pool holds too few free characters.</summary>
    NoPoolCharacters,
}

/// <summary>What creating or filling a testing team did.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Snapshot">Roster after the change, when it was made.</param>
/// <param name="AddedSlots">Slots that were filled, in order.</param>
/// <param name="TeamIdentifier">Row the team has.</param>
public readonly record struct FakeTeamFillResult(
    FakeTeamFillOutcome Outcome,
    EventSnapshot? Snapshot,
    IReadOnlyList<int> AddedSlots,
    int TeamIdentifier);

/// <summary>One team of a lobby, as the testing tools list it.</summary>
/// <param name="Identifier">Row identifier of the team.</param>
/// <param name="Name">Display name of the team.</param>
/// <param name="State">Lifecycle state the client reads.</param>
/// <param name="MemberCount">Players the roster holds, leader included.</param>
/// <param name="EventIdentifier">Event the team is filed under.</param>
public readonly record struct FakeTeamRecord(
    int Identifier,
    string Name,
    int State,
    int MemberCount,
    int EventIdentifier);

/// <summary>
/// Creates and fills the teams the testing tools use, as ordinary rows: a real
/// <c>event_teams</c> team whose members are drawn from the test-character pool.
/// <para>
/// There is no second store. A test team is a team row like any other and its
/// players are real character rows, so everything that reads a team — the lists,
/// the queue, the assignment, the outcome — reads one kind of data and cannot
/// tell a testing team from a player's own. What used to be held in memory and
/// written out at the moment it was paired is simply written at the moment it is
/// created.
/// </para>
/// <para>
/// The players come from the pool rather than being created here, so a whole
/// testing session reuses the same characters and the character table does not
/// grow with every team that is tried. A team that is removed returns its
/// players to the pool, because the mark of being taken is the team membership
/// itself.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="pool">The test characters the players are taken from.</param>
public sealed class FakeTeamService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    TestCharacterPoolService pool)
    : DomainService(contextFactory)
{
    /// <summary>Most players one team may hold.</summary>
    public const int MaximumPerTeam = EventConstants.TeamMemberLimit;

    /// <summary>Creates a real team of test players in a lobby.</summary>
    /// <param name="mode">Lobby mode the team is formed in.</param>
    /// <param name="lobbyIdentifier">Lobby the team is formed in.</param>
    /// <param name="eventIdentifier">Event the team is filed under.</param>
    /// <param name="teamName">Name to give the team, or blank for a composed one.</param>
    /// <param name="playerCount">How many players the team holds, leader included.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<FakeTeamFillResult> CreateTeamAsync(
        int mode,
        int lobbyIdentifier,
        int eventIdentifier,
        string teamName,
        int playerCount,
        CancellationToken cancellationToken = default)
    {
        if (playerCount < 1 || playerCount > MaximumPerTeam)
        {
            return new FakeTeamFillResult(FakeTeamFillOutcome.TeamNotFound, null, [], 0);
        }

        var players = await pool.TakeAsync(playerCount, cancellationToken);
        if (players.Count < playerCount)
        {
            return new FakeTeamFillResult(FakeTeamFillOutcome.NoPoolCharacters, null, [], 0);
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var team = new EventTeam
        {
            OwnerCharacterIdentifier = players[0].Identifier,
            LobbyIdentifier = lobbyIdentifier,
            EventIdentifier = eventIdentifier,
            MatchType = mode,
            Name = ComposeTeamName(teamName),
            Comment = string.Empty,
            FlagBits = 0,
            Password = string.Empty,
            State = EventConstants.TeamJoinableState,
            Sequence = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        for (var slot = 0; slot < players.Count; slot++)
        {
            team.Members.Add(NewMember(slot, players[slot], EventConstants.ParticipantPendingState));
        }

        context.EventTeams.Add(team);
        await context.SaveChangesAsync(cancellationToken);

        return new FakeTeamFillResult(
            FakeTeamFillOutcome.Filled,
            EventTeamService.BuildSnapshot(team),
            [.. Enumerable.Range(0, players.Count)],
            team.Identifier);
    }

    /// <summary>
    /// Adds test players from the pool to the team of a lobby, whether it is a
    /// testing team or a player's own.
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby the team is in.</param>
    /// <param name="teamName">Name of the team to fill.</param>
    /// <param name="count">How many players to add.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<FakeTeamFillResult> FillTeamAsync(
        int lobbyIdentifier,
        string teamName,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count < 1 || count > MaximumPerTeam || string.IsNullOrWhiteSpace(teamName))
        {
            return new FakeTeamFillResult(FakeTeamFillOutcome.TeamNotFound, null, [], 0);
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var team = await FindByNameAsync(context, lobbyIdentifier, teamName, cancellationToken);
        if (team is null)
        {
            return new FakeTeamFillResult(FakeTeamFillOutcome.TeamNotFound, null, [], 0);
        }

        var free = MaximumPerTeam - team.Members.Count(member => member.Slot >= 0);
        var wanted = Math.Min(count, Math.Max(0, free));
        var players = wanted == 0
            ? []
            : await pool.TakeAsync(wanted, cancellationToken);
        if (players.Count == 0)
        {
            return free <= 0
                ? new FakeTeamFillResult(FakeTeamFillOutcome.TeamFull, null, [], team.Identifier)
                : new FakeTeamFillResult(FakeTeamFillOutcome.NoPoolCharacters, null, [], team.Identifier);
        }

        var addedSlots = new List<int>();
        var memberState = EventTeamRegistrationUtils.ParticipantStateFor(team.State);
        foreach (var player in players)
        {
            var slot = NextFreeSlot(team);
            if (slot < 0)
            {
                break;
            }

            team.Members.Add(NewMember(slot, player, memberState));
            addedSlots.Add(slot);
        }

        // The sequence is not advanced for the roster change. The clients
        // holding this team cached its serial from the reply that created it,
        // and every 0x4918 is discarded unless it carries that same serial, so
        // moving it here is what makes an added player invisible.
        team.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return new FakeTeamFillResult(
            FakeTeamFillOutcome.Filled,
            EventTeamService.BuildSnapshot(team),
            addedSlots,
            team.Identifier);
    }

    /// <summary>
    /// Moves a stored team into a new state, and optionally forces a member
    /// state on its whole roster.
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby the team is in.</param>
    /// <param name="teamName">Name of the team to change.</param>
    /// <param name="state">Team state to store.</param>
    /// <param name="memberState">Member state to force, or null to leave the roster.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<FakeTeamFillResult> SetStateAsync(
        int lobbyIdentifier,
        string teamName,
        int state,
        int? memberState,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return new FakeTeamFillResult(FakeTeamFillOutcome.TeamNotFound, null, [], 0);
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var team = await FindByNameAsync(context, lobbyIdentifier, teamName, cancellationToken);
        if (team is null)
        {
            return new FakeTeamFillResult(FakeTeamFillOutcome.TeamNotFound, null, [], 0);
        }

        team.State = state;
        if (memberState is int forced)
        {
            foreach (var member in team.Members)
            {
                member.State = forced;
            }
        }

        team.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return new FakeTeamFillResult(
            FakeTeamFillOutcome.Filled,
            EventTeamService.BuildSnapshot(team),
            [],
            team.Identifier);
    }

    /// <summary>Removes a stored team of a lobby and its roster.</summary>
    /// <param name="lobbyIdentifier">Lobby the team is in.</param>
    /// <param name="teamName">Name of the team to remove.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The team that was removed, or null when no team of that name is held.</returns>
    public async Task<FakeTeamRecord?> RemoveTeamAsync(
        int lobbyIdentifier,
        string teamName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return null;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var team = await FindByNameAsync(context, lobbyIdentifier, teamName, cancellationToken);
        if (team is null)
        {
            return null;
        }

        var record = new FakeTeamRecord(
            team.Identifier,
            team.Name,
            team.State,
            team.Members.Count,
            team.EventIdentifier);
        context.EventTeams.Remove(team);
        await context.SaveChangesAsync(cancellationToken);
        return record;
    }

    private static EventTeamMember NewMember(int slot, TestCharacterPoolEntry player, int state) =>
        new()
        {
            Slot = slot,
            CharacterIdentifier = player.Identifier,
            Name = player.Name,
            State = state,
            Experience = TestCharacterPoolService.TestExperience,
        };

    private static Task<EventTeam?> FindByNameAsync(
        Mgo2DatabaseContext context,
        int lobbyIdentifier,
        string teamName,
        CancellationToken cancellationToken)
    {
        var lowered = teamName.Trim().ToLowerInvariant();
        return context.EventTeams
            .Include(team => team.Members)
            .Where(team => team.LobbyIdentifier == lobbyIdentifier
                && team.Name.ToLower() == lowered)
            .OrderBy(team => team.Identifier)
            .FirstOrDefaultAsync(cancellationToken);
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

    private static string ComposeTeamName(string teamName) =>
        string.IsNullOrWhiteSpace(teamName)
            ? $"{TestPlayerNameUtils.Prefix}team-{Random.Shared.Next(100, 1000)}"
            : teamName.Trim();
}
