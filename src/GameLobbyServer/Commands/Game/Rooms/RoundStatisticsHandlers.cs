using System.Text.Json;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Domain.Instructors;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>Stores the statistics the host reports for one player.</summary>
/// <param name="roundStatisticsProcessor">Processor that applies the round.</param>
/// <param name="instructorService">Service that awards a pending instructor graduation.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class HostUpdateStatsHandler(
    RoundStatisticsProcessor roundStatisticsProcessor,
    InstructorService instructorService,
    SessionHelper sessionHelper,
    ILogger<HostUpdateStatsHandler> logger) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var targetIdentifier = await roundStatisticsProcessor.ProcessAsync(session, packet, cancellationToken);

        // The host reports every player as they leave, combat training included, so
        // the instructor award rides the session ending rather than the graduation
        // packet. An award that misses here is picked up by the next report instead
        // of being lost, and the award itself is latched, so a repeat is harmless.
        // The letter beats the student's mailbox fetch: leaving sends this report
        // first and the fetch a few seconds later.
        if (targetIdentifier > 0 &&
            await instructorService.AwardPendingInstructorSkillAsync(targetIdentifier, cancellationToken))
        {
            logger.LogInformation(
                "Character {TargetIdentifier} awarded the instructor skill and its announcement",
                targetIdentifier);
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.HostUpdateStatsResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>
/// Applies one end-of-round report: stores the frame as a round report, then
/// applies the experience it carries. The frame is the statistics store, so the
/// report is only stored when the target verifiably played the round — there is no
/// accumulator left for a dropped report to have already dirtied.
/// </summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="roundReportService">Service that owns the round reports.</param>
/// <param name="titleService">Service that latches the titles a round earns.</param>
/// <param name="logger">Logger of this processor.</param>
public sealed class RoundStatisticsProcessor(
    GameService gameService,
    CharacterService characterService,
    RoundReportService roundReportService,
    CharacterTitleService titleService,
    ILogger<RoundStatisticsProcessor> logger)
{
    /// <summary>Offset of the target character identifier.</summary>
    private const int TargetOffset = 0x00;

    /// <summary>Offset of the team-win word.</summary>
    private const int TeamWinOffset = 0x23;

    /// <summary>Offset of the round duration word.</summary>
    private const int SecondsOffset = 0x25;

    /// <summary>Offset of the absolute experience total.</summary>
    private const int ExperienceOffset = 0x27;

    /// <summary>Offset of the aborted flag in the longer build layout.</summary>
    private const int AbortedOffset = 0xb7;

    /// <summary>Offset of struct A, the fifteen counters.</summary>
    private const int StructAOffset = 0x05;

    /// <summary>Applies one report.</summary>
    /// <param name="session">Connection the report arrived on.</param>
    /// <param name="packet">Report to process.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The character the report described, or zero when nothing was applied.</returns>
    public async Task<int> ProcessAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } reporterIdentifier)
        {
            return 0;
        }

        var payload = packet.Payload;
        if (payload.Length < ExperienceOffset + 4)
        {
            return 0;
        }

        short ReadInt16(int offset) =>
            offset + 2 <= payload.Length ? unchecked((short)BinaryUtility.ReadUInt16BigEndian(payload, offset)) : (short)0;

        var game = session.GameIdentifier is { } gameIdentifier
            ? await gameService.FindByIdAsync(gameIdentifier, cancellationToken)
            : null;

        // The host frame carries the target; a self-report names its sender.
        var targetIdentifier = payload.Length >= TargetOffset + 4
            ? BinaryUtility.ReadUInt32BigEndian(payload, TargetOffset)
            : (uint)reporterIdentifier;
        var isHostFrame = targetIdentifier != (uint)reporterIdentifier;

        if (isHostFrame && (game is null || game.HostIdentifier != reporterIdentifier))
        {
            logger.LogWarning(
                "A host statistics report arrived from a non-host connection (character {ReporterIdentifier})",
                reporterIdentifier);
        }

        var experience = (int)BinaryUtility.ReadUInt32BigEndian(payload, ExperienceOffset);
        var aborted = payload.Length > AbortedOffset && payload[AbortedOffset] == 1;

        var played = game is not null &&
            await gameService.IsInGameAsync(game.Identifier, game.HostIdentifier, (int)targetIdentifier, cancellationToken);

        if (game is not null && !played)
        {
            logger.LogWarning(
                "Room {GameIdentifier}: a statistics report named character {TargetIdentifier}, who neither is in the room nor played the round; dropped",
                game.Identifier,
                targetIdentifier);
            return 0;
        }

        var gameMode = ResolveGameMode(game);
        var round = Parse(payload, gameMode, experience, aborted, ReadInt16);

        if (game is not null)
        {
            // The whole frame is stored: these rows are the match history and the
            // statistics every screen is derived from.
            await roundReportService.InsertAsync(
                new RoundReportInput(
                    game.Identifier,
                    game.HostIdentifier,
                    (int)targetIdentifier,
                    (short)round.GameMode,
                    (short)BinaryUtility.ReadUInt16BigEndian(payload, TeamWinOffset),
                    (short)BinaryUtility.ReadUInt16BigEndian(payload, SecondsOffset),
                    experience,
                    aborted,
                    session.LobbyIdentifier is null ? (short)0 : (short)(await ResolveLobbySubtypeAsync(game, cancellationToken)),
                    new RoundCounters(
                        (short)round.Wins,
                        (short)round.Kills,
                        (short)round.Deaths,
                        (short)round.Score,
                        (short)round.Stuns,
                        (short)round.StunsReceived,
                        (short)round.HeadshotKills,
                        (short)round.HeadshotDeaths,
                        (short)round.HeadshotStuns,
                        (short)round.HeadshotStunsReceived,
                        (short)round.LockKills,
                        (short)round.LockDeaths,
                        (short)round.LockStuns,
                        (short)round.LockStunsReceived,
                        (short)round.ConsecutiveKills)),
                cancellationToken);
        }

        await characterService.AddExperienceAsync((int)targetIdentifier, round.Experience, round.Aborted, cancellationToken);

        // Titles are evaluated after the statistics and the experience have
        // landed, because both feed the requirements. Evaluating here rather than
        // deriving the rank on read is what keeps a title once the ratio behind it
        // falls, and it is also where the player is told about it: the client
        // shows the badge, not an announcement.
        var unlocked = await titleService.EvaluateAsync((int)targetIdentifier, cancellationToken: cancellationToken);
        if (unlocked.Count > 0)
        {
            logger.LogInformation(
                "Character {TargetIdentifier} unlocked title {Titles} at the end of the round",
                targetIdentifier,
                string.Join(',', unlocked));
        }

        return (int)targetIdentifier;
    }

    private async Task<int> ResolveLobbySubtypeAsync(
        Mgo2Server.Shared.Persistence.Entities.Game game,
        CancellationToken cancellationToken)
    {
        var lobby = await gameService.FindLobbyAsync(game.LobbyIdentifier, cancellationToken);
        return lobby?.SubtypeIdentifier ?? 0;
    }

    /// <summary>Reads the active mode out of the room's stored rotation.</summary>
    private static int ResolveGameMode(Mgo2Server.Shared.Persistence.Entities.Game? game)
    {
        if (game is null)
        {
            return 0;
        }

        try
        {
            var rounds = JsonSerializer.Deserialize<List<int[]>>(game.Games);
            if (rounds is null || game.CurrentGame < 0 || game.CurrentGame >= rounds.Count)
            {
                return 0;
            }

            var entry = rounds[game.CurrentGame];
            return entry.Length > 0 ? entry[0] : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Parses the reported frame in the layout the client sends: struct A is
    /// fifteen signed counters, and the absolute experience arrives separately.
    /// </summary>
    private static RoundStatistics Parse(
        byte[] payload,
        int gameMode,
        int experience,
        bool aborted,
        Func<int, short> readInt16) =>
        new()
        {
            Aborted = aborted,
            GameMode = gameMode,
            Wins = readInt16(StructAOffset + 2 * 2),
            Kills = readInt16(StructAOffset + 2 * 0),
            Deaths = readInt16(StructAOffset + 2 * 1),
            StunsReceived = readInt16(StructAOffset + 2 * 5),
            Score = readInt16(StructAOffset + 2 * 3),
            Stuns = readInt16(StructAOffset + 2 * 4),
            HeadshotKills = readInt16(StructAOffset + 2 * 6),
            HeadshotDeaths = readInt16(StructAOffset + 2 * 7),
            HeadshotStuns = readInt16(StructAOffset + 2 * 8),
            HeadshotStunsReceived = readInt16(StructAOffset + 2 * 9),
            LockKills = readInt16(StructAOffset + 2 * 10),
            LockDeaths = readInt16(StructAOffset + 2 * 11),
            LockStuns = readInt16(StructAOffset + 2 * 12),
            LockStunsReceived = readInt16(StructAOffset + 2 * 13),
            ConsecutiveKills = readInt16(StructAOffset + 2 * 14),
            Time = readInt16(SecondsOffset),
            Experience = experience,
        };
}
