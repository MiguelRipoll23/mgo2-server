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

    /// <summary>Timestamp with time zone of the article.</summary>
    [Column("time")]
    public DateTimeOffset Time { get; set; }

    /// <summary>Title of the article.</summary>
    [Column("title")]
    [MaxLength(128)]
    public required string Title { get; set; }

    /// <summary>Body of the article.</summary>
    [Column("body")]
    public required string Body { get; set; }
}
