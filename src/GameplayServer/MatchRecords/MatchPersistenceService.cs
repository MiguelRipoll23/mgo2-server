using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Domain.Games;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Writes the gameplay server's own changes back to the games domain's rows.
/// </summary>
/// <remarks>
/// <para>
/// The lobby owns the join and the team choice, and writes both. A player that
/// reaches the gameplay server without that lobby pass is still a member of the
/// room, so its row and its side are written here as well. The calls are
/// idempotent: a row the lobby already added is left in place.
/// </para>
/// <para>
/// A round is written when it closes, from a snapshot of the score and the
/// roster taken before the next open clears them. The report is the round's,
/// including its winning side, and it is written once per human player.
/// </para>
/// </remarks>
/// <param name="gameService">The games domain's service, which owns the rows.</param>
/// <param name="roundReports">Writer of a round's per-player report.</param>
/// <param name="match">This host's published room.</param>
/// <param name="matchSettings">The room's own rotation and team policy.</param>
/// <param name="roster">The room's roster, which names its human players.</param>
/// <param name="lobbySlots">Reader of the sides the room's rows store.</param>
/// <param name="score">The host's score for the round being written.</param>
/// <param name="hostIdentity">Identity the room is hosted under.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class MatchPersistenceService(
    GameService gameService,
    RoundReportService roundReports,
    MatchService match,
    MatchSettingsService matchSettings,
    RoomRosterService roster,
    LobbySlotService lobbySlots,
    MatchScoreService score,
    HostIdentityService hostIdentity,
    ILogger<MatchPersistenceService> logger)
{
    /// <summary>
    /// Adds a player to the room's row and, when the room places joiners itself,
    /// gives it the lighter side.
    /// </summary>
    /// <remarks>
    /// It runs after the roster has accepted the profile, so a peer whose join
    /// did not pass through the lobby still has a row and a side to be read from.
    /// The call is fire-and-forget from the join path: a database that is slow
    /// must not hold the roster answer the client is waiting for.
    /// </remarks>
    /// <param name="characterIdentifier">Character that joined the room.</param>
    /// <param name="cancellationToken">Token that cancels the writes.</param>
    public async Task EnsurePlayerAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        var gameIdentifier = match.MatchIdentifier;
        if (gameIdentifier == 0 || characterIdentifier <= 0)
        {
            return;
        }

        try
        {
            var added = await gameService.AddPlayerAsync(
                gameIdentifier,
                characterIdentifier,
                cancellationToken);

            if (added)
            {
                if (matchSettings.AutoAssignTeams)
                {
                    await gameService.AssignBalancedTeamAsync(
                        gameIdentifier,
                        characterIdentifier,
                        cancellationToken);
                }

                logger.LogInformation(
                    "Character {CharacterIdentifier} joined room {GameIdentifier}; its roster row was written",
                    characterIdentifier,
                    gameIdentifier);
                return;
            }

            var teams = await gameService.GetPlayerTeamsAsync(gameIdentifier, cancellationToken);
            if (teams.TryGetValue(characterIdentifier, out var team) && RoomSideUtils.IsSide(team))
            {
                return;
            }

            if (!matchSettings.AutoAssignTeams)
            {
                return;
            }

            await gameService.AssignBalancedTeamAsync(
                gameIdentifier,
                characterIdentifier,
                cancellationToken);
            logger.LogInformation(
                "Character {CharacterIdentifier} had no side in room {GameIdentifier}; it was balanced onto one",
                characterIdentifier,
                gameIdentifier);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Character {CharacterIdentifier} could not be written to room {GameIdentifier}; the room " +
                "keeps serving it from memory",
                characterIdentifier,
                gameIdentifier);
        }
    }

    /// <summary>
    /// Writes one round report for every human player the round was played with.
    /// </summary>
    /// <remarks>
    /// The score, the roster and the rounds-won counters are copied before the
    /// first await, because the next round can open while the database is being
    /// reached and clear them. The winning side is the closing result's; a result
    /// that names no side leaves the win counters as they were.
    /// </remarks>
    /// <param name="round">The rotation entry the round played.</param>
    /// <param name="roundResult">Result code the round closed on, or <c>null</c>.</param>
    /// <param name="seconds">Seconds the round ran for.</param>
    /// <param name="cancellationToken">Token that cancels the writes.</param>
    public async Task RecordRoundAsync(
        MatchRotationEntry round,
        byte? roundResult,
        int seconds,
        CancellationToken cancellationToken = default)
    {
        var gameIdentifier = match.MatchIdentifier;
        if (gameIdentifier == 0)
        {
            return;
        }

        var members = roster.Snapshot()
            .Where(member => !member.IsBot && member.CharacterId > 0)
            .ToArray();
        if (members.Length == 0)
        {
            return;
        }

        var stats = score.SnapshotRound();
        var wins = members.ToDictionary(
            member => member.SessionSlot,
            member => score.RoundsWon(member.SessionSlot));
        var winningTeam = roundResult is { } result
            ? MatchRecordBodiesUtils.TeamOfResult(result)
            : null;
        var hostIdentifier = (int)hostIdentity.PeerIdentifier;

        try
        {
            var sides = (await lobbySlots.ReadAsync(cancellationToken))
                .ToDictionary(slot => slot.Slot, slot => slot.Team);

            foreach (var member in members)
            {
                var slot = member.SessionSlot;
                stats.TryGetValue(slot, out var player);
                player ??= new PlayerRoundStats(0, 0, 0, 0, 0);

                var side = sides.GetValueOrDefault(slot);
                var winsForPlayer = wins.GetValueOrDefault(slot);
                if (winningTeam is not null && side == winningTeam)
                {
                    winsForPlayer++;
                }

                await roundReports.InsertAsync(
                    new RoundReportInput(
                        gameIdentifier,
                        hostIdentifier,
                        member.CharacterId,
                        Clamped(round.Rule),
                        winningTeam ?? 0,
                        Math.Max(0, seconds),
                        0,
                        false,
                        match.LobbySubtype,
                        new RoundCounters(
                            Clamped(winsForPlayer),
                            Clamped(player.Kills),
                            Clamped(player.Deaths),
                            Clamped(player.Score),
                            0,
                            0,
                            Clamped(player.HeadshotKills),
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            0,
                            Clamped(player.BestStreak))),
                    cancellationToken);
            }            logger.LogInformation(
                "The round in rule {Rule} on map {Map} was written for {PlayerCount} player(s) in room "
                + "{GameIdentifier}: side {WinningTeam} won in {Seconds} s",
                round.Rule,
                round.Map,
                members.Length,
                gameIdentifier,
                winningTeam,
                seconds);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "The round on map {Map} could not be written for room {GameIdentifier}; the room keeps " +
                "serving the next round",
                round.Map,
                gameIdentifier);
        }
    }

    private static short Clamped(int value) =>
        (short)Math.Clamp(value, short.MinValue, short.MaxValue);
}
