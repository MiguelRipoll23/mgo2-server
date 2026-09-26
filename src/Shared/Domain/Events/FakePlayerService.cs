using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// One player that exists only in this process's memory.
/// </summary>
/// <param name="CharacterIdentifier">Identifier the client protocol carries.</param>
/// <param name="Name">Name the client shows.</param>
/// <param name="TeamIdentifier">Team the player is in, or zero when it is in none.</param>
public readonly record struct FakePlayer(int CharacterIdentifier, string Name, int TeamIdentifier);

/// <summary>
/// Fills an event with players who are not really there.
/// <para>
/// A Survival lobby with one player in it cannot be tested: the team forms, it
/// enters, and it then waits for an opponent nobody is going to join. This is
/// how a moderator puts enough players in the lobby for that to happen.
/// </para>
/// <para>
/// The players are the one part of an event that is not a row. They have no
/// account, no character and nothing anybody could log in as, and they are gone
/// the moment this process ends — a fake player that outlived the process that
/// made it would be indistinguishable from a real one. Their identifiers are
/// handed out from a range no character row can occupy, and the roster is held
/// here so the two cannot collide.
/// </para>
/// <para>
/// The team they form is a real row. That is the deliberate half: matchmaking
/// pairs teams, the battle list is built from them, and the host lease is drawn
/// against a match between them, so a team that existed only in memory would be
/// a pairing no real client could ever see or be drawn against.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class FakePlayerService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory) : DomainService(contextFactory)
{
    /// <summary>Most players one request may create.</summary>
    public const int MaximumPerTeam = EventConstants.TeamMemberLimit;

    /// <summary>
    /// First identifier a fake player is given. It sits far above any character
    /// a client could have been assigned, so a fake identifier in a roster is
    /// never mistaken for a real one and never collides with one.
    /// </summary>
    private const int FirstIdentifier = 1_000_000_000;

    /// <summary>Experience a fake player is shown with, roughly a mid-level rank.</summary>
    private const int DefaultExperience = 18_000;

    private readonly Lock gate = new();
    private readonly Dictionary<int, FakePlayer> players = [];
    private int nextIdentifier = FirstIdentifier;

    /// <summary>Players this process is holding, in the order they were created.</summary>
    public IReadOnlyList<FakePlayer> List()
    {
        lock (gate)
        {
            return [.. players.Values.OrderBy(player => player.CharacterIdentifier)];
        }
    }

    /// <summary>Number of fake players this process is holding.</summary>
    public int Count
    {
        get
        {
            lock (gate)
            {
                return players.Count;
            }
        }
    }

    /// <summary>
    /// Creates a team of fake players, already entered into the event, and
    /// queues it for a match.
    /// </summary>
    /// <param name="mode">Lobby mode the team is formed in.</param>
    /// <param name="lobbyIdentifier">Lobby the team is formed in.</param>
    /// <param name="eventIdentifier">Event the team has entered.</param>
    /// <param name="count">How many players the team holds.</param>
    /// <param name="teamName">Name to give the team, or blank for a composed one.</param>
    /// <param name="playerPrefix">Name the players are shown with, or blank.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The team that was created, or null when the request was refused.</returns>
    public async Task<EventTeam?> CreateTeamAsync(
        int mode,
        int lobbyIdentifier,
        int eventIdentifier,
        int count,
        string teamName,
        string playerPrefix,
        CancellationToken cancellationToken = default)
    {
        if (count < 1 || count > MaximumPerTeam)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var team = new EventTeam
        {
            OwnerCharacterIdentifier = 0,
            LobbyIdentifier = lobbyIdentifier,
            EventIdentifier = eventIdentifier,
            MatchType = mode,
            Name = string.IsNullOrWhiteSpace(teamName) ? ComposeTeamName() : teamName.Trim(),
            Comment = string.Empty,
            FlagBits = 0,
            Password = string.Empty,

            // The team is created already entered and already queued: there is
            // nobody to press a button, so a fake team that waited for one would
            // be indistinguishable from the bug it exists to test for.
            State = EventConstants.TeamRegisteredState,
            Sequence = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        List<FakePlayer> created = [];
        for (var slot = 0; slot < count; slot++)
        {
            var identifier = NextIdentifier();
            var playerName = ComposePlayerName(playerPrefix, slot);
            team.Members.Add(new EventTeamMember
            {
                Slot = slot,
                CharacterIdentifier = identifier,
                Name = playerName,
                State = EventConstants.ParticipantReadyState,
                Experience = DefaultExperience,
            });
            created.Add(new FakePlayer(identifier, playerName, 0));
        }

        // The owner is the first slot, because a team is led by slot zero and
        // the battle list and the round draws both read it as the leader.
        team.OwnerCharacterIdentifier = created[0].CharacterIdentifier;

        await using var context = await CreateContextAsync(cancellationToken);
        context.EventTeams.Add(team);
        await context.SaveChangesAsync(cancellationToken);

        // The roster is recorded only once the team exists, so a write that
        // failed does not leave players this process believes are playing.
        lock (gate)
        {
            foreach (var player in created)
            {
                players[player.CharacterIdentifier] = player with { TeamIdentifier = team.Identifier };
            }
        }

        return team;
    }

    /// <summary>
    /// Withdraws every fake player this process is holding, so an event can be
    /// emptied again. The teams go with them: a team of players that are not
    /// there is a roster every other client would see with a member missing.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>How many players were withdrawn.</returns>
    public async Task<int> WithdrawAllAsync(CancellationToken cancellationToken = default)
    {
        int withdrawn;
        int[] teamIdentifiers;
        lock (gate)
        {
            withdrawn = players.Count;
            teamIdentifiers = [.. players.Values
                .Select(player => player.TeamIdentifier)
                .Where(identifier => identifier > 0)
                .Distinct()];
            players.Clear();
        }

        if (teamIdentifiers.Length == 0)
        {
            return withdrawn;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var teams = await context.EventTeams
            .Where(team => teamIdentifiers.Contains(team.Identifier))
            .ToListAsync(cancellationToken);

        context.EventTeams.RemoveRange(teams);
        await context.SaveChangesAsync(cancellationToken);
        return withdrawn;
    }

    private int NextIdentifier()
    {
        lock (gate)
        {
            return nextIdentifier++;
        }
    }

    private static string ComposeTeamName() =>
        $"FAKE {Random.Shared.Next(100, 1000)}";

    private static string ComposePlayerName(string prefix, int slot)
    {
        var stem = string.IsNullOrWhiteSpace(prefix) ? "Fake" : prefix.Trim();
        return $"{stem} {slot + 1}";
    }
}
