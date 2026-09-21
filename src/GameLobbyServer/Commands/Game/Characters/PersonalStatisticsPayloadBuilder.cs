using System.Diagnostics;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Domain.Instructors;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Renders the record header and the tail of the personal-stats burst; the per-mode
/// grid between them is built by <see cref="PersonalStatisticsMatrixBuilder"/>.
/// <para>
/// Every field is at a hand-computed offset the client's parsers read positionally,
/// so each packet is padded to its fixed size and the assertions here exist to fail
/// loudly if a field is ever added without accounting for the layout.
/// </para>
/// </summary>
public static class PersonalStatisticsPayloadBuilder
{
    /// <summary>
    /// Exact size of the header packet: 648 on the disc build, 909 here.
    /// <para>
    /// The 1.36 client reads 261 bytes more. Two hundred and fifty-six of them are
    /// the two relation grids, which it walks to 64 identifiers each instead of 32;
    /// the remaining five are a trailing word and the feature byte the parser hands
    /// to the same splitter the connect burst's 0x4101 feature byte goes through.
    /// The size is asserted rather than implied because a short payload does not
    /// leave a hole — it moves the comment and everything under it early, and the
    /// parser abandons the packet part-way through.
    /// </para>
    /// </summary>
    private const int InfoSize = 909;

    /// <summary>
    /// Exact size of the tail packet: 660, the status word and two records of the
    /// 82 slots the 1.36 parser's loop reads. The disc build's record holds 73.
    /// </summary>
    private const int TailSize = 0x294;

    /// <summary>Rating-block entry carrying the title collection, a 22-bit mask.</summary>
    private const int TitleMaskEntry = 3;

    /// <summary>Rating-block entry carrying the host rating numerator, the sum of the votes.</summary>
    private const int HostRatingNumeratorEntry = 5;

    /// <summary>Rating-block entry carrying the host rating denominator, the vote count.</summary>
    private const int HostRatingDenominatorEntry = 6;

    /// <summary>
    /// Number of slots in one tail record: 73 on the disc build, 82 here. The nine
    /// the 1.36 client added are written as zeros, and every named slot below sits
    /// before them, so its position is the same under either build.
    /// </summary>
    private const int TailRecordSlots = 82;

    /// <summary>Tail slot carrying the distinct students the character has graduated.</summary>
    private const int StudentsTrainedSlot = 36;

    /// <summary>Tail slot carrying the seconds spent in a training lobby.</summary>
    private const int TrainingSecondsSlot = 46;

    /// <summary>Tail slot carrying the seconds spent instructing.</summary>
    private const int InstructorSecondsSlot = 47;

    /// <summary>Tail slot carrying the seconds spent as a student.</summary>
    private const int StudentSecondsSlot = 48;

    /// <summary>
    /// Number of identifiers in each relation array. The 1.36 parser's two loops
    /// run to 64 (`cmpdi cr6,r29,0x40`) where the disc build's run to 32, so each
    /// grid is 256 bytes and every field after them sits 256 bytes later.
    /// </summary>
    private const int RelationListIdentifiers = 64;

    /// <summary>
    /// Offset the comment starts at: 413 on the disc build, 669 here — the two
    /// relation grids are 256 bytes wider, and nothing between them and the comment
    /// changed size. The 1.36 parser reads the 128 bytes into the block's comment
    /// slot, so a payload that keeps the disc offset leaves the screen blank.
    /// </summary>
    private const int CommentOffset = 669;

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
        // The two fields the 1.36 build added below the instructor block: a word the
        // disc build has no slot for, and the feature byte its parser hands to the
        // same splitter the connect burst's 0x4101 feature byte goes through. The
        // two packets therefore write that byte from one constant rather than each
        // deciding for itself which flags the client may hold.
        header.WriteUInt32(0);
        header.WriteUInt8((byte)FeatureFlags.MainMenuFlags);
        Debug.Assert(
            header.Size <= InfoSize,
            "The personal-stats header grew past its fixed size; adjust the hand-computed layout.");
        header.WritePadding(InfoSize - header.Size);
        return header.Build();
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

    /// <summary>Writes one eighty-two-slot record; the slot array is one-based, so index zero is skipped.</summary>
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
