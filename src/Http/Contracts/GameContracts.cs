using System.ComponentModel.DataAnnotations;

namespace Mgo2Server.Http.Contracts;

/// <summary>One game room as the API publishes it.</summary>
/// <param name="Id">Identifier of the room.</param>
/// <param name="HostId">Character hosting the room.</param>
/// <param name="LobbyId">Lobby the room belongs to.</param>
/// <param name="Name">Display name of the room.</param>
/// <param name="Password">Join password of the room.</param>
/// <param name="Comment">Comment shown in the room list.</param>
/// <param name="MaxPlayers">Maximum number of players.</param>
/// <param name="CurrentGame">Index of the selected game in the rotation.</param>
/// <param name="Games">Serialized game rotation.</param>
/// <param name="Stance">Stance the host allows.</param>
/// <param name="Ping">Reported ping of the hosting client.</param>
/// <param name="Common">Serialized common settings block.</param>
/// <param name="Rules">Serialized rule settings block.</param>
/// <param name="Status">Status of the room.</param>
/// <param name="CreatedAt">Creation timestamp, in round-trip format.</param>
public sealed record GameContract(
    int Id,
    int HostId,
    int LobbyId,
    string Name,
    string Password,
    string Comment,
    int MaxPlayers,
    int CurrentGame,
    string Games,
    int Stance,
    int Ping,
    string Common,
    string Rules,
    int Status,
    string? CreatedAt);

/// <summary>Fields accepted when a room is created.</summary>
public sealed class GameRequest
{
    /// <summary>Character hosting the room.</summary>
    [Range(1, int.MaxValue)]
    public int HostId { get; set; }

    /// <summary>Lobby the room belongs to.</summary>
    [Range(1, int.MaxValue)]
    public int LobbyId { get; set; }

    /// <summary>Display name of the room.</summary>
    [Required]
    [StringLength(16, MinimumLength = 1)]
    public required string Name { get; set; }

    /// <summary>Join password of the room.</summary>
    [StringLength(15)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Comment shown in the room list.</summary>
    [StringLength(128)]
    public string Comment { get; set; } = string.Empty;

    /// <summary>Maximum number of players.</summary>
    [Range(1, 8)]
    public int MaxPlayers { get; set; } = 8;

    /// <summary>Index of the selected game in the rotation.</summary>
    public int CurrentGame { get; set; }

    /// <summary>Serialized game rotation.</summary>
    public string Games { get; set; } = "[]";

    /// <summary>Stance the host allows.</summary>
    public int Stance { get; set; }

    /// <summary>Reported ping of the hosting client.</summary>
    public int Ping { get; set; }

    /// <summary>Serialized common settings block.</summary>
    public string Common { get; set; } = "{}";

    /// <summary>Serialized rule settings block.</summary>
    public string Rules { get; set; } = "{}";

    /// <summary>Status of the room.</summary>
    public int Status { get; set; }
}

/// <summary>Fields accepted when a room is partly updated.</summary>
public sealed class GamePatchRequest
{
    /// <summary>Character hosting the room.</summary>
    [Range(1, int.MaxValue)]
    public int? HostId { get; set; }

    /// <summary>Lobby the room belongs to.</summary>
    [Range(1, int.MaxValue)]
    public int? LobbyId { get; set; }

    /// <summary>Display name of the room.</summary>
    [StringLength(16, MinimumLength = 1)]
    public string? Name { get; set; }

    /// <summary>Join password of the room.</summary>
    [StringLength(15)]
    public string? Password { get; set; }

    /// <summary>Comment shown in the room list.</summary>
    [StringLength(128)]
    public string? Comment { get; set; }

    /// <summary>Maximum number of players.</summary>
    [Range(1, 8)]
    public int? MaxPlayers { get; set; }

    /// <summary>Index of the selected game in the rotation.</summary>
    public int? CurrentGame { get; set; }

    /// <summary>Serialized game rotation.</summary>
    public string? Games { get; set; }

    /// <summary>Stance the host allows.</summary>
    public int? Stance { get; set; }

    /// <summary>Reported ping of the hosting client.</summary>
    public int? Ping { get; set; }

    /// <summary>Serialized common settings block.</summary>
    public string? Common { get; set; }

    /// <summary>Serialized rule settings block.</summary>
    public string? Rules { get; set; }

    /// <summary>Status of the room.</summary>
    public int? Status { get; set; }
}
