using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// The peer-to-peer endpoint a character's client listens on, registered by the
/// connection push and handed to joining clients in the join reply. The public
/// address is the one seen by the server socket; the private address is what
/// the client reported for its local network. One row per character.
/// </summary>
[Table("character_connections")]
public sealed class CharacterConnection
{
    /// <summary>Character the endpoint belongs to.</summary>
    [Key]
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Public address of the endpoint.</summary>
    [Column("public_ip")]
    [MaxLength(16)]
    public required string PublicIpAddress { get; set; }

    /// <summary>Public port of the endpoint.</summary>
    [Column("public_port")]
    public int PublicPort { get; set; }

    /// <summary>Private address of the endpoint.</summary>
    [Column("private_ip")]
    [MaxLength(16)]
    public required string PrivateIpAddress { get; set; }

    /// <summary>Private port of the endpoint.</summary>
    [Column("private_port")]
    public int PrivatePort { get; set; }

    /// <summary>Timestamp with time zone of the last update.</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Character the endpoint belongs to.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}
