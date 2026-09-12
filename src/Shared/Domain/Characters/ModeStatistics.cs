using System.Text.Json;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// The per-mode tally stored in one of the statistics blobs of a character.
/// Property order is fixed by the wire format, so serialized blobs stay
/// byte-identical to what the client sends and expects.
/// </summary>
public sealed class ModeStatistics
{
    /// <summary>Rounds won.</summary>
    public int Wins { get; set; }

    /// <summary>Rounds played.</summary>
    public int Rounds { get; set; }

    /// <summary>Score earned.</summary>
    public int Score { get; set; }

    /// <summary>Seconds played.</summary>
    public int Time { get; set; }

    /// <summary>Kills.</summary>
    public int Kills { get; set; }

    /// <summary>Deaths.</summary>
    public int Deaths { get; set; }

    /// <summary>Stuns delivered.</summary>
    public int Stuns { get; set; }

    /// <summary>Stuns received.</summary>
    public int StunsRec { get; set; }

    /// <summary>Headshot kills.</summary>
    public int HsKills { get; set; }

    /// <summary>Headshot deaths.</summary>
    public int HsDeaths { get; set; }

    /// <summary>Headshot stuns delivered.</summary>
    public int HsStuns { get; set; }

    /// <summary>Headshot stuns received.</summary>
    public int HsStunsRec { get; set; }

    /// <summary>Lock-on kills.</summary>
    public int LockKills { get; set; }

    /// <summary>Lock-on deaths.</summary>
    public int LockDeaths { get; set; }

    /// <summary>Lock-on stuns delivered.</summary>
    public int LockStuns { get; set; }

    /// <summary>Lock-on stuns received.</summary>
    public int LockStunsRec { get; set; }
}

/// <summary>Serializes and reads the per-mode statistics blobs of a character.</summary>
public static class ModeStatisticsCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Serialized default blob, stored for every mode when a character is created.</summary>
    public static string DefaultJson => JsonSerializer.Serialize(new ModeStatistics(), SerializerOptions);

    /// <summary>Serializes a blob.</summary>
    /// <param name="statistics">Statistics to serialize.</param>
    public static string Serialize(ModeStatistics statistics) =>
        JsonSerializer.Serialize(statistics, SerializerOptions);

    /// <summary>Reads a blob, falling back to the default when it is absent or malformed.</summary>
    /// <param name="json">Serialized blob.</param>
    public static ModeStatistics Deserialize(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new ModeStatistics();
        }

        try
        {
            return JsonSerializer.Deserialize<ModeStatistics>(json, SerializerOptions) ?? new ModeStatistics();
        }
        catch (JsonException)
        {
            return new ModeStatistics();
        }
    }

    /// <summary>Returns the statistics blob of a character for a game mode.</summary>
    /// <param name="statistics">Statistics row of the character.</param>
    /// <param name="gameMode">Game mode index, as carried by the wire format.</param>
    public static ModeStatistics ForMode(CharacterStatistics statistics, int gameMode) =>
        Deserialize(SelectBlob(statistics, gameMode));

    /// <summary>Returns the property name of the blob a game mode is stored in.</summary>
    /// <param name="gameMode">Game mode index, as carried by the wire format.</param>
    public static string BlobNameForMode(int gameMode) => gameMode switch
    {
        0 => nameof(CharacterStatistics.DeathmatchStatistics),
        1 => nameof(CharacterStatistics.TeamDeathmatchStatistics),
        2 => nameof(CharacterStatistics.SneakingStatistics),
        3 => nameof(CharacterStatistics.CaptureStatistics),
        4 => nameof(CharacterStatistics.BaseStatistics),
        5 => nameof(CharacterStatistics.BombStatistics),
        6 => nameof(CharacterStatistics.RescueStatistics),
        7 => nameof(CharacterStatistics.RaceStatistics),
        8 => nameof(CharacterStatistics.TeamSneakingStatistics),
        9 => nameof(CharacterStatistics.SdmStatistics),
        10 => nameof(CharacterStatistics.ScapStatistics),
        _ => nameof(CharacterStatistics.DeathmatchStatistics),
    };

    /// <summary>Returns the serialized blob of a character for a game mode.</summary>
    /// <param name="statistics">Statistics row of the character.</param>
    /// <param name="gameMode">Game mode index, as carried by the wire format.</param>
    public static string? SelectBlob(CharacterStatistics statistics, int gameMode) => gameMode switch
    {
        0 => statistics.DeathmatchStatistics,
        1 => statistics.TeamDeathmatchStatistics,
        2 => statistics.SneakingStatistics,
        3 => statistics.CaptureStatistics,
        4 => statistics.BaseStatistics,
        5 => statistics.BombStatistics,
        6 => statistics.RescueStatistics,
        7 => statistics.RaceStatistics,
        8 => statistics.TeamSneakingStatistics,
        9 => statistics.SdmStatistics,
        10 => statistics.ScapStatistics,
        _ => statistics.DeathmatchStatistics,
    };

    /// <summary>Stores a serialized blob on the matching property of a statistics row.</summary>
    /// <param name="statistics">Statistics row to update.</param>
    /// <param name="gameMode">Game mode index, as carried by the wire format.</param>
    /// <param name="json">Serialized blob.</param>
    public static void AssignBlob(CharacterStatistics statistics, int gameMode, string json)
    {
        switch (gameMode)
        {
            case 1:
                statistics.TeamDeathmatchStatistics = json;
                break;
            case 2:
                statistics.SneakingStatistics = json;
                break;
            case 3:
                statistics.CaptureStatistics = json;
                break;
            case 4:
                statistics.BaseStatistics = json;
                break;
            case 5:
                statistics.BombStatistics = json;
                break;
            case 6:
                statistics.RescueStatistics = json;
                break;
            case 7:
                statistics.RaceStatistics = json;
                break;
            case 8:
                statistics.TeamSneakingStatistics = json;
                break;
            case 9:
                statistics.SdmStatistics = json;
                break;
            case 10:
                statistics.ScapStatistics = json;
                break;
            default:
                statistics.DeathmatchStatistics = json;
                break;
        }
    }
}
