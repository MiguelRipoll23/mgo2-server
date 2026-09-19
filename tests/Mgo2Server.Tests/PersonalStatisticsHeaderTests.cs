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
public sealed class PersonalStatisticsHeaderTests
{
    /// <summary>Offset of the experience.</summary>
    private const int ExperienceOffset = 4 + 4 + 16 + 8;

    /// <summary>Offset of the login this one replaced.</summary>
    private const int PreviousLoginOffset = ExperienceOffset + 4;

    /// <summary>Offset of the login recorded for the character.</summary>
    private const int LastLoginOffset = PreviousLoginOffset + 4;

    /// <summary>Exact size of the header packet.</summary>
    private const int InfoSize = 0x288;

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
}
