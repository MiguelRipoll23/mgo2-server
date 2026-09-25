using System.Buffers.Binary;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the two event records the client parses by fixed size. An under- or
/// over-length reply parses "successfully" off whatever the receive buffer held,
/// so the size is asserted here rather than trusted at the edge.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventInformationTests
{
    private static EventInformationService CreateService(Action<EventOptions>? configure = null)
    {
        var options = new EventOptions
        {
            WinRewards = "100,200,300,400,500",
            ParticipationReward = 50,
            InformationRuleFlags = 0x7f,
            DisplayedWinCount = 5,
        };
        configure?.Invoke(options);
        return new EventInformationService(Options.Create(options));
    }

    [Fact]
    public void Information_record_is_exactly_905_bytes()
    {
        var record = CreateService().BuildRecord(EventConstants.SurvivalSelector);

        Assert.Equal(EventConstants.InformationWireSize, record.Length);
    }

    [Fact]
    public void Information_record_carries_the_result_event_id_and_selector()
    {
        var record = CreateService().BuildRecord(EventConstants.TournamentSelector);

        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(record));
        Assert.Equal((uint)EventConstants.TransientEventIdentifier, BinaryPrimitives.ReadUInt32BigEndian(record.AsSpan(4)));
        Assert.Equal("TOURNAMENT", StringUtility.ReadFixedString(record, 14, 16));
    }

    [Fact]
    public void Information_record_places_rewards_rule_byte_and_win_count()
    {
        var record = CreateService(options =>
        {
            options.WinRewards = "100,200,300";
            options.DisplayedWinCount = 3;
        }).BuildRecord(EventConstants.SurvivalSelector);

        // Ten win slots begin at 838; the participation reward follows at 878.
        Assert.Equal(100, BinaryPrimitives.ReadInt32BigEndian(record.AsSpan(838)));
        Assert.Equal(300, BinaryPrimitives.ReadInt32BigEndian(record.AsSpan(846)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(record.AsSpan(850)));
        Assert.Equal(50, BinaryPrimitives.ReadInt32BigEndian(record.AsSpan(878)));

        // Eleven untouched bytes, then the rule byte and the drawn win count.
        Assert.Equal(0x7f, record[901]);
        Assert.Equal(3, record[904]);
    }

    [Fact]
    public void Information_record_window_closes_after_it_opens()
    {
        var record = CreateService(options =>
        {
            options.StartMinute = 0;
            options.EndMinute = 1440;
            options.TimeZone = "UTC";
        }).BuildRecord(EventConstants.SurvivalSelector);

        var start = BinaryPrimitives.ReadUInt32BigEndian(record.AsSpan(882));
        var end = BinaryPrimitives.ReadUInt32BigEndian(record.AsSpan(886));

        Assert.Equal(86_400u, end - start);
    }

    [Fact]
    public void Host_environment_round_trips_through_204_bytes()
    {
        var environment = EventHostEnvironment.CreateDefault();
        environment.WeaponRestrictions[3] = 0x5a;
        environment.SetRotation(7, 3, 9, 1);

        var writer = new PacketWriter();
        EventHostEnvironmentUtils.Write(writer, environment);
        var block = writer.Build();

        Assert.Equal(EventConstants.HostEnvironmentWireSize, block.Length);

        var decoded = EventHostEnvironmentUtils.Read(block);

        Assert.Equal(environment.MaximumPlayers, decoded.MaximumPlayers);
        Assert.Equal(environment.TeamDeathmatchTime, decoded.TeamDeathmatchTime);
        Assert.Equal(environment.CommonA, decoded.CommonA);
        Assert.Equal(environment.NetworkStatus, decoded.NetworkStatus);
        Assert.Equal(0x5a, decoded.WeaponRestrictions[3]);
        Assert.Equal(new byte[] { 3, 9, 1 }, decoded.Rotations[7]);
        Assert.Equal(new byte[] { 4, 1, 0 }, decoded.Rotations[0]);
    }
}
