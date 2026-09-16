using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Domain.Instructors;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Serves the per-mode statistics grids: the record header, the two mode
/// matrices and the tail that releases the client's wait slot. The layout of each
/// packet lives in <see cref="PersonalStatisticsPayloadBuilder"/>.
/// </summary>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="statisticsService">Service that owns the lifetime statistics.</param>
/// <param name="gameService">Service that owns the rooms and host ratings, and the training totals they accumulate.</param>
/// <param name="instructorService">Service that owns the instructor relationship and reviews.</param>
/// <param name="titleService">Service that owns the titles the character has latched.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetPersonalStatsHandler(
    CharacterService characterService,
    CharacterStatisticsService statisticsService,
    GameService gameService,
    InstructorService instructorService,
    CharacterTitleService titleService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        if (reader.Remaining < 4)
        {
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.GetPersonalStatsHeader,
                BuildResult(ErrorCodeConstants.ResultGeneral),
                cancellationToken);
            return;
        }

        var targetIdentifier = (int)reader.ReadUInt32();
        var character = targetIdentifier > 0
            ? await characterService.FindByIdAsync(targetIdentifier, cancellationToken)
            : null;

        if (character is null)
        {
            // The client's own code for a deleted character, sent unmasked so
            // it matches its error table.
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.GetPersonalStatsHeader,
                BuildResult(ErrorCodeConstants.ResultCharacterGone),
                cancellationToken);
            return;
        }

        var statistics = await statisticsService.FindByCharacterIdentifierAsync(targetIdentifier, cancellationToken);
        var clan = await characterService.GetClanInformationAsync(targetIdentifier, cancellationToken);
        var friendsAndBlocked = await characterService.GetFriendsAndBlockedAsync(targetIdentifier, cancellationToken);
        var instructor = await instructorService.FindInstructorAsync(targetIdentifier, cancellationToken);
        var instructorScore = await instructorService.GetInstructorScoreAsync(targetIdentifier, cancellationToken: cancellationToken);
        var trainingSeconds = await gameService.GetTrainingSecondsAsync(targetIdentifier, cancellationToken);
        var titleMask = await titleService.FindTitleMaskAsync(targetIdentifier, cancellationToken);

        var hostRatings = await gameService.GetHostRatingSummariesAsync([targetIdentifier], cancellationToken);
        var hostRating = hostRatings.TryGetValue(targetIdentifier, out var summary) ? summary : default;

        var friends = friendsAndBlocked.Where(entry => entry.Type == 0).Select(entry => entry.TargetIdentifier).ToList();
        var blocked = friendsAndBlocked.Where(entry => entry.Type == 1).Select(entry => entry.TargetIdentifier).ToList();

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetPersonalStatsHeader,
            PersonalStatisticsPayloadBuilder.BuildHeader(
                character,
                targetIdentifier,
                friends,
                blocked,
                clan,
                instructor,
                instructorScore,
                hostRating,
                titleMask),
            cancellationToken);

        // The cumulative page must precede the weekly page, because receiving
        // the first zeroes the whole grid region including the second.
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetPersonalStatsPage,
            PersonalStatisticsPayloadBuilder.BuildMatrix(statistics, 0, character),
            cancellationToken);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetPersonalStatsPage,
            PersonalStatisticsPayloadBuilder.BuildMatrix(statistics, 1, character),
            cancellationToken);

        // Both periods of each counter the tail carries. The training totals have no
        // period model of their own — they are running totals with no per-session
        // history — so the monthly record carries zero there rather than repeating
        // the lifetime figure, which would claim a month we never measured.
        var studentsTrained = await instructorService.CountStudentsTrainedAsync(targetIdentifier, cancellationToken: cancellationToken);
        var studentsTrainedThisMonth = await instructorService.CountStudentsTrainedAsync(
            targetIdentifier,
            currentMonth: true,
            cancellationToken);

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetPersonalStatsTail,
            PersonalStatisticsPayloadBuilder.BuildTail(studentsTrained, studentsTrainedThisMonth, trainingSeconds),
            cancellationToken);
    }

    private static byte[] BuildResult(uint result)
    {
        var writer = new PacketWriter();
        writer.WriteUInt32(result);
        return writer.Build();
    }
}
