using System.ComponentModel.DataAnnotations;

namespace Mgo2Server.Http.Contracts;

/// <summary>One news article as the API publishes it.</summary>
/// <param name="Id">Identifier of the article.</param>
/// <param name="Important">Whether the article is highlighted.</param>
/// <param name="Time">Timestamp of the article.</param>
/// <param name="Title">Title of the article.</param>
/// <param name="Body">Body of the article.</param>
public sealed record NewsItemContract(int Id, bool Important, DateTimeOffset Time, string Title, string Body);

/// <summary>Fields accepted when an article is created.</summary>
public sealed class NewsRequest
{
    /// <summary>Whether the article is highlighted.</summary>
    public bool Important { get; set; }

    /// <summary>Timestamp of the article.</summary>
    public DateTimeOffset Time { get; set; }

    /// <summary>Title of the article.</summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public required string Title { get; set; }

    /// <summary>Body of the article.</summary>
    [Required]
    [MinLength(1)]
    public required string Body { get; set; }
}

/// <summary>Fields accepted when an article is partly updated.</summary>
public sealed class NewsPatchRequest
{
    /// <summary>Whether the article is highlighted.</summary>
    public bool? Important { get; set; }

    /// <summary>Timestamp of the article.</summary>
    public DateTimeOffset? Time { get; set; }

    /// <summary>Title of the article.</summary>
    [StringLength(128, MinimumLength = 1)]
    public string? Title { get; set; }

    /// <summary>Body of the article.</summary>
    [MinLength(1)]
    public string? Body { get; set; }
}
