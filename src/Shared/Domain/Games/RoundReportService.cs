using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>One row of the "met players" list, derived from round reports at query time.</summary>
/// <param name="CharacterIdentifier">Identifier of the character that was met.</param>
/// <param name="Name">Name of the character that was met.</param>
/// <param name="LastMetEpochSeconds">Unix timestamp of the most recent encounter.</param>
/// <param name="LobbySubtype">Game type of the lobby the encounter happened in.</param>
public readonly record struct MetPlayer(
    int CharacterIdentifier,
    string Name,
    long LastMetEpochSeconds,
    int LobbySubtype);

/// <summary>Fields of a stored round report.</summary>
/// <param name="GameIdentifier">Room the round was played in.</param>
/// <param name="HostCharacterIdentifier">Character that hosted the round.</param>
/// <param name="TargetCharacterIdentifier">Character the report describes.</param>
/// <param name="TeamWin">Team credited with the win.</param>
/// <param name="Seconds">Duration of the round in seconds.</param>
/// <param name="Experience">Experience awarded by the round.</param>
/// <param name="Aborted">Whether the round was aborted.</param>
/// <param name="LobbySubtype">Game type of the lobby the round was played in.</param>
public sealed record RoundReportInput(
    int GameIdentifier,
    int HostCharacterIdentifier,
    int TargetCharacterIdentifier,
    short TeamWin,
    int Seconds,
    int Experience,
    bool Aborted,
    short LobbySubtype);

/// <summary>Fields of a stored weapon tally.</summary>
/// <param name="GameIdentifier">Room the tally belongs to.</param>
/// <param name="CharacterIdentifier">Character the tally belongs to.</param>
/// <param name="WeaponIdentifier">Weapon identifier.</param>
/// <param name="ValueA">First tallied value.</param>
/// <param name="ValueB">Second tallied value.</param>
/// <param name="ValueC">Third tallied value.</param>
public sealed record WeaponTallyInput(
    int GameIdentifier,
    int CharacterIdentifier,
    short WeaponIdentifier,
    short ValueA,
    short ValueB,
    short ValueC);

/// <summary>
/// Owns the round reports that back the match history, and the weapon tallies
/// reported at the end of a round.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class RoundReportService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Stores one round report.</summary>
    /// <param name="input">Report to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task InsertAsync(RoundReportInput input, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        context.RoundReports.Add(new RoundReport
        {
            GameIdentifier = input.GameIdentifier,
            HostCharacterIdentifier = input.HostCharacterIdentifier,
            TargetCharacterIdentifier = input.TargetCharacterIdentifier,
            TeamWin = input.TeamWin,
            Seconds = input.Seconds,
            Experience = input.Experience,
            Aborted = input.Aborted,
            LobbySubtype = input.LobbySubtype,
            CreatedAt = DateTime.UtcNow,
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Stores the weapon tallies of a round.</summary>
    /// <param name="tallies">Tallies to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task InsertTalliesAsync(
        IReadOnlyCollection<WeaponTallyInput> tallies,
        CancellationToken cancellationToken = default)
    {
        if (tallies.Count == 0)
        {
            return;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        foreach (var tally in tallies)
        {
            context.WeaponTallies.Add(new WeaponTally
            {
                GameIdentifier = tally.GameIdentifier,
                CharacterIdentifier = tally.CharacterIdentifier,
                WeaponIdentifier = tally.WeaponIdentifier,
                ValueA = tally.ValueA,
                ValueB = tally.ValueB,
                ValueC = tally.ValueC,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Players who shared a room with the viewer, newest encounter first:
    /// distinct counterpart characters of round reports in rooms the viewer also
    /// appears in. Either side of a report counts as having met.
    /// </summary>
    /// <param name="viewerCharacterIdentifier">Identifier of the character whose list is requested.</param>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<MetPlayer>> MetPlayersAsync(
        int viewerCharacterIdentifier,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var asHost = context.RoundReports
            .Where(report => report.HostCharacterIdentifier == viewerCharacterIdentifier)
            .Select(report => new Encounter(
                report.TargetCharacterIdentifier,
                report.GameIdentifier,
                report.CreatedAt,
                report.LobbySubtype));

        var asTarget = context.RoundReports
            .Where(report => report.TargetCharacterIdentifier == viewerCharacterIdentifier)
            .Select(report => new Encounter(
                report.HostCharacterIdentifier,
                report.GameIdentifier,
                report.CreatedAt,
                report.LobbySubtype));

        var meetings = asHost.Concat(asTarget);

        var grouped = await (
            from meeting in meetings
            join report in context.RoundReports on meeting.GameIdentifier equals report.GameIdentifier
            join character in context.Characters on meeting.CharacterIdentifier equals character.Identifier
            where (report.TargetCharacterIdentifier == viewerCharacterIdentifier ||
                   report.HostCharacterIdentifier == viewerCharacterIdentifier)
                && meeting.CharacterIdentifier != viewerCharacterIdentifier
            group new { meeting, report } by new { meeting.CharacterIdentifier, character.Name }
            into grouping
            orderby grouping.Max(row => row.report.CreatedAt) descending
            select new
            {
                grouping.Key.CharacterIdentifier,
                grouping.Key.Name,
                LastMet = grouping.Max(row => row.report.CreatedAt),
                // The highest subtype seen in the room. Taking the subtype of
                // the most recent encounter instead does not translate, and a
                // mislabel on a room reused across lobbies is cosmetic.
                LobbySubtype = grouping.Max(row => row.report.LobbySubtype),
            })
            .Take(limit)
            .ToListAsync(cancellationToken);

        return
        [
            .. grouped.Select(row => new MetPlayer(
                row.CharacterIdentifier,
                row.Name,
                new DateTimeOffset(DateTime.SpecifyKind(row.LastMet, DateTimeKind.Utc)).ToUnixTimeSeconds(),
                row.LobbySubtype)),
        ];
    }

    /// <summary>Returns whether any round report names a character in a room.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<bool> PlayedInAsync(
        int gameIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.RoundReports
            .AsNoTracking()
            .AnyAsync(
                report => report.GameIdentifier == gameIdentifier &&
                    report.TargetCharacterIdentifier == characterIdentifier,
                cancellationToken);
    }

    /// <summary>Returns the room identifiers a character appears in, newest first.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="limit">Maximum number of reports to consider.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<int>> GameIdentifiersForAsync(
        int characterIdentifier,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var identifiers = await context.RoundReports
            .AsNoTracking()
            .Where(report => report.TargetCharacterIdentifier == characterIdentifier ||
                report.HostCharacterIdentifier == characterIdentifier)
            .OrderByDescending(report => report.CreatedAt)
            .Select(report => report.GameIdentifier)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return [.. identifiers.Distinct()];
    }

    /// <summary>Returns the rooms of a set that a character appears in.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="gameIdentifiers">Room identifiers to filter.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<int>> GameIdentifiersInAsync(
        int characterIdentifier,
        IReadOnlyCollection<int> gameIdentifiers,
        CancellationToken cancellationToken = default)
    {
        if (gameIdentifiers.Count == 0)
        {
            return [];
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var identifiers = await context.RoundReports
            .AsNoTracking()
            .Where(report => gameIdentifiers.Contains(report.GameIdentifier) &&
                (report.TargetCharacterIdentifier == characterIdentifier ||
                 report.HostCharacterIdentifier == characterIdentifier))
            .Select(report => report.GameIdentifier)
            .ToListAsync(cancellationToken);

        return [.. identifiers.Distinct()];
    }

    /// <summary>One counterpart character seen in a room.</summary>
    /// <param name="CharacterIdentifier">Identifier of the counterpart.</param>
    /// <param name="GameIdentifier">Room the encounter happened in.</param>
    /// <param name="CreatedAt">Timestamp of the report.</param>
    /// <param name="LobbySubtype">Game type of the lobby.</param>
    private sealed record Encounter(int CharacterIdentifier, int GameIdentifier, DateTime CreatedAt, short LobbySubtype);
}
