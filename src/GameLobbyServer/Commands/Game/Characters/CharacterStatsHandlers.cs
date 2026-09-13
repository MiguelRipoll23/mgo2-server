using System.Diagnostics;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Serves the per-mode statistics grids: the record header, the two mode
/// matrices and the tail that releases the client's wait slot.
/// </summary>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="statisticsService">Service that owns the lifetime statistics.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetPersonalStatsHandler(
    CharacterService characterService,
    CharacterStatisticsService statisticsService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Exact size of the header packet.</summary>
    private const int InfoSize = 0x288;

    /// <summary>Exact size of the tail packet.</summary>
    private const int TailSize = 0x24c;

    /// <summary>Number of mode rows in a matrix.</summary>
    private const int ModeRows = 8;

    /// <summary>Number of columns in a mode row.</summary>
    private const int StatColumns = 18;

    /// <summary>Column carrying the score, the only signed one.</summary>
    private const int ScoreColumn = 3;

    /// <summary>Summary column carrying the play time.</summary>
    private const int SummaryPlaySecondsColumn = 17;

    /// <summary>Summary column carrying the level.</summary>
    private const int SummaryLevelColumn = 13;

    /// <summary>Number of slots in one tail record.</summary>
    private const int TailRecordSlots = 73;

    /// <summary>Number of identifiers in each relation array.</summary>
    private const int RelationListIdentifiers = 32;

    /// <summary>Offset the comment starts at.</summary>
    private const int CommentOffset = 413;

    /// <summary>The four dead 16-bit constants after the name.</summary>
    private static readonly byte[] CharacterInfoPrefix =
    [
        0x16, 0xae, 0x03, 0x38, 0x01, 0x3e, 0x01, 0x50,
    ];

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

        var friends = friendsAndBlocked.Where(entry => entry.Type == 0).Select(entry => entry.TargetIdentifier).ToList();
        var blocked = friendsAndBlocked.Where(entry => entry.Type == 1).Select(entry => entry.TargetIdentifier).ToList();

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetPersonalStatsHeader,
            BuildHeader(character, targetIdentifier, friends, blocked, clan),
            cancellationToken);

        // The cumulative page must precede the weekly page, because receiving
        // the first zeroes the whole grid region including the second.
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetPersonalStatsPage,
            BuildMatrix(statistics, 0, character),
            cancellationToken);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetPersonalStatsPage,
            BuildMatrix(statistics, 1, character),
            cancellationToken);

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetPersonalStatsTail,
            BuildTail(),
            cancellationToken);
    }

    private static byte[] BuildResult(uint result)
    {
        var writer = new PacketWriter();
        writer.WriteUInt32(result);
        return writer.Build();
    }

    private static byte[] BuildHeader(
        Character character,
        int targetIdentifier,
        IReadOnlyList<int> friends,
        IReadOnlyList<int> blocked,
        CharacterClanInformation? clan)
    {
        var header = new PacketWriter();
        header.WriteUInt32(0);
        header.WriteUInt32((uint)targetIdentifier);
        header.WriteFixedString(character.Name, 16);
        header.WriteBytes(CharacterInfoPrefix);
        header.WriteUInt32((uint)character.Experience);
        // Login times are not recorded; zero is the honest answer.
        header.WriteUInt32(0);
        header.WriteUInt32(0);
        header.WriteUInt8(0);

        for (var index = 0; index < RelationListIdentifiers; index++)
        {
            header.WriteUInt32((uint)(index < friends.Count ? friends[index] : 0));
        }

        for (var index = 0; index < RelationListIdentifiers; index++)
        {
            header.WriteUInt32((uint)(index < blocked.Count ? blocked[index] : 0));
        }

        header.WriteUInt8(0);
        header.WriteUInt32((uint)(clan?.ClanIdentifier ?? 0));
        header.WriteFixedString(clan?.ClanName ?? string.Empty, 16);
        header.WriteUInt8(clan is not null ? 1 : 0);

        for (var index = 0; index < 12; index++)
        {
            header.WriteUInt16(0);
        }

        header.WriteUInt32(0);
        for (var index = 0; index < 9; index++)
        {
            header.WriteUInt8(0);
        }

        header.WriteUInt32(0);
        for (var index = 0; index < 14; index++)
        {
            header.WriteUInt8(0);
        }

        for (var index = 0; index < 10; index++)
        {
            header.WriteUInt8(0);
        }

        for (var index = 0; index < 5; index++)
        {
            header.WriteUInt32(0);
        }

        header.WriteUInt8(0);
        header.WriteUInt32(0);
        Debug.Assert(
            header.Size <= CommentOffset,
            "The personal-stats header grew past the comment offset; adjust the hand-computed layout.");
        header.WritePadding(CommentOffset - header.Size);
        header.WriteFixedString(character.Comment, 128);
        header.WriteUInt8(0);

        for (var index = 0; index < 9; index++)
        {
            header.WriteUInt8(0);
        }

        for (var index = 0; index < 9; index++)
        {
            header.WriteUInt32(0);
        }

        header.WriteUInt32(0);
        header.WriteFixedString(string.Empty, 16);
        header.WriteUInt32(0);
        header.WriteUInt32(0);
        header.WritePadding(16);
        header.WriteUInt8(0);
        header.WriteUInt32(0);
        header.WriteUInt32(0);
        header.WriteUInt32(0);
        header.WriteUInt32(0);
        Debug.Assert(
            header.Size <= InfoSize,
            "The personal-stats header grew past its fixed size; adjust the hand-computed layout.");
        header.WritePadding(InfoSize - header.Size);
        return header.Build();
    }

    private static byte[] BuildMatrix(CharacterStatistics? statistics, int page, Character character)
    {
        var matrix = new PacketWriter();
        matrix.WriteUInt32(0);
        matrix.WriteUInt32((uint)page);

        for (var mode = 0; mode < ModeRows; mode++)
        {
            for (var column = 0; column < StatColumns; column++)
            {
                matrix.WriteUInt32(ReadStatistic(statistics, mode, column));
            }
        }

        if (page != 0)
        {
            return matrix.Build();
        }

        // The summary row feeds the player-details card: play time and level.
        var payload = matrix.Build();
        var summaryBase = 8 + (ModeRows - 1) * StatColumns * 4;
        var level = CharacterService.CalculateLevel(character.Experience);
        var playSeconds = character.CreationTime > 0 && statistics is not null ? statistics.TotalTime : 0;
        BinaryUtility.WriteUInt32BigEndian(payload, summaryBase + SummaryPlaySecondsColumn * 4, (uint)playSeconds);
        BinaryUtility.WriteUInt32BigEndian(payload, summaryBase + SummaryLevelColumn * 4, (uint)level);
        return payload;
    }

    private static uint ReadStatistic(CharacterStatistics? statistics, int mode, int column)
    {
        if (statistics is null)
        {
            return 0;
        }

        var modeStatistics = ModeStatisticsCodec.ForMode(statistics, mode);
        var values = new int[]
        {
            modeStatistics.Kills,
            modeStatistics.Deaths,
            modeStatistics.LockKills,
            modeStatistics.Score,
            modeStatistics.Stuns,
            modeStatistics.StunsRec,
            modeStatistics.HsKills,
            modeStatistics.HsDeaths,
            modeStatistics.HsStuns,
            modeStatistics.HsStunsRec,
            modeStatistics.LockStuns,
            modeStatistics.LockDeaths,
            modeStatistics.LockStunsRec,
            modeStatistics.Score,
            modeStatistics.Rounds,
            0,
            modeStatistics.Wins,
            modeStatistics.Time,
        };

        var value = column < values.Length ? values[column] : 0;
        // Column three carries the score, the only signed column.
        return column == ScoreColumn ? unchecked((uint)value) : (uint)value;
    }

    private static byte[] BuildTail()
    {
        var tail = new PacketWriter();
        tail.WriteUInt32(0);
        for (var record = 0; record < 2; record++)
        {
            for (var slot = 1; slot <= TailRecordSlots; slot++)
            {
                tail.WriteUInt32(0);
            }
        }

        Debug.Assert(
            tail.Size <= TailSize,
            "The personal-stats tail grew past its fixed size; adjust the hand-computed layout.");
        tail.WritePadding(TailSize - tail.Size);
        return tail.Build();
    }
}
