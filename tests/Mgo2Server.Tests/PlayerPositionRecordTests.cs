using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the gameplay channel's position record against the bodies a real
/// dedicated server wrote during a live game (<c>docs/mgo2-game.pcapng</c>).
/// </summary>
/// <remarks>
/// The vectors are the four forms the capture decodes plus the one it does not,
/// verbatim. A position record carries no character id, no name and no address,
/// so nothing here identifies anybody — unlike the roster vectors, where the
/// identifiers had to be stood in for.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class PlayerPositionRecordTests
{
    /// <summary>A walking character, in the form every alive record uses.</summary>
    private const string WalkingBody = "2000000000005af141000cfeff3f0000";

    /// <summary>The same form one byte longer, which the capture also sends.</summary>
    private const string WalkingBodySeventeen = "612600000000aa0d7c01270146ed0000ff";

    /// <summary>A dead character: the same five words, the 32-byte shape, the death bit set.</summary>
    private const string DeadBody = "221a000b01000000ef6e4845ab5a7845c61208474ade9b0d7c0140014ade0000";

    /// <summary>The longer alive form, which lands on the same trajectory.</summary>
    private const string AimingBody = "61020426de1c46a15ab545d211dfc663ba00000000200008000000004f034402c4f472ba0000";

    /// <summary>The 44-byte form, which nothing in it decoded.</summary>
    private const string UndecodedBody = "4101000602000000bd0e24457c7c47451ed905470068bfc600002f4500b888c6fe7f2bf918016ef6ef7c0000";

    private static byte[] Body(string hexed) => Convert.FromHexString(hexed);

    [Fact]
    public void The_three_coordinates_come_out_of_the_record_in_world_units()
    {
        // The wire carries a tenth of these numbers: -500, 65 and -3750 at the
        // offsets the walking form puts them, and the ten-unit scale is what
        // turns them into the position the capture agrees with.
        Assert.Equal(new PlayerPosition(-5000, 650, -37500), PlayerPositionRecordUtility.Parse(Body(WalkingBody)));
        Assert.Equal(new PlayerPosition(2950, 3800, 34980), PlayerPositionRecordUtility.Parse(Body(WalkingBodySeventeen)));
        Assert.Equal(new PlayerPosition(3200, 3800, 34830), PlayerPositionRecordUtility.Parse(Body(DeadBody)));
        Assert.Equal(new PlayerPosition(-28760, 5800, 8470), PlayerPositionRecordUtility.Parse(Body(AimingBody)));
    }

    [Fact]
    public void Every_decoded_form_puts_the_same_five_words_at_its_own_offsets()
    {
        // Each length carries the coordinates somewhere else, which is why the
        // codec keys on the length rather than reading one fixed place.
        foreach (var hexed in new[] { WalkingBody, WalkingBodySeventeen, DeadBody, AimingBody })
        {
            var position = PlayerPositionRecordUtility.Parse(Body(hexed));

            Assert.NotNull(position);

            // The trailing five words are the two facings and the three
            // coordinates, so nothing in them runs away with the value.
            Assert.InRange(position.X, -400_000, 400_000);
            Assert.InRange(position.Y, -100_000, 100_000);
            Assert.InRange(position.Z, -400_000, 400_000);
        }
    }

    [Fact]
    public void The_death_bit_of_the_first_byte_is_what_marks_a_character_dead()
    {
        // Bit 1 of byte 0: fifteen thousand records carry it clear, three hundred
        // and ninety carry it set, and every one of the three hundred and ninety
        // belongs to a character whose health was reading zero.
        Assert.True(PlayerPositionRecordUtility.IsDead(Body(DeadBody)));
        Assert.False(PlayerPositionRecordUtility.IsDead(Body(WalkingBody)));
        Assert.False(PlayerPositionRecordUtility.IsDead(Body(WalkingBodySeventeen)));
        Assert.False(PlayerPositionRecordUtility.IsDead(Body(AimingBody)));

        // The first byte values the capture carries, and what each says.
        foreach (var alive in new byte[] { 0x01, 0x20, 0x21, 0x41, 0x61, 0x81, 0xa1, 0xc1, 0xe1 })
        {
            var body = Body(WalkingBody);
            body[0] = alive;
            Assert.False(PlayerPositionRecordUtility.IsDead(body));
        }

        var dead = Body(WalkingBody);
        dead[0] = 0x22;
        Assert.True(PlayerPositionRecordUtility.IsDead(dead));
    }

    [Fact]
    public void A_dead_character_is_reported_in_the_thirty_two_byte_shape()
    {
        // The shape changes with the flag: dead records are the 32-byte form and
        // alive ones are 16, 38 or 44. The captured dead body is the 32-byte one.
        Assert.Equal(PlayerPositionRecordUtility.DeadBodyLength, Body(DeadBody).Length);
        Assert.True(PlayerPositionRecordUtility.IsDead(Body(DeadBody)));
        Assert.NotEqual(PlayerPositionRecordUtility.DeadBodyLength, Body(WalkingBody).Length);
    }

    [Fact]
    public void The_scale_is_ten_and_the_single_precision_copy_agrees_with_two_of_the_three()
    {
        // The 32-byte form carries its position a second time, in single
        // precision, eight bytes in. That copy is the check on the scale: at ten
        // units per step the compressed x and z land within the quantisation of
        // it, which is what fixes the factor at ten rather than one.
        var body = Body(DeadBody);

        Assert.Equal(3200, PlayerPositionRecordUtility.Parse(body)!.X);
        Assert.Equal(34830, PlayerPositionRecordUtility.Parse(body)!.Z);
        Assert.True(Math.Abs(Single(Body(DeadBody), 8) - 3200) < 10);
        Assert.True(Math.Abs(Single(Body(DeadBody), 16) - 34830) < 10);

        // The middle axis does not agree: it reads 3800 where the other reads
        // 3973.7, and it holds still while the other moves. Which of the two is
        // the height is unresolved, so this pins the disagreement rather than
        // pretending it away.
        Assert.Equal(3800, PlayerPositionRecordUtility.Parse(body)!.Y);
        Assert.True(Math.Abs(Single(body, 12) - 3800) > 100);
    }

    [Fact]
    public void A_form_nothing_decoded_yields_no_position_rather_than_a_wrong_one()
    {
        // The 44-byte form is [U]: no offset in it lands on any player's path, so
        // the codec declines it instead of reading coordinates out of other words.
        Assert.Null(PlayerPositionRecordUtility.Parse(Body(UndecodedBody)));
        Assert.Null(PlayerPositionRecordUtility.ParseSample(Body(UndecodedBody)));

        // And so does anything that is not a position record at all.
        Assert.Null(PlayerPositionRecordUtility.Parse([]));
        Assert.Null(PlayerPositionRecordUtility.Parse(new byte[8]));
    }

    [Fact]
    public void A_position_sample_carries_the_position_and_the_death_flag_together()
    {
        var dead = PlayerPositionRecordUtility.ParseSample(Body(DeadBody));
        var alive = PlayerPositionRecordUtility.ParseSample(Body(WalkingBody));

        Assert.NotNull(dead);
        Assert.NotNull(alive);
        Assert.True(dead.IsDead);
        Assert.False(alive.IsDead);
        Assert.Equal(new PlayerPosition(3200, 3800, 34830), dead.Position);
    }

    [Fact]
    public void Only_the_second_attribute_class_is_a_position_record()
    {
        Assert.True(PlayerPositionRecordUtility.IsPositionRecord(0x02));
        Assert.False(PlayerPositionRecordUtility.IsPositionRecord(0x03));
        Assert.False(PlayerPositionRecordUtility.IsPositionRecord(0x01));
    }

    [Fact]
    public void The_two_spawn_sides_are_sixty_thousand_units_apart_on_the_first_axis()
    {
        // The capture's own spawn points, one per character: two of them sit near
        // x = -30000 and two near x = +33500. That is the two teams starting on
        // opposite sides, and it is the reason this record is worth naming.
        var west = new PlayerPosition(-30000, 1790, -5000);
        var east = new PlayerPosition(33500, 3290, -500);

        Assert.True(west.SeparationOnX(east) > 60_000);
        Assert.True(west.SeparationOnX(new PlayerPosition(-30840, 1790, -10260)) < 2_000);
        Assert.True(east.SeparationOnX(new PlayerPosition(33470, 3300, -470)) < 2_000);
    }

    private static float Single(byte[] body, int offset) =>
        BitConverter.Int32BitsToSingle(
            (body[offset] | (body[offset + 1] << 8) | (body[offset + 2] << 16) | (body[offset + 3] << 24)));
}