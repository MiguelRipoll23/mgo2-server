using Mgo2Server.GameLobbyServer.Commands.Game.Characters;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the title collection the personal-stats screen renders. The client mints
/// the badges from the mask, so a bit that should not be set draws a title nobody
/// earned — and the reference server records paying for exactly that.
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class CharacterTitleMaskTests
{
    [Fact]
    public void Nothing_latched_is_an_empty_collection() =>
        Assert.Equal(0, CharacterTitleService.BuildTitleMask([]));

    [Fact]
    public void The_best_rank_occupies_bit_zero() =>
        Assert.Equal(1, CharacterTitleService.BuildTitleMask([1]));

    [Fact]
    public void The_last_title_the_client_carries_sets_its_own_bit() =>
        Assert.Equal(1 << 21, CharacterTitleService.BuildTitleMask([CharacterTitleService.ClientTitleCount]));

    [Fact]
    public void Two_latched_ranks_set_two_bits() =>
        Assert.Equal(1 | (1 << 21), CharacterTitleService.BuildTitleMask([22, 1]));

    /// <summary>
    /// Ranks past the client's badge table are the post-1.30 ranks it has no bit for.
    /// Shifting one would make the client's popcount walk past the table, which is
    /// why a rank that does not fit contributes nothing instead of a wider bit.
    /// </summary>
    [Fact]
    public void A_rank_past_the_table_sets_nothing() =>
        Assert.Equal(
            0,
            CharacterTitleService.BuildTitleMask([CharacterTitleService.ClientTitleCount + 1, 26, 52]));

    [Fact]
    public void Zero_and_negative_ranks_are_ignored() =>
        Assert.Equal(0, CharacterTitleService.BuildTitleMask([0, -1]));
}

/// <summary>
/// Pins the two title fields of the personal-stats header. Both are read
/// positionally, so a field added without accounting for the layout would move
/// them and the screen would draw a different character's badges.
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class PersonalStatisticsTitleTests
{
    /// <summary>Offset the worn-title byte is written at, right after the 128-byte comment.</summary>
    private const int WornTitleOffset = 541;

    /// <summary>Offset of rating-block entry 3, the title collection.</summary>
    private const int TitleMaskOffset = 563;

    /// <summary>Fixed size of the header packet.</summary>
    private const int HeaderSize = 0x288;

    [Fact]
    public void The_worn_title_and_the_collection_land_at_their_offsets()
    {
        var character = new Character
        {
            Identifier = 7,
            Name = "Someone",
            Rank = 6,
        };

        var payload = PersonalStatisticsPayloadBuilder.BuildHeader(
            character,
            targetIdentifier: 7,
            friends: [],
            blocked: [],
            clan: null,
            instructor: null,
            instructorScore: default,
            hostRating: default,
            titleMask: 1 << 5);

        Assert.Equal(HeaderSize, payload.Length);
        Assert.Equal(6, payload[WornTitleOffset]);
        Assert.Equal(1u << 5, BinaryUtility.ReadUInt32BigEndian(payload, TitleMaskOffset));
    }

    [Fact]
    public void A_character_with_no_titles_sends_an_empty_collection()
    {
        var character = new Character { Identifier = 1, Name = "Nobody" };

        var payload = PersonalStatisticsPayloadBuilder.BuildHeader(
            character,
            targetIdentifier: 1,
            friends: [],
            blocked: [],
            clan: null,
            instructor: null,
            instructorScore: default,
            hostRating: default,
            titleMask: 0);

        Assert.Equal(0, payload[WornTitleOffset]);
        Assert.Equal(0u, BinaryUtility.ReadUInt32BigEndian(payload, TitleMaskOffset));
    }
}
