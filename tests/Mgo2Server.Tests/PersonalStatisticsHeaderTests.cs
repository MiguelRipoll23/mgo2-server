using Mgo2Server.GameLobbyServer.Commands.Game.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Domain.Instructors;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the head of the personal-stats record (0x4103), which repeats the character
/// header the connect burst sends. The two screens are read by the same card, so a field
/// that is right in one and wrong in the other is a difference the player can see.
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class PersonalStatisticsHeaderTests
{
    /// <summary>Offset of the experience.</summary>
    private const int ExperienceOffset = 4 + 4 + 16 + 8;

    /// <summary>Offset of the login this one replaced.</summary>
    private const int PreviousLoginOffset = ExperienceOffset + 4;

    /// <summary>Offset of the login recorded for the character.</summary>
    private const int LastLoginOffset = PreviousLoginOffset + 4;

    /// <summary>Exact size of the header packet the 1.36 client reads.</summary>
    private const int InfoSize = 909;

    /// <summary>Offset the 128-byte comment starts at on the 1.36 client.</summary>
    private const int CommentOffset = 669;

    /// <summary>Offset the friend grid starts at.</summary>
    private const int FriendGridOffset = 4 + 4 + 16 + 8 + 4 + 4 + 4 + 1;

    [Fact]
    public void The_header_carries_the_characters_experience_and_login_pair()
    {
        var character = new Character
        {
            Identifier = 8,
            Name = "Someone",
            Experience = 1450,
            PreviousLoginTime = DateTimeOffset.FromUnixTimeSeconds(1_699_000_001),
            LastSeenAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_002),
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1),
        };

        var payload = PersonalStatisticsPayloadBuilder.BuildHeader(
            character,
            8,
            [],
            [],
            null,
            null,
            default,
            default,
            titleMask: 0);

        Assert.Equal(InfoSize, payload.Length);
        Assert.Equal(1450u, BinaryUtility.ReadUInt32BigEndian(payload, ExperienceOffset));
        Assert.Equal(1_699_000_001u, BinaryUtility.ReadUInt32BigEndian(payload, PreviousLoginOffset));
        Assert.Equal(1_700_000_002u, BinaryUtility.ReadUInt32BigEndian(payload, LastLoginOffset));
    }

    /// <summary>
    /// A character that has never logged in has no stamps, and zero is what the client
    /// renders for none — the alternative is an invented epoch, which it prints as a date.
    /// </summary>
    [Fact]
    public void A_character_that_never_logged_in_sends_zero_for_both()
    {
        var character = new Character
        {
            Identifier = 8,
            Name = "Someone",
            Experience = 0,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1),
        };

        var payload = PersonalStatisticsPayloadBuilder.BuildHeader(
            character,
            8,
            [],
            [],
            null,
            null,
            default,
            default,
            titleMask: 0);

        Assert.Equal(0u, BinaryUtility.ReadUInt32BigEndian(payload, PreviousLoginOffset));
        Assert.Equal(0u, BinaryUtility.ReadUInt32BigEndian(payload, LastLoginOffset));
    }

    /// <summary>
    /// The comment has to land where the 1.36 parser reads it. Its two relation grids
    /// are 64 identifiers wide where the disc build's are 32, so a payload built to the
    /// disc offsets puts the comment 256 bytes early and the screen renders it empty —
    /// which is what this pins.
    /// </summary>
    [Fact]
    public void The_comment_lands_where_the_1_36_client_reads_it()
    {
        var character = new Character
        {
            Identifier = 2,
            Name = "Someone",
            Comment = "hi",
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1),
        };

        var payload = PersonalStatisticsPayloadBuilder.BuildHeader(
            character,
            2,
            [],
            [],
            null,
            null,
            default,
            default,
            titleMask: 0);

        Assert.Equal("hi", StringUtility.ReadFixedString(payload, CommentOffset, 128));
    }

    /// <summary>
    /// A friend beyond the disc build's thirty-second slot still travels: the grid the
    /// 1.36 client walks holds sixty-four.
    /// </summary>
    [Fact]
    public void The_friend_grid_carries_more_than_thirty_two_identifiers()
    {
        var character = new Character
        {
            Identifier = 2,
            Name = "Someone",
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1),
        };
        var friends = Enumerable.Range(5001, 40).ToList();

        var payload = PersonalStatisticsPayloadBuilder.BuildHeader(
            character,
            2,
            friends,
            [],
            null,
            null,
            default,
            default,
            titleMask: 0);

        Assert.Equal(5001u, BinaryUtility.ReadUInt32BigEndian(payload, FriendGridOffset));
        Assert.Equal(5040u, BinaryUtility.ReadUInt32BigEndian(payload, FriendGridOffset + (39 * 4)));
    }
}
