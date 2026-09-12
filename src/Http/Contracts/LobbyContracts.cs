using System.ComponentModel.DataAnnotations;

namespace Mgo2Server.Http.Contracts;

/// <summary>One lobby as the API publishes it.</summary>
/// <param name="Id">Identifier of the lobby.</param>
/// <param name="TypeId">Kind of service the lobby describes.</param>
/// <param name="SubtypeId">Game type of the lobby.</param>
/// <param name="Name">Display name of the lobby.</param>
/// <param name="IpAddress">Address clients connect to.</param>
/// <param name="Port">Port clients connect to.</param>
/// <param name="PlayersCount">Number of players in the lobby.</param>
/// <param name="BeginnerOnly">Whether the lobby only accepts beginners.</param>
/// <param name="ExpansionOnly">Whether the lobby only accepts expansion owners.</param>
/// <param name="NoHeadshot">Whether the lobby disables headshots.</param>
/// <param name="ReplaysOnly">Whether the lobby only accepts replays.</param>
public sealed record LobbyContract(
    int Id,
    int TypeId,
    int SubtypeId,
    string Name,
    string IpAddress,
    int Port,
    int PlayersCount,
    bool BeginnerOnly,
    bool ExpansionOnly,
    bool NoHeadshot,
    bool ReplaysOnly);

/// <summary>Fields accepted when a lobby is created.</summary>
public sealed class LobbyRequest
{
    /// <summary>Kind of service the lobby describes.</summary>
    [Range(0, 2)]
    public int TypeId { get; set; }

    /// <summary>Game type of the lobby.</summary>
    public int SubtypeId { get; set; } = 1;

    /// <summary>Display name of the lobby.</summary>
    [Required]
    [StringLength(16, MinimumLength = 1)]
    public required string Name { get; set; }

    /// <summary>Address clients connect to.</summary>
    [Required]
    public required string IpAddress { get; set; }

    /// <summary>Port clients connect to.</summary>
    [Range(1, 65535)]
    public int Port { get; set; }

    /// <summary>Initial player count.</summary>
    [Range(0, int.MaxValue)]
    public int PlayersCount { get; set; }
}

/// <summary>Fields accepted when a lobby is partly updated.</summary>
public sealed class LobbyPatchRequest
{
    /// <summary>Kind of service the lobby describes.</summary>
    [Range(0, 2)]
    public int? TypeId { get; set; }

    /// <summary>Game type of the lobby.</summary>
    public int? SubtypeId { get; set; }

    /// <summary>Display name of the lobby.</summary>
    [StringLength(16, MinimumLength = 1)]
    public string? Name { get; set; }

    /// <summary>Address clients connect to.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Port clients connect to.</summary>
    [Range(1, 65535)]
    public int? Port { get; set; }

    /// <summary>Initial player count.</summary>
    [Range(0, int.MaxValue)]
    public int? PlayersCount { get; set; }
}
