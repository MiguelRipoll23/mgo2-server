using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the vitals record — the two bytes that carry a character's health and
/// stamina — against the bodies a real dedicated server wrote during a live
/// game (<c>docs/mgo2-game.pcapng</c>).
/// </summary>
/// <remarks>
/// The capture holds 3 269 of them in one round, all server to client, and only
/// seven distinct bodies between them. Six of the seven are the ones below; the
/// health values in between (<c>46 fa</c>, <c>7f fa</c>, <c>c2 fa</c>,
/// <c>a0 fa</c>, <c>0e fa</c>) are the steps health falls through on its way
/// down and are not pinned individually, because what they are is the scale
/// rather than a value: the two ends are what the record means.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class PlayerVitalsRecordTests
{
    /// <summary>
    /// The seven distinct bodies the capture carried, with how often each one
    /// arrived. <c>fa fa</c> is an untouched character and <c>00 fa</c> is a
    /// dead one; stamina reads 250 in every one of them.
    /// </summary>
    private static readonly (string Body, int Count)[] CapturedBodies =
    [
        ("fafa", 2893),
        ("00fa", 316),
        ("46fa", 47),
        ("7ffa", 6),
        ("c2fa", 3),
        ("a0fa", 2),
        ("0efa", 2),
    ];

    [Fact]
    public void The_record_is_two_bytes_of_health_and_stamina()
    {
        Assert.Equal(2, PlayerVitalsRecordUtility.BodyLength);

        var record = PlayerVitalsRecordUtility.Build(PlayerVitals.Full);

        Assert.Equal("fafa", Convert.ToHexString(record.Body).ToLowerInvariant());
        Assert.Equal(PlayerVitalsRecordUtility.FirstPlayerType, record.Type);
        Assert.Equal(PlayerVitalsRecordUtility.AttributeClass, record.Flags);
    }

    [Fact]
    public void An_untouched_character_reads_the_full_value_and_a_dead_one_reads_zero()
    {
        // The two ends of the scale, and the whole of what the record means: the
        // capture holds 2 893 records at the full value between fights, health
        // falling in steps after a hit, and 316 at zero before it comes back.
        Assert.Equal(PlayerVitals.MaximumValue, PlayerVitals.Full.Health);
        Assert.False(PlayerVitals.Full.IsDead);
        Assert.True(new PlayerVitals(0, PlayerVitals.MaximumValue).IsDead);

        Assert.Equal(
            "fafa",
            Convert.ToHexString(PlayerVitalsRecordUtility.Build(PlayerVitals.Full).Body).ToLowerInvariant());
        Assert.Equal(
            "00fa",
            Convert.ToHexString(
                PlayerVitalsRecordUtility.Build(new PlayerVitals(0, PlayerVitals.MaximumValue)).Body)
                .ToLowerInvariant());
    }

    [Fact]
    public void Every_captured_body_parses_back_to_what_it_carried()
    {
        foreach (var (body, _) in CapturedBodies)
        {
            var bytes = Convert.FromHexString(body);
            var vitals = PlayerVitalsRecordUtility.Parse(bytes);

            Assert.NotNull(vitals);
            Assert.Equal(bytes[0], vitals.Health);
            Assert.Equal(bytes[1], vitals.Stamina);

            // Every one of them rebuilds to the same two bytes.
            Assert.Equal(body, Convert.ToHexString(PlayerVitalsRecordUtility.Build(vitals).Body).ToLowerInvariant());
        }
    }

    [Fact]
    public void Stamina_is_the_second_byte_and_nothing_else_moved_it_in_the_captured_round()
    {
        // The capture's 3 269 records agree on the second byte and disagree on
        // the first, which is what puts health first and stamina second rather
        // than the other way round.
        Assert.All(CapturedBodies, captured => Assert.Equal("fa", captured.Body[2..]));

        var record = PlayerVitalsRecordUtility.Parse(Convert.FromHexString("0efa"))!;
        Assert.Equal(14, record.Health);
        Assert.Equal(250, record.Stamina);
    }

    [Fact]
    public void Health_falls_in_steps_from_the_full_value_to_zero()
    {
        // The ladder the capture shows, in the order it shows it: 250, then 194,
        // 160, 127, 70, 14, and 0 — three deaths and three restores over the
        // round. The steps are the game's, not round numbers, so this pins the
        // order and the two ends rather than any claim about the step size.
        int[] ladder = [250, 194, 160, 127, 70, 14, 0];

        Assert.Equal(PlayerVitals.MaximumValue, ladder[0]);
        Assert.Equal(0, ladder[^1]);
        Assert.Equal(ladder, ladder.OrderDescending());

        foreach (var health in ladder)
        {
            var rebuilt = PlayerVitalsRecordUtility.Build(new PlayerVitals(health, 250));
            Assert.Equal(health, PlayerVitalsRecordUtility.Parse(rebuilt.Body)!.Health);
        }
    }

    [Fact]
    public void A_body_of_another_length_is_not_a_vitals_record()
    {
        Assert.Null(PlayerVitalsRecordUtility.Parse(new byte[1]));
        Assert.Null(PlayerVitalsRecordUtility.Parse(new byte[3]));
        Assert.Null(PlayerVitalsRecordUtility.Parse([]));
    }

    [Fact]
    public void Values_outside_the_scale_are_clamped_rather_than_wrapped()
    {
        // The wire carries one byte, so 300 and -5 cannot be written as they
        // stand. Clamping keeps a full character full and a dead one dead rather
        // than turning either into the other.
        var over = PlayerVitalsRecordUtility.Build(new PlayerVitals(300, 300));
        Assert.Equal("fafa", Convert.ToHexString(over.Body).ToLowerInvariant());

        var under = PlayerVitalsRecordUtility.Build(new PlayerVitals(-5, -5));
        Assert.Equal("0000", Convert.ToHexString(under.Body).ToLowerInvariant());
        Assert.True(PlayerVitalsRecordUtility.Parse(under.Body)!.IsDead);
    }

    [Fact]
    public void The_second_player_carries_the_same_record_under_the_slot_bit()
    {
        // 0x0080 and 0x0880 are the same two bytes, one identifier apart by the
        // bit the recorded tick stream uses for the second player.
        var first = PlayerVitalsRecordUtility.Build(PlayerVitals.Full);
        var second = PlayerVitalsRecordUtility.Build(PlayerVitals.Full, secondPlayer: true);

        Assert.Equal(first.Body, second.Body);
        Assert.Equal(0x0080, first.Type);
        Assert.Equal(0x0880, second.Type);
        Assert.True(PlayerVitalsRecordUtility.IsVitalsRecord(first.Type));
        Assert.True(PlayerVitalsRecordUtility.IsVitalsRecord(second.Type));
        Assert.False(PlayerVitalsRecordUtility.IsVitalsRecord(0x0081));
    }

    [Fact]
    public void The_record_is_framed_as_a_tick_record_rather_than_a_session_one()
    {
        // Below the tick threshold the length byte counts the attribute class as
        // well as the body, so the record is one byte shorter than that byte
        // says — and serializing it has to say so, or the peer's reader stops
        // one byte early.
        var record = PlayerVitalsRecordUtility.Build(new PlayerVitals(194, 250));
        var serialized = MessageCodecUtility.SerializeMessages([record]);

        Assert.Equal("8000", Convert.ToHexString(serialized, 0, 2).ToLowerInvariant());
        Assert.Equal(0x03, serialized[2]);
        Assert.Equal(0x03, serialized[3]);
        Assert.Equal("c2fa", Convert.ToHexString(serialized, 4, 2).ToLowerInvariant());

        // And it walks back out of a content region as the record it went in as.
        var parsed = MessageCodecUtility.ParseMessages(serialized);
        Assert.Single(parsed);
        Assert.Equal(PlayerVitalsRecordUtility.FirstPlayerType, parsed[0].Type);
        Assert.Equal(record.Body, parsed[0].Body);
        Assert.Equal(194, PlayerVitalsRecordUtility.Parse(parsed[0].Body)!.Health);
    }

    [Fact]
    public void A_session_record_still_declares_its_body_length_alone()
    {
        // The other framing is unchanged by the vitals record: a session record's
        // length byte is the body and nothing else.
        var profile = PlayerProfileRecordUtility.BuildRosterClose();
        var serialized = MessageCodecUtility.SerializeMessages(
            [new UdpMessage(UdpCommandConstants.PlayerProfile, (byte)profile.Length, 0, profile)]);

        Assert.Equal((byte)profile.Length, serialized[2]);
        Assert.Single(MessageCodecUtility.ParseMessages(serialized));
    }
}