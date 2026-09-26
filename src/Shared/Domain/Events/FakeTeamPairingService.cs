using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>Why an in-memory team could not be made pairable.</summary>
public enum FakeTeamPairingOutcome
{
    /// <summary>The team became a row and is in the queue.</summary>
    Materialised,

    /// <summary>No in-memory team of that name is held in the lobby.</summary>
    TeamNotFound,

    /// <summary>The mode is not one the Survival queue pairs.</summary>
    NotSurvival,
}

/// <summary>What making an in-memory team pairable did.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="TeamIdentifier">Row the team now has, when it has one.</param>
/// <param name="TeamName">Name the team was found under.</param>
public readonly record struct FakeTeamPairingResult(
    FakeTeamPairingOutcome Outcome,
    int TeamIdentifier,
    string TeamName);

/// <summary>
/// Writes an in-memory team out as a real <c>event_teams</c> row, so the
/// Survival queue can pair it against a real one.
/// <para>
/// A pairing is a durable row naming two team identifiers, and everything
/// downstream of it — the host lease, the assignment packets, the outcome
/// inference and the reward ledger — re-reads both sides as
/// <c>event_teams</c> rows. A team that exists only in memory therefore cannot
/// take part in one: the match would be created and then silently never reach a
/// host, because every one of those readers finds a team missing and gives up.
/// Rather than teach six services to resolve a team from two places, the team
/// is given a row at the moment it is asked to play, and from that point it is
/// an ordinary team the rest of the way through.
/// </para>
/// <para>
/// That is the one moment an in-memory team stops being in-memory, and it is
/// deliberate rather than a leak. A team only becomes a row because somebody
/// asked it to be paired, which is the one operation memory alone cannot
/// express. Creating, filling, re-stating and forgetting a team stay in memory,
/// because none of them is visible to another process.
/// </para>
/// <para>
/// The row is left behind afterwards rather than removed with the match, so a
/// pairing can be inspected once it has been played. A row that came from here
/// is recognised by its identifiers: the team and every member sit in the fake
/// range, so nothing about it can be mistaken for a player's own.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="fakeTeams">The in-memory teams this lobby is holding.</param>
public sealed class FakeTeamPairingService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    FakeTeamService fakeTeams)
{
    /// <summary>
    /// Writes one in-memory team out as a row and marks it queued, which is the
    /// state the Survival queue pairs on.
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby the team is in.</param>
    /// <param name="teamName">Name of the in-memory team.</param>
    /// <param name="mode">Mode of that lobby, which decides whether it can be paired at all.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>What happened.</returns>
    public async Task<FakeTeamPairingResult> MaterialiseAsync(
        int lobbyIdentifier,
        string teamName,
        int mode,
        CancellationToken cancellationToken = default)
    {
        // The queue this reaches is the Survival one. A Tournament entrant is
        // seeded into a bracket and frozen into a roster, and a team that
        // existed only in memory has neither; letting one into that draw would
        // put a team the bracket cannot read into real tournament state.
        if (mode != EventConstants.SurvivalSelector)
        {
            return new FakeTeamPairingResult(
                FakeTeamPairingOutcome.NotSurvival,
                0,
                teamName);
        }

        // The team is taken out of memory as it is written out. Leaving it
        // behind would put one team in two places: the row would be queued and
        // the memory copy would still be listed and fillable, so a second
        // pairing request would find a team that is already a row.
        var team = fakeTeams.RemoveTeam(lobbyIdentifier, teamName);
        if (team is null)
        {
            return new FakeTeamPairingResult(
                FakeTeamPairingOutcome.TeamNotFound,
                0,
                teamName);
        }

        var row = BuildRow(team, lobbyIdentifier);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // A team that was already written out is not written twice. The pairing
        // is idempotent because the page's button can be pressed again after a
        // lobby answered a request it had already carried out.
        var alreadyWritten = await context.EventTeams
            .AnyAsync(candidate => candidate.Identifier == row.Identifier, cancellationToken);
        if (alreadyWritten)
        {
            return new FakeTeamPairingResult(
                FakeTeamPairingOutcome.Materialised,
                row.Identifier,
                row.Name);
        }

        context.EventTeams.Add(row);
        await context.SaveChangesAsync(cancellationToken);

        return new FakeTeamPairingResult(
            FakeTeamPairingOutcome.Materialised,
            row.Identifier,
            row.Name);
    }

    /// <summary>
    /// Projects one in-memory team into the row it becomes.
    /// <para>
    /// It is separated from the write because it is the part with rules in it —
    /// which identifier the row keeps, what state it is queued in, and what
    /// state each member is written ready in — and those are worth reading
    /// without a database in the way.
    /// </para>
    /// </summary>
    /// <param name="team">In-memory team to project.</param>
    /// <param name="lobbyIdentifier">Lobby the team is in.</param>
    /// <returns>The row the team is written out as.</returns>
    public static EventTeam BuildRow(FakeTeam team, int lobbyIdentifier)
    {
        ArgumentNullException.ThrowIfNull(team);

        var now = DateTimeOffset.UtcNow;
        var row = new EventTeam
        {
            // The fake identifier is reused rather than replaced: the client
            // cached this number when the team was listed, and a match packet
            // carrying a different one is dropped against the client's own copy.
            Identifier = team.Identifier,
            OwnerCharacterIdentifier = team.OwnerCharacterIdentifier,
            LobbyIdentifier = lobbyIdentifier,
            EventIdentifier = team.EventIdentifier,

            // The queue is keyed by lobby and match type, and pairing only
            // happens inside one, so the row carries the Survival selector
            // rather than whatever the lobby's own subtype happens to be.
            MatchType = EventConstants.SurvivalSelector,
            Name = team.Name,
            Comment = string.Empty,
            FlagBits = 0,
            Password = string.Empty,

            // Queued is what a decided team holds, and it is also the state the
            // queue treats as "waiting for an opponent". The roster is written
            // ready rather than pending because there is nobody here to press
            // the button that decides, and a team queued with an undecided
            // roster would be released again on the next reconcile.
            State = EventConstants.TeamRegisteredState,
            Sequence = team.Sequence,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var slot = 0;
        foreach (var member in team.Members)
        {
            row.Members.Add(new EventTeamMember
            {
                Slot = slot,
                CharacterIdentifier = member.CharacterIdentifier,
                Name = member.Name,
                State = EventConstants.ParticipantReadyState,
                Experience = FakeTeam.DefaultExperience,
            });
            slot++;
        }

        return row;
    }
}
