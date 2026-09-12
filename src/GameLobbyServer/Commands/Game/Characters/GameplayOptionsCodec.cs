using System.Text.Json.Nodes;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// The gameplay-options codec: builds the read payload and parses the
/// write-back, which is the same structure truncated before the list
/// preferences trailer. The two must be changed together.
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

    /// <summary>The presets applied when a setting was never stored.</summary>
    private static readonly Dictionary<string, object> DefaultOptions = new()
    {
        ["onlineStatusMode"] = 0,
        ["emailFriendsOnly"] = false,
        ["receiveNotices"] = true,
        ["receiveInvites"] = true,
        ["normalViewVerticalInvert"] = false,
        ["normalViewHorizontalInvert"] = false,
        ["normalViewSpeed"] = 5,
        ["shoulderViewVerticalInvert"] = false,
        ["shoulderViewHorizontalInvert"] = false,
        ["shoulderViewSpeed"] = 5,
        ["firstViewVerticalInvert"] = false,
        ["firstViewHorizontalInvert"] = false,
        ["firstViewSpeed"] = 5,
        ["firstViewPlayerDirection"] = true,
        ["viewChangeSpeed"] = 5,
        ["firstViewMemory"] = false,
        ["radarLockNorth"] = false,
        ["radarFloorHide"] = false,
        ["hudDisplaySize"] = 0,
        ["hudHideNameTags"] = false,
        ["lockOnEnabled"] = false,
        ["weaponSwitchMode"] = 2,
        ["weaponSwitchA"] = 0,
        ["weaponSwitchB"] = 1,
        ["weaponSwitchC"] = 2,
        ["weaponSwitchNow"] = 0,
        ["weaponSwitchBefore"] = 1,
        ["weaponSwitchToggle"] = 2,
        ["itemSwitchMode"] = 2,
        ["voiceChatOutputDevice"] = 0,
        ["codecOutputDevice"] = 0,
        ["codec1Name"] = string.Empty,
        ["codec1a"] = 1,
        ["codec1b"] = 3,
        ["codec1c"] = 4,
        ["codec1d"] = 2,
        ["codec2Name"] = string.Empty,
        ["codec2a"] = 10,
        ["codec2b"] = 12,
        ["codec2c"] = 13,
        ["codec2d"] = 11,
        ["codec3Name"] = string.Empty,
        ["codec3a"] = 14,
        ["codec3b"] = 16,
        ["codec3c"] = 17,
        ["codec3d"] = 15,
        ["codec4Name"] = string.Empty,
        ["codec4a"] = 5,
        ["codec4b"] = 7,
        ["codec4c"] = 8,
        ["codec4d"] = 6,
        ["voiceChatRecognitionLevel"] = 5,
        ["voiceChatVolume"] = 5,
        ["headsetVolume"] = 5,
        ["bgmVolume"] = 10,
    };

    /// <summary>Builds the read payload from the stored settings.</summary>
    /// <param name="stored">Stored settings, or <c>null</c> when the character has none.</param>
    public static byte[] BuildPayload(JsonNode? stored)
    {
        var data = Resolve(stored);

        var privacyA = 1 |
            ((Number(data, "onlineStatusMode", 0) & 0b11) << 4) |
            (Boolean(data, "emailFriendsOnly", false) ? 0b01000000 : 0);

        var normalView =
            (Boolean(data, "normalViewVerticalInvert", false) ? 0b1 : 0) |
            (Boolean(data, "normalViewHorizontalInvert", false) ? 0b10 : 0) |
            (Speed(Number(data, "normalViewSpeed", 5)) << 4);

        var shoulderView =
            (Boolean(data, "shoulderViewVerticalInvert", false) ? 0b1 : 0) |
            (Boolean(data, "shoulderViewHorizontalInvert", false) ? 0b10 : 0) |
            (Speed(Number(data, "shoulderViewSpeed", 5)) << 4);

        var firstView =
            (Boolean(data, "firstViewVerticalInvert", false) ? 0b1 : 0) |
            (Boolean(data, "firstViewHorizontalInvert", false) ? 0b10 : 0) |
            (Boolean(data, "firstViewPlayerDirection", true) ? 0b100 : 0) |
            (Speed(Number(data, "firstViewSpeed", 5)) << 4);

        var switchModes =
            (Number(data, "weaponSwitchMode", 2) & 0b1111) |
            ((Number(data, "itemSwitchMode", 2) & 0b1111) << 4);

        var voiceChatA =
            (Number(data, "voiceChatOutputDevice", 0) & 0b11) |
            ((Number(data, "codecOutputDevice", 0) & 0b11) << 2) |
            ((Number(data, "voiceChatRecognitionLevel", 5) & 0b1111) << 4);

        var voiceChatB =
            (Number(data, "voiceChatVolume", 5) & 0b1111) |
            ((Number(data, "headsetVolume", 5) & 0b1111) << 4);

        var weaponSwitchAB =
            (Number(data, "weaponSwitchA", 0) & 0b1111) |
            ((Number(data, "weaponSwitchB", 1) & 0b1111) << 4);

        var weaponSwitchC =
            (Number(data, "weaponSwitchC", 2) & 0b1111) |
            ((Number(data, "weaponSwitchNow", 0) & 0b1111) << 4);

        var weaponSwitchRecall =
            (Number(data, "weaponSwitchBefore", 1) & 0b1111) |
            ((Number(data, "weaponSwitchToggle", 2) & 0b1111) << 4);

        var firstViewMemory = Boolean(data, "firstViewMemory", false) ? 1 : 0;

        var privacyB =
            (Boolean(data, "receiveNotices", true) ? 0b1 : 0) |
            (Boolean(data, "receiveInvites", true) ? 0b10000 : 0);

        var lockOnAndBgm =
            (Boolean(data, "lockOnEnabled", false) ? 0b1 : 0) |
            (((Number(data, "bgmVolume", 10) + 1) & 0b1111) << 4);

        var radar =
            (Boolean(data, "radarLockNorth", false) ? 0b1 : 0) |
            (Boolean(data, "radarFloorHide", false) ? 0b10000 : 0);

        var hudDisplay =
            (Number(data, "hudDisplaySize", 0) & 0b11) |
            (Boolean(data, "hudHideNameTags", false) ? 0b10000 : 0);

        var bytes = new byte[PayloadSize];
        var at = 0;
        void Put(int value) => bytes[at++] = (byte)(value & 0xff);

        Put(privacyA);
        Put(normalView);
        Put(shoulderView);
        Put(firstView);
        Put(Speed(Number(data, "viewChangeSpeed", 5)));
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
        Put(Number(data, "codec1a", 1));
        Put(Number(data, "codec1b", 3));
        Put(Number(data, "codec1c", 4));
        Put(Number(data, "codec1d", 2));
        Put(Number(data, "codec2a", 10));
        Put(Number(data, "codec2b", 12));
        Put(Number(data, "codec2c", 13));
        Put(Number(data, "codec2d", 11));
        Put(Number(data, "codec3a", 14));
        Put(Number(data, "codec3b", 16));
        Put(Number(data, "codec3c", 17));
        Put(Number(data, "codec3d", 15));
        Put(Number(data, "codec4a", 5));
        Put(Number(data, "codec4b", 7));
        Put(Number(data, "codec4c", 8));
        Put(Number(data, "codec4d", 6));

        StringUtility.WriteFixedStringInto(bytes, at, Text(data, "codec1Name", string.Empty), CodecNameLength);
        at += CodecNameLength;
        StringUtility.WriteFixedStringInto(bytes, at, Text(data, "codec2Name", string.Empty), CodecNameLength);
        at += CodecNameLength;
        StringUtility.WriteFixedStringInto(bytes, at, Text(data, "codec3Name", string.Empty), CodecNameLength);
        at += CodecNameLength;
        StringUtility.WriteFixedStringInto(bytes, at, Text(data, "codec4Name", string.Empty), CodecNameLength);
        at += CodecNameLength;

        Array.Copy(ListPreferencesTrailer, 0, bytes, at, ListPreferencesTrailer.Length);
        return bytes;
    }

    /// <summary>
    /// Parses the write-back into named settings. Returns <c>null</c> for a
    /// short payload, because half-applying a truncated write-back would
    /// persist a mixture of the player's settings and stale bytes.
    /// </summary>
    /// <param name="payload">Payload bytes to parse.</param>
    public static JsonObject? ParsePayload(byte[] payload)
    {
        if (payload.Length < WritebackSize)
        {
            return null;
        }

        byte At(int offset) => payload[offset];

        var settings = new JsonObject();

        var privacyA = At(0);
        settings["onlineStatusMode"] = (privacyA >> 4) & 0b11;
        settings["emailFriendsOnly"] = (privacyA & 0b01000000) != 0;

        var normalView = At(1);
        settings["normalViewVerticalInvert"] = (normalView & 0b1) != 0;
        settings["normalViewHorizontalInvert"] = (normalView & 0b10) != 0;
        settings["normalViewSpeed"] = SpeedFromWire(normalView >> 4);

        var shoulderView = At(2);
        settings["shoulderViewVerticalInvert"] = (shoulderView & 0b1) != 0;
        settings["shoulderViewHorizontalInvert"] = (shoulderView & 0b10) != 0;
        settings["shoulderViewSpeed"] = SpeedFromWire(shoulderView >> 4);

        var firstView = At(3);
        settings["firstViewVerticalInvert"] = (firstView & 0b1) != 0;
        settings["firstViewHorizontalInvert"] = (firstView & 0b10) != 0;
        settings["firstViewPlayerDirection"] = (firstView & 0b100) != 0;
        settings["firstViewSpeed"] = SpeedFromWire(firstView >> 4);

        settings["viewChangeSpeed"] = SpeedFromWire(At(4));

        var switchModes = At(11);
        settings["weaponSwitchMode"] = switchModes & 0b1111;
        settings["itemSwitchMode"] = (switchModes >> 4) & 0b1111;

        var voiceChatA = At(13);
        settings["voiceChatOutputDevice"] = voiceChatA & 0b11;
        settings["codecOutputDevice"] = (voiceChatA >> 2) & 0b11;
        settings["voiceChatRecognitionLevel"] = (voiceChatA >> 4) & 0b1111;

        var voiceChatB = At(14);
        settings["voiceChatVolume"] = voiceChatB & 0b1111;
        settings["headsetVolume"] = (voiceChatB >> 4) & 0b1111;

        var weaponSwitchAB = At(15);
        settings["weaponSwitchA"] = weaponSwitchAB & 0b1111;
        settings["weaponSwitchB"] = (weaponSwitchAB >> 4) & 0b1111;

        var cycleC = At(16);
        settings["weaponSwitchC"] = cycleC & 0b1111;
        settings["weaponSwitchNow"] = (cycleC >> 4) & 0b1111;

        var recall = At(17);
        settings["weaponSwitchBefore"] = recall & 0b1111;
        settings["weaponSwitchToggle"] = (recall >> 4) & 0b1111;

        settings["firstViewMemory"] = (At(18) & 0b1111) != 0;

        var privacyB = At(19);
        settings["receiveNotices"] = (privacyB & 0b1) != 0;
        settings["receiveInvites"] = (privacyB & 0b10000) != 0;

        var lockOnAndBgm = At(20);
        settings["lockOnEnabled"] = (lockOnAndBgm & 0b1) != 0;
        settings["bgmVolume"] = Math.Max(0, ((lockOnAndBgm >> 4) & 0b1111) - 1);

        var radar = At(21);
        settings["radarLockNorth"] = (radar & 0b1) != 0;
        settings["radarFloorHide"] = (radar & 0b10000) != 0;

        var hud = At(22);
        settings["hudDisplaySize"] = hud & 0b11;
        settings["hudHideNameTags"] = (hud & 0b10000) != 0;

        settings["codec1a"] = At(32);
        settings["codec1b"] = At(33);
        settings["codec1c"] = At(34);
        settings["codec1d"] = At(35);
        settings["codec2a"] = At(36);
        settings["codec2b"] = At(37);
        settings["codec2c"] = At(38);
        settings["codec2d"] = At(39);
        settings["codec3a"] = At(40);
        settings["codec3b"] = At(41);
        settings["codec3c"] = At(42);
        settings["codec3d"] = At(43);
        settings["codec4a"] = At(44);
        settings["codec4b"] = At(45);
        settings["codec4c"] = At(46);
        settings["codec4d"] = At(47);

        settings["codec1Name"] = ReadNullTerminatedString(payload, 48);
        settings["codec2Name"] = ReadNullTerminatedString(payload, 48 + CodecNameLength);
        settings["codec3Name"] = ReadNullTerminatedString(payload, 48 + CodecNameLength * 2);
        settings["codec4Name"] = ReadNullTerminatedString(payload, 48 + CodecNameLength * 3);

        return settings;
    }

    /// <summary>Parses a stored options blob into a node, returning <c>null</c> when it is not an object.</summary>
    /// <param name="stored">Stored JSON text.</param>
    public static JsonNode? ParseStored(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(stored);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, object> Resolve(JsonNode? stored)
    {
        var data = new Dictionary<string, object>(DefaultOptions);

        if (stored is not JsonObject storedObject)
        {
            return data;
        }

        foreach (var pair in storedObject)
        {
            switch (pair.Value)
            {
                case JsonValue value when value.TryGetValue<double>(out var number):
                    data[pair.Key] = (int)number;
                    break;
                case JsonValue value when value.TryGetValue<bool>(out var flag):
                    data[pair.Key] = flag;
                    break;
                case JsonValue value when value.TryGetValue<string>(out var text):
                    data[pair.Key] = text;
                    break;
            }
        }

        return data;
    }

    private static int Number(Dictionary<string, object> data, string key, int fallback) =>
        data.TryGetValue(key, out var value) && value is int number ? number : fallback;

    private static bool Boolean(Dictionary<string, object> data, string key, bool fallback) =>
        data.TryGetValue(key, out var value) && value is bool flag ? flag : fallback;

    private static string Text(Dictionary<string, object> data, string key, string fallback) =>
        data.TryGetValue(key, out var value) && value is string text ? text : fallback;

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
