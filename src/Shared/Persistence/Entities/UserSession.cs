using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>A login session issued to an account.</summary>
[Table("sessions")]
public sealed class UserSession
{
    /// <summary>Identifier of the session.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Account the session belongs to.</summary>
    [Column("account_id")]
    public int AccountIdentifier { get; set; }

    /// <summary>Login token issued to the client.</summary>
    [Column("token")]
    [MaxLength(32)]
    public required string Token { get; set; }
}
