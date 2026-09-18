using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// The gameplay-options codec: builds the read payload and parses the write-back,
/// which is the same structure truncated before the list preferences trailer. The two
/// must be changed together.
/// <para>
/// Both halves work on the stored settings row, whose property initializers are the
/// game's own defaults, so a character with no row is served exactly what the client
/// itself starts from.
/// </para>
/// </summary>
public static class GameplayOptionsCodec
{
    /// <summary>Size of the read payload.</summary>
    public const int PayloadSize = 0x150;

    /// <summary>Size of the write-back, which omits the list-preferences trailer.</summary>
    public const int WritebackSize = 0x130;

    /// <summary>Length of one codec name field.</summary>
    private const int CodecNameLength = 64;

    /// <summary>
    /// The 32 bytes closing the read payload: filter, sort and search
    /// preferences. All zeros is the game's own default.
    /// </summary>
    private static readonly byte[] ListPreferencesTrailer = new byte[32];

    /// <summary>Builds the read payload from the stored settings.</summary>
    /// <param name="stored">Stored settings, or <c>null</c> when the character has none.</param>
    public static byte[] BuildPayload(CharacterGameplayOptions? stored)
    {
        var options = stored ?? new CharacterGameplayOptions();

        var privacyA = 1 |
            ((options.OnlineStatusMode & 0b11) << 4) |
            (options.EmailFriendsOnly ? 0b01000000 : 0);

        var normalView =
            (options.NormalViewVerticalInvert ? 0b1 : 0) |
            (options.NormalViewHorizontalInvert ? 0b10 : 0) |
            (Speed(options.NormalViewSpeed) << 4);

        var shoulderView =
            (options.ShoulderViewVerticalInvert ? 0b1 : 0) |
            (options.ShoulderViewHorizontalInvert ? 0b10 : 0) |
            (Speed(options.ShoulderViewSpeed) << 4);

        var firstView =
            (options.FirstViewVerticalInvert ? 0b1 : 0) |
            (options.FirstViewHorizontalInvert ? 0b10 : 0) |
            (options.FirstViewPlayerDirection ? 0b100 : 0) |
            (Speed(options.FirstViewSpeed) << 4);

        var switchModes =
            (options.WeaponSwitchMode & 0b1111) |
            ((options.ItemSwitchMode & 0b1111) << 4);

        var voiceChatA =
            (options.VoiceChatOutputDevice & 0b11) |
            ((options.CodecOutputDevice & 0b11) << 2) |
            ((options.VoiceChatRecognitionLevel & 0b1111) << 4);

        var voiceChatB =
            (options.VoiceChatVolume & 0b1111) |
            ((options.HeadsetVolume & 0b1111) << 4);

        var weaponSwitchAB =
            (options.WeaponSwitchA & 0b1111) |
            ((options.WeaponSwitchB & 0b1111) << 4);

        var weaponSwitchC =
            (options.WeaponSwitchC & 0b1111) |
            ((options.WeaponSwitchNow & 0b1111) << 4);

        var weaponSwitchRecall =
            (options.WeaponSwitchBefore & 0b1111) |
            ((options.WeaponSwitchToggle & 0b1111) << 4);

        var firstViewMemory = options.FirstViewMemory ? 1 : 0;

        var privacyB =
            (options.ReceiveNotices ? 0b1 : 0) |
            (options.ReceiveInvites ? 0b10000 : 0);

        var lockOnAndBgm =
            (options.LockOnEnabled ? 0b1 : 0) |
            (((options.BgmVolume + 1) & 0b1111) << 4);

        var radar =
            (options.RadarLockNorth ? 0b1 : 0) |
            (options.RadarFloorHide ? 0b10000 : 0);

        var hudDisplay =
            (options.HudDisplaySize & 0b11) |
            (options.HudHideNameTags ? 0b10000 : 0);

        var bytes = new byte[PayloadSize];
        var at = 0;
        void Put(int value) => bytes[at++] = (byte)(value & 0xff);

        Put(privacyA);
        Put(normalView);
        Put(shoulderView);
        Put(firstView);
        Put(Speed(options.ViewChangeSpeed));
        at += 6; // +5..+10 unmapped
        Put(switchModes);
        at += 1; // +12 unmapped
        Put(voiceChatA);
        Put(voiceChatB);
        Put(weaponSwitchAB);
        Put(weaponSwitchC);
        Put(weaponSwitchRecall);
        Put(firstViewMemory);
        Put(privacyB);
        Put(lockOnAndBgm);
        Put(radar);
        Put(hudDisplay);
        at += 9; // +23..+31 unmapped

        // Codec shortcut entries: four bytes each, at +32.
        Put(options.Codec1A);
        Put(options.Codec1B);
        Put(options.Codec1C);
        Put(options.Codec1D);
        Put(options.Codec2A);
        Put(options.Codec2B);
        Put(options.Codec2C);
        Put(options.Codec2D);
        Put(options.Codec3A);
        Put(options.Codec3B);
        Put(options.Codec3C);
        Put(options.Codec3D);
        Put(options.Codec4A);
        Put(options.Codec4B);
        Put(options.Codec4C);
        Put(options.Codec4D);

        StringUtility.WriteFixedStringInto(bytes, at, options.Codec1Name, CodecNameLength);
        at += CodecNameLength;
        StringUtility.WriteFixedStringInto(bytes, at, options.Codec2Name, CodecNameLength);
        at += CodecNameLength;
        StringUtility.WriteFixedStringInto(bytes, at, options.Codec3Name, CodecNameLength);
        at += CodecNameLength;
        StringUtility.WriteFixedStringInto(bytes, at, options.Codec4Name, CodecNameLength);
        at += CodecNameLength;

        Array.Copy(ListPreferencesTrailer, 0, bytes, at, ListPreferencesTrailer.Length);
        return bytes;
    }

    /// <summary>
    /// Parses the write-back into the settings row. Returns <c>null</c> for a short
    /// payload, because half-applying a truncated write-back would persist a mixture
    /// of the player's settings and stale bytes.
    /// </summary>
    /// <param name="payload">Payload bytes to parse.</param>
    public static CharacterGameplayOptions? ParsePayload(byte[] payload)
    {
        if (payload.Length < WritebackSize)
        {
            return null;
        }

        byte At(int offset) => payload[offset];

        var privacyA = At(0);
        var normalView = At(1);
        var shoulderView = At(2);
        var firstView = At(3);
        var switchModes = At(11);
        var voiceChatA = At(13);
        var voiceChatB = At(14);
        var weaponSwitchAB = At(15);
        var cycleC = At(16);
        var recall = At(17);
        var privacyB = At(19);
        var lockOnAndBgm = At(20);
        var radar = At(21);
        var hud = At(22);

        return new CharacterGameplayOptions
        {
            OnlineStatusMode = (privacyA >> 4) & 0b11,
            EmailFriendsOnly = (privacyA & 0b01000000) != 0,

            NormalViewVerticalInvert = (normalView & 0b1) != 0,
            NormalViewHorizontalInvert = (normalView & 0b10) != 0,
            NormalViewSpeed = SpeedFromWire(normalView >> 4),

            ShoulderViewVerticalInvert = (shoulderView & 0b1) != 0,
            ShoulderViewHorizontalInvert = (shoulderView & 0b10) != 0,
            ShoulderViewSpeed = SpeedFromWire(shoulderView >> 4),

            FirstViewVerticalInvert = (firstView & 0b1) != 0,
            FirstViewHorizontalInvert = (firstView & 0b10) != 0,
            FirstViewPlayerDirection = (firstView & 0b100) != 0,
            FirstViewSpeed = SpeedFromWire(firstView >> 4),

            ViewChangeSpeed = SpeedFromWire(At(4)),

            WeaponSwitchMode = switchModes & 0b1111,
            ItemSwitchMode = (switchModes >> 4) & 0b1111,

            VoiceChatOutputDevice = voiceChatA & 0b11,
            CodecOutputDevice = (voiceChatA >> 2) & 0b11,
            VoiceChatRecognitionLevel = (voiceChatA >> 4) & 0b1111,

            VoiceChatVolume = voiceChatB & 0b1111,
            HeadsetVolume = (voiceChatB >> 4) & 0b1111,

            WeaponSwitchA = weaponSwitchAB & 0b1111,
            WeaponSwitchB = (weaponSwitchAB >> 4) & 0b1111,

            WeaponSwitchC = cycleC & 0b1111,
            WeaponSwitchNow = (cycleC >> 4) & 0b1111,

            WeaponSwitchBefore = recall & 0b1111,
            WeaponSwitchToggle = (recall >> 4) & 0b1111,

            FirstViewMemory = (At(18) & 0b1111) != 0,

            ReceiveNotices = (privacyB & 0b1) != 0,
            ReceiveInvites = (privacyB & 0b10000) != 0,

            LockOnEnabled = (lockOnAndBgm & 0b1) != 0,
            BgmVolume = Math.Max(0, ((lockOnAndBgm >> 4) & 0b1111) - 1),

            RadarLockNorth = (radar & 0b1) != 0,
            RadarFloorHide = (radar & 0b10000) != 0,

            HudDisplaySize = hud & 0b11,
            HudHideNameTags = (hud & 0b10000) != 0,

            Codec1A = At(32),
            Codec1B = At(33),
            Codec1C = At(34),
            Codec1D = At(35),
            Codec2A = At(36),
            Codec2B = At(37),
            Codec2C = At(38),
            Codec2D = At(39),
            Codec3A = At(40),
            Codec3B = At(41),
            Codec3C = At(42),
            Codec3D = At(43),
            Codec4A = At(44),
            Codec4B = At(45),
            Codec4C = At(46),
            Codec4D = At(47),

            Codec1Name = ReadNullTerminatedString(payload, 48),
            Codec2Name = ReadNullTerminatedString(payload, 48 + CodecNameLength),
            Codec3Name = ReadNullTerminatedString(payload, 48 + CodecNameLength * 2),
            Codec4Name = ReadNullTerminatedString(payload, 48 + CodecNameLength * 3),
        };
    }

    /// <summary>Speeds are shown one-based but sent zero-based.</summary>
    private static int Speed(int value) => (value - 1) & 0b1111;

    /// <summary>The inverse of <see cref="Speed"/>.</summary>
    private static int SpeedFromWire(int wire) => (wire & 0b1111) + 1;

    /// <summary>Reads a null-terminated fixed-width string.</summary>
    private static string ReadNullTerminatedString(byte[] payload, int offset)
    {
        var characters = new List<char>(CodecNameLength);
        for (var index = 0; index < CodecNameLength; index++)
        {
            var value = payload[offset + index];
            if (value == 0)
            {
                break;
            }

            characters.Add((char)value);
        }

        return new string([.. characters]);
    }
}
