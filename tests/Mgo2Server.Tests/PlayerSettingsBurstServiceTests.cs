using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.Rooms;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the settings payloads to the bytes the recorded host wrote in
/// <c>docs/mgo2-game.pcapng</c> at t+4.9929, outbound counter 5.
/// </summary>
/// <remarks>
/// The burst used to be built as the leading tag, the slot, and zeros for the
/// rest of the length, and it left out the two fifty-four-byte records in slot
/// <c>0x15</c> that the capture shows after the joiner's payload. Both are
/// differences from the recorded host, and both are what these tests hold. The
/// slot bytes are the replay parser's settings slots: <c>0x00</c> is the room's
/// rotation, <c>0x01</c> a player's loadout.
///
/// The rotation record is the one body that is no longer the recorded host's:
/// its fifteen triples are written from the room's own rotation, which is a row
/// rather than a recording. Its own test below holds that, and the loadout and
/// blob tests hold the bodies that still are.
/// </remarks>
public sealed class PlayerSettingsBurstServiceTests
{
    /// <summary>
    /// The rotation record carries the room's own rounds — map, rule and the
    /// room's flags byte, per triple — and a rotation shorter than the field's
    /// fifteen entries is walked round rather than padded with the map the client
    /// reads as None.
    /// </summary>
    [Fact]
    public void TheRotationRecordCarriesTheRoomsOwnRounds()
    {
        MatchRotationEntry[] rotation =
        [
            new(Rule: 1, Map: 5, Flags: 0, TimeLimitMinutes: 8),
            new(Rule: 7, Map: 10, Flags: 0x20, TimeLimitMinutes: 4),
        ];

        var body = PlayerSettingsBurstService.RoomRotationBody(rotation);

        Assert.Equal(161, body.Length);
        Assert.Equal(PlayerSettingsBurstService.RoomRotationPayloadLength, body.Length);
        Assert.Equal(PlayerSettingsBurstService.SettingsPayloadType, body[0]);
        Assert.Equal(PlayerSettingsBurstService.RoomRotationSlot, body[1]);

        for (var slot = 0; slot < PlayerSettingsBurstService.RotationEntryCount; slot++)
        {
            var entry = rotation[slot % rotation.Length];
            var offset = PlayerSettingsBurstService.RotationOffset
                + (slot * PlayerSettingsBurstService.RotationEntryLength);
            Assert.Equal(entry.Map, body[offset]);
            Assert.Equal(entry.Rule, body[offset + 1]);
            Assert.Equal(entry.Flags, body[offset + 2]);
        }

        // The bytes around the triples are the recorded host's, because nothing
        // names them: the body opens on 0b 00 ff 27, which a tag-plus-slot build
        // could never produce.
        Assert.Equal(0xff, body[2]);
        Assert.Equal(0x27, body[3]);

        // The recorded body is what a room that cannot be read falls back to.
        var recorded = PlayerSettingsBurstService.RecordedRotationBody();
        Assert.Equal(161, recorded.Length);
        Assert.Equal(0x05, recorded[PlayerSettingsBurstService.RotationOffset]);
        Assert.Equal(0x01, recorded[PlayerSettingsBurstService.RotationOffset + 1]);
    }

    [Fact]
    public void ThePlayerLoadoutIsTheRecordedOne()
    {
        var body = LoadoutBodiesUtils.PlayerLoadout();

        Assert.Equal(50, body.Length);
        Assert.Equal(LoadoutBodiesUtils.PlayerLoadoutPayloadLength, body.Length);
        Assert.Equal(PlayerSettingsBurstService.SettingsPayloadType, body[0]);
        Assert.Equal(LoadoutBodiesUtils.PlayerLoadoutSlot, body[1]);
        Assert.Equal(0x02, body[2]);
    }

    /// <summary>
    /// The capture writes the fifty-four-byte record twice, byte for byte
    /// identical, and it is in slot <c>0x15</c> rather than the joiner's
    /// <c>0x01</c>.
    /// </summary>
    [Fact]
    public void TheSecondLoadoutIsTheRecordedOne()
    {
        var first = LoadoutBodiesUtils.SecondLoadout();
        var second = LoadoutBodiesUtils.SecondLoadout();

        Assert.Equal(54, first.Length);
        Assert.Equal(LoadoutBodiesUtils.SecondLoadoutPayloadLength, first.Length);
        Assert.Equal(PlayerSettingsBurstService.SettingsPayloadType, first[0]);
        Assert.Equal(LoadoutBodiesUtils.SecondLoadoutSlot, first[1]);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// The three payloads in one burst share a type byte and differ in slot and
    /// length, which is what tells the peer they are three records.
    /// </summary>
    [Fact]
    public void TheThreePayloadsAreDistinguishable()
    {
        var payloads = new[]
        {
            LoadoutBodiesUtils.PlayerLoadout(),
            LoadoutBodiesUtils.SecondLoadout(),
            LoadoutBodiesUtils.SecondLoadout(),
        };

        Assert.All(payloads, payload => Assert.Equal(PlayerSettingsBurstService.SettingsPayloadType, payload[0]));
        Assert.Equal(0x01, payloads[0][1]);
        Assert.Equal(0x15, payloads[1][1]);
        Assert.Equal(0x15, payloads[2][1]);
        Assert.NotEqual(payloads[0], payloads[1]);
    }

    [Fact]
    public void TheClosingStateBlobIsTheRecordedOne()
    {
        var blob = PlayerSettingsBurstService.BuildClosingStateBlob();

        Assert.Equal(22, blob.Length);
        Assert.Equal(0x40, blob[18]);
        Assert.Equal(0x00, blob[19]);
        Assert.Equal(0x00, blob[20]);
        Assert.Equal(0x32, blob[21]);
        Assert.Equal(new byte[18], blob[..18]);
    }

    /// <summary>
    /// The shape builder stays available for the tests that only care about the
    /// type byte and the slot, but nothing in the send path uses it: a
    /// zero-filled body is not one the peer has been shown to read.
    /// </summary>
    [Fact]
    public void TheShapeBuilderStillWritesTheTypeByteAndSlot()
    {
        var body = PlayerSettingsBurstService.BuildPlaceholderPayload(
            LoadoutBodiesUtils.SecondLoadoutSlot,
            54);

        Assert.Equal(54, body.Length);
        Assert.Equal(0x0b, body[0]);
        Assert.Equal(0x15, body[1]);
        Assert.Equal(new byte[52], body[2..]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlayerSettingsBurstService.BuildPlaceholderPayload(0x00, 1));
    }
}
