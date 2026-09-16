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
            context.RoundWeaponStats.Add(new RoundWeaponStat
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

        // For each report where the viewer is involved, compute the "other"
        // character: the target when the viewer is the host, and the host when
        // the viewer is the target.  This avoids a Concat after client
        // projection, which EF Core cannot translate to SQL.
        var meetings = context.RoundReports
            .Where(r => r.HostCharacterIdentifier == viewerCharacterIdentifier ||
                        r.TargetCharacterIdentifier == viewerCharacterIdentifier)
            .Select(r => new
            {
                OtherCharacterIdentifier =
                    r.HostCharacterIdentifier == viewerCharacterIdentifier
                        ? r.TargetCharacterIdentifier
                        : r.HostCharacterIdentifier,
                r.CreatedAt,
                r.LobbySubtype,
            });

        var grouped = await (
            from meeting in meetings
            where meeting.OtherCharacterIdentifier != viewerCharacterIdentifier
            join character in context.Characters
                on meeting.OtherCharacterIdentifier equals character.Identifier
            group meeting by new { meeting.OtherCharacterIdentifier, character.Name }
            into grouping
            orderby grouping.Max(row => row.CreatedAt) descending
            select new
            {
                CharacterIdentifier = grouping.Key.OtherCharacterIdentifier,
                grouping.Key.Name,
                LastMet = grouping.Max(row => row.CreatedAt),
                LobbySubtype = grouping.Max(row => row.LobbySubtype),
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

}
