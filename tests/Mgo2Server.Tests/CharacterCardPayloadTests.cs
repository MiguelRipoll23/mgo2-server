using System.Text;
using Mgo2Server.GameLobbyServer.Commands.Game.Characters;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the player-details card, the reply to <c>0x4220</c>. The 1.36 client's parser
/// reads a fixed field map, so every field has to sit at its own offset: a field one
/// byte out moves everything after it, and the clan in particular is only rendered when
/// its identifier arrives at the slot the client reads it from.
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class CharacterCardPayloadTests
{
    /// <summary>Result code: zero is success, and a nonzero one skips every field.</summary>
    private const int ResultOffset = 0x00;

    /// <summary>Echo of the requested character identifier.</summary>
    private const int IdentifierOffset = 0x04;

    /// <summary>Character name, sixteen bytes.</summary>
    private const int NameOffset = 0x08;

    /// <summary>Experience, a u32 — the client derives the card's Level from it.</summary>
    private const int ExperienceOffset = 0x18;

    /// <summary>The character's own reward total, the bit-2-gated "TOTAL REWARDS" figure.</summary>
    private const int TotalRewardsOffset = 0x1e;

    /// <summary>Worn title, 1-based, zero for none.</summary>
    private const int WornTitleOffset = 0x26;

    /// <summary>Comment, 128 bytes.</summary>
    private const int CommentOffset = 0x27;

    /// <summary>Clan identifier, the first member of the clan triple.</summary>
    private const int ClanIdentifierOffset = 0xa7;

    /// <summary>Clan name, the second member of the clan triple.</summary>
    private const int ClanNameOffset = 0xab;

    /// <summary>Clan membership state, the third member of the clan triple.</summary>
    private const int ClanStateOffset = 0xbb;

    /// <summary>Emblem flag: three calls for the emblem image.</summary>
    private const int EmblemOffset = 0xc4;

    /// <summary>Grade points, which mirror the experience on this server.</summary>
    private const int GradePointsOffset = 0xc5;

    /// <summary>The six bytes 1.36 reads past the disc build's last field.</summary>
    private const int OneThirtySixTailOffset = 201;

    [Fact]
    public void A_character_that_no_longer_exists_gets_the_clients_own_deleted_code()
    {
        // -266 is the client's literal for "Designated character has been deleted and no
        // longer exists", and the one nonzero result it renders as a sentence of its own.
        // It has to go out unmasked: a code of this server's own making matches nothing in
        // the client's table and collapses into the generic sentence instead.
        var payload = CharacterCardPayloadBuilder.BuildResult(ErrorCodeConstants.ResultCharacterGone);

        Assert.Equal(0xfffffef6u, ErrorCodeConstants.ResultCharacterGone);
        Assert.Equal(4, payload.Length);
        Assert.Equal(0xfffffef6u, BinaryUtility.ReadUInt32BigEndian(payload, ResultOffset));
    }

    [Fact]
    public void A_refusal_is_the_result_word_alone_and_is_not_the_deleted_code()
    {
        // A nonzero result makes the client skip every field below it, so the refusal is
        // four bytes — and a request that names nobody must not borrow the deleted code,
        // which would tell the player their character had been deleted when it was only the
        // packet that was short.
        var payload = CharacterCardPayloadBuilder.BuildResult(ErrorCodeConstants.ResultGeneral);

        Assert.Equal(4, payload.Length);
        Assert.Equal(ErrorCodeConstants.ResultGeneral, BinaryUtility.ReadUInt32BigEndian(payload, ResultOffset));
        Assert.NotEqual(ErrorCodeConstants.ResultCharacterGone, ErrorCodeConstants.ResultGeneral);
    }

    [Fact]
    public void The_card_is_the_length_the_1_36_parser_reads()
    {
        var payload = CharacterCardPayloadBuilder.Build(Character(), 4, null);

        Assert.Equal(CharacterCardPayloadBuilder.Size, payload.Length);
        Assert.Equal(207, payload.Length);
        Assert.Equal(0u, BinaryUtility.ReadUInt32BigEndian(payload, ResultOffset));
        Assert.Equal(4u, BinaryUtility.ReadUInt32BigEndian(payload, IdentifierOffset));
        Assert.Equal("Someone", ReadFixedString(payload, NameOffset, 16));
    }

    [Fact]
    public void A_character_with_a_clan_carries_the_triple_at_the_offsets_a_clan_lives_at()
    {
        // The name alone never renders: every reader of the triple checks the identifier
        // first and reads a zero one as "no clan", whatever the name says. So the card has
        // to carry the clan's own identifier at 0xa7 and the name at 0xab, not a name with
        // something packed in beside it.
        var clan = new CharacterClanInformation(77, "Best Clan", HasEmblem: true);

        var payload = CharacterCardPayloadBuilder.Build(Character(), 4, clan);

        Assert.Equal(77u, BinaryUtility.ReadUInt32BigEndian(payload, ClanIdentifierOffset));
        Assert.Equal("Best Clan", ReadFixedString(payload, ClanNameOffset, 16));
        Assert.Equal((byte)1, payload[ClanStateOffset]);
        Assert.Equal((byte)3, payload[EmblemOffset]);
    }

    [Fact]
    public void A_character_without_a_clan_zeroes_the_whole_triple()
    {
        var payload = CharacterCardPayloadBuilder.Build(Character(), 4, null);

        Assert.Equal(0u, BinaryUtility.ReadUInt32BigEndian(payload, ClanIdentifierOffset));
        Assert.Equal(string.Empty, ReadFixedString(payload, ClanNameOffset, 16));
        Assert.Equal((byte)0, payload[ClanStateOffset]);
        Assert.Equal((byte)0, payload[EmblemOffset]);
    }

    [Fact]
    public void The_card_carries_the_worn_title_the_badge_is_drawn_from()
    {
        // The badge beside the name is drawn from this single byte, and it is the rank
        // the title service latched — the same figure the personal-stats header writes,
        // so the two screens cannot disagree about which title is worn.
        var character = Character();
        character.Rank = 7;

        var payload = CharacterCardPayloadBuilder.Build(character, 4, null);

        Assert.Equal((byte)7, payload[WornTitleOffset]);
    }

    [Fact]
    public void A_character_with_no_title_wears_none()
    {
        var payload = CharacterCardPayloadBuilder.Build(Character(), 4, null);

        Assert.Equal((byte)0, payload[WornTitleOffset]);
    }

    [Fact]
    public void A_full_length_comment_does_not_shift_the_clan()
    {
        // The comment is 128 bytes, and it was 127: the field that followed it was then
        // read one byte early, which is the difference between a resolvable clan and a
        // fabricated identifier assembled out of the comment's last byte.
        var character = Character();
        character.Comment = new string('c', 128);

        var payload = CharacterCardPayloadBuilder.Build(
            character,
            4,
            new CharacterClanInformation(77, "Best Clan", HasEmblem: false));

        Assert.Equal(new string('c', 122), ReadFixedString(payload, CommentOffset, 128)[..122]);
        Assert.Equal(77u, BinaryUtility.ReadUInt32BigEndian(payload, ClanIdentifierOffset));
        Assert.Equal("Best Clan", ReadFixedString(payload, ClanNameOffset, 16));
    }

    [Fact]
    public void The_experience_is_a_single_word_the_level_is_derived_from()
    {
        // It used to be written as two 16-bit halves, which silently dropped everything
        // above 65535 — the client walks its threshold table over this word, so a
        // truncated one shows a lower level rather than a wrong number.
        var character = Character();
        character.Experience = 70_000;

        var payload = CharacterCardPayloadBuilder.Build(character, 4, null);

        Assert.Equal(70_000u, BinaryUtility.ReadUInt32BigEndian(payload, ExperienceOffset));
        Assert.Equal(70_000u, BinaryUtility.ReadUInt32BigEndian(payload, GradePointsOffset));
    }

    [Fact]
    public void The_reward_total_is_the_characters_own_and_sits_above_the_play_time()
    {
        var payload = CharacterCardPayloadBuilder.Build(Character(), 4, null);

        Assert.Equal(1234u, BinaryUtility.ReadUInt32BigEndian(payload, TotalRewardsOffset));
    }

    /// <summary>
    /// The six trailing bytes 1.36 added. Nothing is filled here, and the test exists so
    /// that the size stays 207 rather than being trimmed back to the disc build's 201 —
    /// the client abandons the record at the first read past its end.
    /// </summary>
    [Fact]
    public void The_words_1_36_added_past_the_disc_builds_last_field_are_zero()
    {
        var payload = CharacterCardPayloadBuilder.Build(Character(), 4, null);

        Assert.Equal(6, payload.Length - OneThirtySixTailOffset);
        Assert.Equal(0u, BinaryUtility.ReadUInt32BigEndian(payload, OneThirtySixTailOffset));
        Assert.Equal((byte)0, payload[OneThirtySixTailOffset + 4]);
        Assert.Equal((byte)0, payload[OneThirtySixTailOffset + 5]);
    }

    /// <summary>A character with everything the card reports filled in.</summary>
    private static Character Character() => new()
    {
        Identifier = 4,
        Name = "Someone",
        Experience = 4000,
        TotalRewards = 1234,
        Comment = "hello",
        CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1),
    };

    /// <summary>Reads a fixed-width, NUL-padded string field.</summary>
    /// <param name="payload">Payload the field lives in.</param>
    /// <param name="offset">Offset the field starts at.</param>
    /// <param name="length">Length of the field.</param>
    private static string ReadFixedString(byte[] payload, int offset, int length)
    {
        var field = payload.AsSpan(offset, length);
        var terminator = field.IndexOf((byte)0);
        if (terminator >= 0)
        {
            field = field[..terminator];
        }

        return Encoding.Latin1.GetString(field);
    }
}
