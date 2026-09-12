using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>A news article delivered to the gate server and the lobby bulletin.</summary>
[Table("news")]
public sealed class NewsArticle
{
    /// <summary>Identifier of the article.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Whether the article is flagged as important.</summary>
    [Column("important")]
    public bool Important { get; set; }

    /// <summary>Unix timestamp of the article.</summary>
    [Column("time")]
    public int Time { get; set; }

    /// <summary>Topic line of the article.</summary>
    [Column("topic")]
    [MaxLength(128)]
    public required string Topic { get; set; }

    /// <summary>Body of the article.</summary>
    [Column("message")]
    public required string Message { get; set; }
}
