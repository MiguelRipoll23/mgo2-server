using System.Diagnostics;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Domain.Instructors;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Renders the three packets of the personal-stats burst: the record header, the
/// mode matrices and the tail.
/// <para>
/// Every field is at a hand-computed offset the client's parsers read positionally,
/// so each packet is padded to its fixed size and the assertions here exist to fail
/// loudly if a field is ever added without accounting for the layout.
/// </para>
/// </summary>
public static class PersonalStatisticsPayloadBuilder
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

    /// <summary>
    /// Summary column carrying the total rewards. It is the cell the client's
    /// character block holds at `T+0x484` — the player-details card's "TOTAL
    /// REWARDS" figure — because the eighth wire block is the card's summary row,
    /// not a game mode. It is deliberately not the level: the card derives that
    /// from the experience the header carries.
    /// </summary>
    private const int SummaryTotalRewardsColumn = 13;

    /// <summary>Rating-block entry carrying the title collection, a 22-bit mask.</summary>
    private const int TitleMaskEntry = 3;

    /// <summary>Rating-block entry carrying the host rating numerator, the sum of the votes.</summary>
    private const int HostRatingNumeratorEntry = 5;

    /// <summary>Rating-block entry carrying the host rating denominator, the vote count.</summary>
    private const int HostRatingDenominatorEntry = 6;

    /// <summary>Number of slots in one tail record.</summary>
    private const int TailRecordSlots = 73;

    /// <summary>Tail slot carrying the distinct students the character has graduated.</summary>
    private const int StudentsTrainedSlot = 36;

    /// <summary>Tail slot carrying the seconds spent in a training lobby.</summary>
    private const int TrainingSecondsSlot = 46;

    /// <summary>Tail slot carrying the seconds spent instructing.</summary>
    private const int InstructorSecondsSlot = 47;

    /// <summary>Tail slot carrying the seconds spent as a student.</summary>
    private const int StudentSecondsSlot = 48;

    /// <summary>Number of identifiers in each relation array.</summary>
    private const int RelationListIdentifiers = 32;

    /// <summary>Offset the comment starts at.</summary>
    private const int CommentOffset = 413;

    /// <summary>The four dead 16-bit constants after the name.</summary>
    private static readonly byte[] CharacterInfoPrefix =
    [
        0x16, 0xae, 0x03, 0x38, 0x01, 0x3e, 0x01, 0x50,
    ];

    /// <summary>Builds the record header.</summary>
    /// <param name="character">Character the header describes.</param>
    /// <param name="targetIdentifier">Identifier of that character.</param>
    /// <param name="friends">Friend identifiers, zero-padded to the fixed array.</param>
    /// <param name="blocked">Blocked identifiers, zero-padded to the fixed array.</param>
    /// <param name="clan">Clan of the character, when it belongs to one.</param>
    /// <param name="instructor">The instructor the character saved, when they have one.</param>
    /// <param name="instructorScore">Reviews the character received as an instructor.</param>
    /// <param name="hostRating">Reviews the character received as a host.</param>
    /// <param name="titleMask">Titles the character has collected, which the screen renders as badges.</param>
    public static byte[] BuildHeader(
        Character character,
        int targetIdentifier,
        IReadOnlyList<int> friends,
        IReadOnlyList<int> blocked,
        CharacterClanInformation? clan,
        InstructorRecord? instructor,
        InstructorScore instructorScore,
        HostRatingSummary hostRating,
        int titleMask)
    {
        var header = new PacketWriter();
        header.WriteUInt32(0);
        header.WriteUInt32((uint)targetIdentifier);
        header.WriteFixedString(character.Name, 16);
        header.WriteBytes(CharacterInfoPrefix);
        header.WriteUInt32((uint)character.Experience);
        // The login this one replaced, then the login recorded for the character. Both are
        // the stamps the connect burst rotates, so this screen and the card agree; zero is
        // what a character who has never logged in sends, rather than an invented epoch.
        header.WriteUInt32((uint)(character.PreviousLoginTime?.ToUnixTimeSeconds() ?? 0));
        header.WriteUInt32((uint)(character.LastSeenAt?.ToUnixTimeSeconds() ?? 0));
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
        // The slot immediately above the comment, which the personal-data screen renders
        // as the character's own figure. The 0x4122 write-back fills the same
        // destination, so both carry characters.total_rewards rather than one of them
        // carrying the character id and the other a bare zero.
        header.WriteUInt32((uint)character.TotalRewards);
        Debug.Assert(
            header.Size <= CommentOffset,
            "The personal-stats header grew past the comment offset; adjust the hand-computed layout.");
        header.WritePadding(CommentOffset - header.Size);
        header.WriteFixedString(character.Comment, 128);
        // The WORN title, 1-based and zero for none: the badge the client draws
        // beside the name. The same value travels in the 0x4122 payload, and the
        // two screens read different blocks, so both are written from the rank
        // the title service latched.
        header.WriteUInt8((byte)Math.Clamp(character.Rank, 0, byte.MaxValue));

        for (var index = 0; index < 9; index++)
        {
            header.WriteUInt8(0);
        }

        // The rating block, nine words. Four are identified: entry 3 is the title
        // collection — the client draws a badge per set bit and nothing else the
        // server sends carries it — entry 5 is the host rating's numerator and
        // entry 6 its denominator, so sending the sum over the count makes the
        // ratio the average and the star gauge lands on the real value. Everything
        // else stays zero rather than carrying a placeholder, because the client
        // mints medals and titles itself from these words and an invented number
        // awards something nobody earned.
        for (var entry = 0; entry < 9; entry++)
        {
            header.WriteUInt32(entry switch
            {
                TitleMaskEntry => (uint)titleMask,
                HostRatingNumeratorEntry => (uint)hostRating.RatingSum,
                HostRatingDenominatorEntry => (uint)hostRating.Votes,
                _ => 0,
            });
        }

        header.WriteUInt32(0);

        // The instructor block: who trained this character, and the gauge of the
        // reviews they were given. An empty name with zeros is the honest answer for
        // someone who never graduated — a placeholder here would tell the client a
        // character has an instructor they do not have, which is a lie its own
        // recognition prompt then acts on.
        header.WriteFixedString(instructor?.InstructorName ?? string.Empty, 16);
        header.WriteUInt32((uint)(instructor?.Generation ?? 0));
        header.WriteUInt32(0);
        header.WritePadding(16);
        header.WriteUInt8(0);
        header.WriteUInt32((uint)instructorScore.RatingSum);
        header.WriteUInt32((uint)instructorScore.Votes);
        header.WriteUInt32(0);
        header.WriteUInt32(0);
        Debug.Assert(
            header.Size <= InfoSize,
            "The personal-stats header grew past its fixed size; adjust the hand-computed layout.");
        header.WritePadding(InfoSize - header.Size);
        return header.Build();
    }

    /// <summary>
    /// Builds one mode matrix.
    /// <para>
    /// The last row block is not a mode: it is the summary row the player-details
    /// card reads, and only the cumulative page fills it, because the card reads
    /// matrix zero and nothing is known to read the other.
    /// </para>
    /// </summary>
    /// <param name="statistics">Statistics of the character, when it has any.</param>
    /// <param name="page">Page selector: zero cumulative, one monthly.</param>
    /// <param name="character">Character the matrix describes.</param>
    public static byte[] BuildMatrix(CharacterStatistics? statistics, int page, Character character)
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

        // The summary row feeds the player-details card: play time and the
        // character's total rewards.
        var payload = matrix.Build();
        var summaryBase = 8 + (ModeRows - 1) * StatColumns * 4;
        var playSeconds = character.CreatedAt.ToUnixTimeSeconds() > 0 && statistics is not null ? statistics.TotalTime : 0;
        BinaryUtility.WriteUInt32BigEndian(payload, summaryBase + SummaryPlaySecondsColumn * 4, (uint)playSeconds);
        BinaryUtility.WriteUInt32BigEndian(
            payload,
            summaryBase + SummaryTotalRewardsColumn * 4,
            (uint)character.TotalRewards);
        return payload;
    }

    /// <summary>
    /// Builds the tail: the cumulative record and the monthly one.
    /// <para>
    /// The client derives medals from these slots, so every one that cannot be
    /// measured honestly stays zero rather than carrying a placeholder — a fabricated
    /// figure clears thresholds in its table and hands out an unearned medal.
    /// </para>
    /// </summary>
    /// <param name="studentsTrained">Distinct students the character has ever graduated.</param>
    /// <param name="studentsTrainedThisMonth">Distinct students graduated this calendar month.</param>
    /// <param name="trainingSeconds">Presence-accumulated training totals.</param>
    public static byte[] BuildTail(
        int studentsTrained,
        int studentsTrainedThisMonth,
        TrainingSeconds trainingSeconds)
    {
        var tail = new PacketWriter();
        tail.WriteUInt32(0);

        WriteTailRecord(tail, slot => slot switch
        {
            StudentsTrainedSlot => studentsTrained,
            TrainingSecondsSlot => trainingSeconds.TrainingMode,
            InstructorSecondsSlot => trainingSeconds.Instructor,
            StudentSecondsSlot => trainingSeconds.Student,
            _ => 0,
        });

        WriteTailRecord(tail, slot => slot == StudentsTrainedSlot ? studentsTrainedThisMonth : 0);

        Debug.Assert(
            tail.Size <= TailSize,
            "The personal-stats tail grew past its fixed size; adjust the hand-computed layout.");
        tail.WritePadding(TailSize - tail.Size);
        return tail.Build();
    }

    private static uint ReadStatistic(CharacterStatistics? statistics, int mode, int column)
    {
        if (statistics is null)
        {
            return 0;
        }

        var modeStatistics = statistics.ForMode(mode);
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

    /// <summary>Writes one seventy-three-slot record; the slot array is one-based, so index zero is skipped.</summary>
    /// <param name="tail">Tail being built.</param>
    /// <param name="value">Value of a slot, zero for every slot not named.</param>
    private static void WriteTailRecord(PacketWriter tail, Func<int, long> value)
    {
        for (var slot = 1; slot <= TailRecordSlots; slot++)
        {
            tail.WriteUInt32((uint)Math.Clamp(value(slot), 0, uint.MaxValue));
        }
    }
}
