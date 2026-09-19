using Mgo2Server.GameLobbyServer.Commands.Game.Characters;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the personal-information payload (0x4122), the record the connect burst sends
/// and the client reads a character's comment out of.
/// <para>
/// The layout is hand-computed, so the comment's offset is asserted rather than
/// assumed. It is also the field furthest down the payload, which makes it the one a
/// miscounted block above it moves: every earlier field keeps its own offset while the
/// comment slides, and the client renders whatever now sits under the offset as text.
/// </para>
/// </summary>
public sealed class PersonalInfoPayloadTests
{
    /// <summary>Exact size of the payload.</summary>
    private const int PayloadSize = 0xf5;

    /// <summary>
    /// Offset of the comment: clan id, clan name, the fixed block, the timestamp, the
    /// appearance, the equipped skills, their levels, the four per-skill totals and the
    /// five bytes before the character id.
    /// </summary>
    private const int CommentOffset = 4 + 16 + 25 + 4 + 27 + 5 + 5 + 16 + 5 + 4;

    /// <summary>The comment is served from the record, at the offset the client reads it from.</summary>
    [Fact]
    public void The_payload_is_the_fixed_size_and_carries_the_comment_where_the_client_reads_it()
    {
        var character = new Character { Identifier = 8, Name = "Someone", Comment = "Hello there" };

        var payload = CharacterPayloadBuilder.BuildPersonalInfoPayload(
            character,
            new CharacterAppearance { CharacterIdentifier = 8 },
            null,
            null,
            8);

        Assert.Equal(PayloadSize, payload.Length);
        Assert.Equal("Hello there", StringUtility.ReadFixedString(payload, CommentOffset, 128));
    }

    /// <summary>
    /// The offset holds whatever the record holds, so a character with no comment is not
    /// distinguished from one whose comment is empty — both serve an empty field rather
    /// than shifting the fields below it up.
    /// </summary>
    [Fact]
    public void A_character_without_a_comment_serves_an_empty_field_of_the_same_size()
    {
        var character = new Character { Identifier = 8, Name = "Someone" };

        var payload = CharacterPayloadBuilder.BuildPersonalInfoPayload(
            character,
            new CharacterAppearance { CharacterIdentifier = 8 },
            null,
            null,
            8);

        Assert.Equal(PayloadSize, payload.Length);
        Assert.Equal(string.Empty, StringUtility.ReadFixedString(payload, CommentOffset, 128));
    }

    /// <summary>
    /// A character with no appearance row still frames the block, so the comment stays at
    /// its offset. A short frame here moves every field after it, which the client reads
    /// as a comment beginning several bytes into the one that was meant.
    /// </summary>
    [Fact]
    public void A_character_with_no_appearance_row_keeps_the_comment_at_its_offset()
    {
        var character = new Character { Identifier = 8, Name = "Someone", Comment = "Hello there" };

        var payload = CharacterPayloadBuilder.BuildPersonalInfoPayload(character, null, null, null, 8);

        Assert.Equal(PayloadSize, payload.Length);
        Assert.Equal("Hello there", StringUtility.ReadFixedString(payload, CommentOffset, 128));
    }
}
