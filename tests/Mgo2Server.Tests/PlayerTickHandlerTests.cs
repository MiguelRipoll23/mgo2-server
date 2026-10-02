using System.Net;
using Mgo2Server.GameplayServer.Commands;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Udp;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins what the gameplay server reports when a peer sends it the round's tick
/// records: every vitals and position record at debug, a death and a revive
/// again at information, and the bytes of the one position form nothing has
/// decoded.
/// </summary>
/// <remarks>
/// The bodies are the ones the live capture carried
/// (<c>docs/mgo2-game.pcapng</c>), verbatim. Neither record carries a character
/// id, a name or an address, so nothing here identifies anybody.
/// </remarks>
[Trait("Category", "Gameplay")]
public sealed class PlayerTickHandlerTests
{
    private static readonly IPEndPoint Joiner = new(IPAddress.Loopback, 40001);

    /// <summary>A walking character, in the form every alive record uses.</summary>
    private const string WalkingBody = "2000000000005af141000cfeff3f0000";

    /// <summary>A dead character: the 32-byte shape, the death bit set.</summary>
    private const string DeadBody = "221a000b01000000ef6e4845ab5a7845c61208474ade9b0d7c0140014ade0000";

    /// <summary>The 44-byte form, which nothing in it decoded.</summary>
    private const string UndecodedBody = "4101000602000000bd0e24457c7c47451ed905470068bfc600002f4500b888c6fe7f2bf918016ef6ef7c0000";

    /// <summary>Runs a vitals record through the handler and collects its log lines.</summary>
    /// <param name="states">State the handler reports changes against.</param>
    /// <param name="body">Record body; the captured bodies unless stated otherwise.</param>
    /// <returns>The formatted log lines, each prefixed with its level.</returns>
    private static async Task<List<string>> RunVitalsAsync(PlayerStateService states, byte[] body)
    {
        var lines = new List<string>();
        var handler = new PlayerVitalsHandler(states, new LevelledCollectingLogger<PlayerVitalsHandler>(lines));
        await handler.HandleAsync(
            Context(PlayerVitalsRecordUtility.FirstPlayerType, body, PlayerVitalsRecordUtility.AttributeClass));
        return lines;
    }

    /// <summary>Runs a position record through the handler and collects its log lines.</summary>
    /// <param name="states">State the handler reports changes against.</param>
    /// <param name="body">Record body; the captured bodies unless stated otherwise.</param>
    /// <returns>The formatted log lines, each prefixed with its level.</returns>
    private static async Task<List<string>> RunPositionAsync(PlayerStateService states, byte[] body)
    {
        var lines = new List<string>();
        var handler = new PlayerPositionHandler(states, new LevelledCollectingLogger<PlayerPositionHandler>(lines));
        await handler.HandleAsync(
            Context(PlayerPositionRecordUtility.FirstPlayerType, body, PlayerPositionRecordUtility.AttributeClass));
        return lines;
    }

    private static PeerContext Context(ushort type, byte[] body, byte flags) =>
        new(
            new PeerSession
            {
                RemoteAddress = Joiner.ToString(),
                DialBack = Joiner,
                CounterBase = 0x11223344,
                OutboundCounter = 0,
                SessionKey = 0,
                PeerIdentifier = 2,
                HostPeerIdentifier = 1,
                Established = true,
                LastSeenAt = 0,
                LastInboundSequence = 0,
            },
            UdpMessage.Create(type, body, flags),
            Joiner,
            5730,
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask);

    [Fact]
    public async Task Every_vitals_record_is_reported_at_debug()
    {
        // The capture holds 2 893 of these in a row between fights, so each one
        // is a debug line and none of them is an information line.
        var states = new PlayerStateService();
        var lines = await RunVitalsAsync(states, Convert.FromHexString("fafa"));

        var line = Assert.Single(lines);
        Assert.StartsWith("Debug", line);
        Assert.Contains("health 250", line);
        Assert.Contains("stamina 250", line);
    }

    [Fact]
    public async Task Health_reaching_zero_is_reported_again_at_information()
    {
        // The round's shape: full, full, then zero — and only the record that
        // changed the state is worth an information line.
        var states = new PlayerStateService();
        await RunVitalsAsync(states, Convert.FromHexString("fafa"));
        var lines = await RunVitalsAsync(states, Convert.FromHexString("00fa"));

        Assert.Contains(lines, line => line.StartsWith("Information") && line.Contains("died"));
    }

    [Fact]
    public async Task Health_coming_back_from_zero_is_reported_again_at_information()
    {
        var states = new PlayerStateService();
        await RunVitalsAsync(states, Convert.FromHexString("fafa"));
        await RunVitalsAsync(states, Convert.FromHexString("00fa"));
        var lines = await RunVitalsAsync(states, Convert.FromHexString("fafa"));

        Assert.Contains(lines, line => line.StartsWith("Information") && line.Contains("is back"));
    }

    [Fact]
    public async Task A_body_that_is_not_the_records_shape_is_not_read_as_one()
    {
        var lines = await RunVitalsAsync(new PlayerStateService(), new byte[3]);

        var line = Assert.Single(lines);
        Assert.StartsWith("Debug", line);
        Assert.Contains("3 bytes", line);
    }

    [Fact]
    public async Task Every_position_record_is_reported_in_world_units_at_debug()
    {
        var lines = await RunPositionAsync(new PlayerStateService(), Convert.FromHexString(WalkingBody));

        var line = Assert.Single(lines);
        Assert.StartsWith("Debug", line);
        Assert.Contains("x -5000", line);
        Assert.Contains("y 650", line);
        Assert.Contains("z -37500", line);
    }

    [Fact]
    public async Task The_death_bit_is_reported_again_at_information_when_it_flips()
    {
        // Alive first, so the flip is a change and not a first reading.
        var states = new PlayerStateService();
        await RunPositionAsync(states, Convert.FromHexString(WalkingBody));
        var lines = await RunPositionAsync(states, Convert.FromHexString(DeadBody));

        Assert.Contains(lines, line => line.StartsWith("Information") && line.Contains("died"));
    }

    [Fact]
    public async Task A_position_record_that_reports_alive_again_is_a_revive()
    {
        var states = new PlayerStateService();
        await RunPositionAsync(states, Convert.FromHexString(WalkingBody));
        await RunPositionAsync(states, Convert.FromHexString(DeadBody));
        var lines = await RunPositionAsync(states, Convert.FromHexString(WalkingBody));

        Assert.Contains(lines, line => line.StartsWith("Information") && line.Contains("is back"));
    }

    [Fact]
    public async Task The_form_nothing_has_decoded_is_reported_with_its_bytes()
    {
        // No offset in the 44-byte form lands on a character's path, so there is
        // no position to report — but the record is on the wire several times a
        // second and the bytes are all that is known about it.
        var lines = await RunPositionAsync(new PlayerStateService(), Convert.FromHexString(UndecodedBody));

        var line = Assert.Single(lines);
        Assert.StartsWith("Debug", line);
        Assert.Contains("44-byte form", line);
        Assert.Contains(UndecodedBody, line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_character_that_joins_already_dead_is_not_reported_as_dying()
    {
        // The peer's first record has nothing to have changed from.
        var lines = await RunPositionAsync(new PlayerStateService(), Convert.FromHexString(DeadBody));

        Assert.DoesNotContain(lines, line => line.StartsWith("Information"));
    }

    [Fact]
    public void Every_tick_identifier_the_capture_measured_reaches_a_handler()
    {
        var registry = new PeerCommandRegistry();
        PeerCommandHandlerRegistration.RegisterCommandHandlers(registry);

        Assert.True(registry.Has(PlayerVitalsRecordUtility.FirstPlayerType));
        Assert.True(registry.Has(PlayerVitalsRecordUtility.SecondPlayerType));
        Assert.All(
            PlayerPositionRecordUtility.MeasuredTypes.ToArray(),
            type => Assert.True(registry.Has(type)));
    }

    [Fact]
    public void The_measured_position_identifiers_are_the_ones_the_capture_carried()
    {
        // Each is one above the health identifier of the same character, and the
        // 0x800 bit names the second character rather than a second copy.
        Assert.Equal(0x0080, PlayerPositionRecordUtility.FirstPlayerType - 1);
        Assert.Equal(0x0880, PlayerPositionRecordUtility.SecondPlayerType - 1);
        Assert.Equal(0x00b2, PlayerPositionRecordUtility.ThirdPlayerType - 1);
        Assert.Equal(0x006c, PlayerPositionRecordUtility.FifthPlayerType - 1);
        Assert.Equal(0x00da, PlayerPositionRecordUtility.SixthPlayerType - 1);

        // The fourth is the third under the 0x800 bit, and the second the first.
        Assert.Equal(
            PlayerPositionRecordUtility.ThirdPlayerType | 0x800,
            PlayerPositionRecordUtility.FourthPlayerType);
        Assert.Equal(
            PlayerPositionRecordUtility.FirstPlayerType | 0x800,
            PlayerPositionRecordUtility.SecondPlayerType);

        Assert.Equal(
            [0x0081, 0x0881, 0x00b3, 0x08b3, 0x006d, 0x00db],
            PlayerPositionRecordUtility.MeasuredTypes.ToArray());
    }
}

/// <summary>Logger that keeps each line with the level it was written at.</summary>
/// <typeparam name="TCategory">Logger category.</typeparam>
/// <param name="lines">List every line is appended to.</param>
internal sealed class LevelledCollectingLogger<TCategory>(List<string> lines) : ILogger<TCategory>
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        lines.Add($"{logLevel}: {formatter(state, exception)}");
}