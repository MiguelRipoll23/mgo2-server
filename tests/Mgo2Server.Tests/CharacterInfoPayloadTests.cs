using Mgo2Server.GameLobbyServer.Commands.Game.Characters;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the character header of the connect burst (0x4101). Its experience field is the
/// only thing the client derives the displayed level from, so a value taken from
/// anywhere but the character is a wrong level on every screen that shows one, and its
/// login pair is the two timestamps the card prints.
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class CharacterInfoPayloadTests
{
    /// <summary>Offset of the experience: the identifier, the name and the four dead words.</summary>
    private const int ExperienceOffset = 4 + 16 + 8;

    /// <summary>Offset of the login this one replaced.</summary>
    private const int PreviousLoginOffset = ExperienceOffset + 4;

    /// <summary>Offset of the login being reported.</summary>
    private const int LastLoginOffset = PreviousLoginOffset + 4;

    /// <summary>Offset of the map and rule availability mask.</summary>
    private const int ContentMaskOffset = 0x22a;

    /// <summary>Offset of the trailing feature byte, which also ends the payload.</summary>
    private const int FeatureByteOffset = 0x242;

    [Fact]
    public void The_header_carries_the_characters_own_experience()
    {
        var payload = CharacterInfoPayloadBuilder.Build(
            41,
            "Someone",
            49250,
            1_699_000_001,
            1_700_000_002,
            [7, 8],
            [9]);

        Assert.Equal(49250u, BinaryUtility.ReadUInt32BigEndian(payload, ExperienceOffset));
        Assert.Equal(41u, BinaryUtility.ReadUInt32BigEndian(payload, 0));
    }

    /// <summary>
    /// The two stamps are the pair the card prints side by side, so each has to land in
    /// its own field: swapped, the card shows the character's logins in the wrong order.
    /// </summary>
    [Fact]
    public void The_header_carries_both_login_stamps_in_order()
    {
        var payload = CharacterInfoPayloadBuilder.Build(
            41,
            "Someone",
            0,
            1_699_000_001,
            1_700_000_002,
            [],
            []);

        Assert.Equal(1_699_000_001u, BinaryUtility.ReadUInt32BigEndian(payload, PreviousLoginOffset));
        Assert.Equal(1_700_000_002u, BinaryUtility.ReadUInt32BigEndian(payload, LastLoginOffset));
    }

    /// <summary>
    /// The two identifier grids are what fix every offset after them: a short payload
    /// would slide the feature byte into the friend grid.
    /// </summary>
    [Fact]
    public void The_grids_are_the_length_the_client_reads()
    {
        var payload = CharacterInfoPayloadBuilder.Build(
            42,
            "Someone",
            0,
            0,
            0,
            [7, 8],
            [9]);

        // Past the two login stamps and the tail byte that follows them.
        var friendGrid = LastLoginOffset + 4 + 1;
        Assert.Equal(0x243, payload.Length);
        Assert.Equal(7u, BinaryUtility.ReadUInt32BigEndian(payload, friendGrid));
        Assert.Equal(8u, BinaryUtility.ReadUInt32BigEndian(payload, friendGrid + 4));
        Assert.Equal(0u, BinaryUtility.ReadUInt32BigEndian(payload, friendGrid + 8));
        Assert.Equal(9u, BinaryUtility.ReadUInt32BigEndian(payload, friendGrid + (64 * 4)));
        Assert.Equal(0xff, payload[ContentMaskOffset]);
        Assert.Equal(0x00, payload[FeatureByteOffset]);
    }
}
