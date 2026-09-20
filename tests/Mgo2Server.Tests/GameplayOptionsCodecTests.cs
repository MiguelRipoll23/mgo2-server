using System.Reflection;
using Mgo2Server.GameLobbyServer.Commands.Game.Characters;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the gameplay-options codec against the stored row. The read payload and the
/// write-back are two halves of one structure, so a setting that only one of them knows
/// about is a player's choice that never comes back.
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class GameplayOptionsCodecTests
{
    /// <summary>Every setting the wire carries, which is the row minus its key.</summary>
    private static readonly PropertyInfo[] WireSettings =
    [
        .. typeof(CharacterGameplayOptions)
            .GetProperties()
            .Where(property => property.Name != nameof(CharacterGameplayOptions.Character))
            .Where(property => property.Name != nameof(CharacterGameplayOptions.CharacterIdentifier)),
    ];

    /// <summary>
    /// A character that has never stored anything is served the defaults, which live on the
    /// row itself — there is no second copy to fall out of step with them.
    /// </summary>
    [Fact]
    public void A_character_with_no_stored_row_is_served_the_games_own_defaults()
    {
        Assert.Equal(
            GameplayOptionsCodec.BuildPayload(new CharacterGameplayOptions()),
            GameplayOptionsCodec.BuildPayload(null));
    }

    /// <summary>The bytes the client is served for a character with no settings at all.</summary>
    [Fact]
    public void The_defaults_are_the_ones_the_client_itself_starts_from()
    {
        var payload = GameplayOptionsCodec.BuildPayload(null);

        Assert.Equal(GameplayOptionsCodec.PayloadSize, payload.Length);
        Assert.Equal(0x01, payload[0]); // the privacy byte's constant bit
        Assert.Equal(0x40, payload[1]); // the normal view at speed five, zero-based four
        Assert.Equal(0x40, payload[2]);
        Assert.Equal(0x44, payload[3]); // the first-person view, plus its direction bit
    }

    /// <summary>
    /// Every setting, chosen away from its default so a value that is dropped, swapped or
    /// defaulted instead of stored fails here rather than reaching a player's menu.
    /// </summary>
    [Fact]
    public void Every_stored_setting_survives_a_round_trip()
    {
        var stored = new CharacterGameplayOptions
        {
            OnlineStatusMode = 2,
            EmailFriendsOnly = true,
            ReceiveNotices = false,
            ReceiveInvites = false,
            NormalViewVerticalInvert = true,
            NormalViewHorizontalInvert = true,
            NormalViewSpeed = 16,
            ShoulderViewVerticalInvert = true,
            ShoulderViewHorizontalInvert = true,
            ShoulderViewSpeed = 1,
            FirstViewVerticalInvert = true,
            FirstViewHorizontalInvert = true,
            FirstViewSpeed = 12,
            FirstViewPlayerDirection = false,
            ViewChangeSpeed = 3,
            FirstViewMemory = true,
            RadarLockNorth = true,
            RadarFloorHide = true,
            HudDisplaySize = 2,
            HudHideNameTags = true,
            LockOnEnabled = true,
            WeaponSwitchMode = 1,
            WeaponSwitchA = 4,
            WeaponSwitchB = 5,
            WeaponSwitchC = 6,
            WeaponSwitchNow = 7,
            WeaponSwitchBefore = 8,
            WeaponSwitchToggle = 9,
            ItemSwitchMode = 0,
            VoiceChatOutputDevice = 2,
            CodecOutputDevice = 3,
            Codec1Name = "Alpha",
            Codec1A = 11,
            Codec1B = 12,
            Codec1C = 13,
            Codec1D = 14,
            Codec2Name = "Bravo",
            Codec2A = 21,
            Codec2B = 22,
            Codec2C = 23,
            Codec2D = 24,
            Codec3Name = "Charlie",
            Codec3A = 31,
            Codec3B = 32,
            Codec3C = 33,
            Codec3D = 34,
            Codec4Name = "Delta",
            Codec4A = 41,
            Codec4B = 42,
            Codec4C = 43,
            Codec4D = 44,
            VoiceChatRecognitionLevel = 9,
            VoiceChatVolume = 4,
            HeadsetVolume = 6,
            BgmVolume = 0,
        };

        // The write-back is the read payload truncated before the list preferences trailer.
        var writeback = GameplayOptionsCodec.BuildPayload(stored)[..GameplayOptionsCodec.WritebackSize];
        var parsed = GameplayOptionsCodec.ParsePayload(writeback);

        Assert.NotNull(parsed);

        // A setting added to the row has to be carried by the codec, so the count is pinned:
        // this failing is the reminder, not an unexpected collapse further down.
        Assert.Equal(55, WireSettings.Length);

        foreach (var setting in WireSettings)
        {
            Assert.Equal(setting.GetValue(stored), setting.GetValue(parsed));
        }
    }

    /// <summary>A truncated write-back is refused whole rather than applied in part.</summary>
    [Fact]
    public void A_short_writeback_is_refused()
    {
        var short_ = new byte[GameplayOptionsCodec.WritebackSize - 1];

        Assert.Null(GameplayOptionsCodec.ParsePayload(short_));
    }
}
