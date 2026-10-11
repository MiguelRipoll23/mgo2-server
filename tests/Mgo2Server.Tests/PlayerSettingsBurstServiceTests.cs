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
/// </remarks>
public sealed class PlayerSettingsBurstServiceTests
{
    [Fact]
    public void TheRoomRotationIsTheRecordedOne()
    {
        var body = PlayerSettingsBurstService.RoomRotationPayload();

        Assert.Equal(161, body.Length);
        Assert.Equal(PlayerSettingsBurstService.RoomRotationPayloadLength, body.Length);
        Assert.Equal(PlayerSettingsBurstService.SettingsPayloadType, body[0]);
        Assert.Equal(PlayerSettingsBurstService.RoomRotationSlot, body[1]);
        // The capture's rotation is not a zero-filled body: it opens on
        // 0b 00 ff 27, which a tag-plus-slot build could never produce.
        Assert.Equal(0xff, body[2]);
        Assert.Equal(0x27, body[3]);
    }

    [Fact]
    public void ThePlayerLoadoutIsTheRecordedOne()
    {
        var body = PlayerSettingsBurstService.PlayerLoadoutPayload();

        Assert.Equal(50, body.Length);
        Assert.Equal(PlayerSettingsBurstService.PlayerLoadoutPayloadLength, body.Length);
        Assert.Equal(PlayerSettingsBurstService.SettingsPayloadType, body[0]);
        Assert.Equal(PlayerSettingsBurstService.PlayerLoadoutSlot, body[1]);
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
        var first = PlayerSettingsBurstService.SecondLoadoutPayload();
        var second = PlayerSettingsBurstService.SecondLoadoutPayload();

        Assert.Equal(54, first.Length);
        Assert.Equal(PlayerSettingsBurstService.SecondLoadoutPayloadLength, first.Length);
        Assert.Equal(PlayerSettingsBurstService.SettingsPayloadType, first[0]);
        Assert.Equal(PlayerSettingsBurstService.SecondLoadoutSlot, first[1]);
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
            PlayerSettingsBurstService.PlayerLoadoutPayload(),
            PlayerSettingsBurstService.SecondLoadoutPayload(),
            PlayerSettingsBurstService.SecondLoadoutPayload(),
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
            PlayerSettingsBurstService.SecondLoadoutSlot,
            54);

        Assert.Equal(54, body.Length);
        Assert.Equal(0x0b, body[0]);
        Assert.Equal(0x15, body[1]);
        Assert.Equal(new byte[52], body[2..]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlayerSettingsBurstService.BuildPlaceholderPayload(0x00, 1));
    }
}
