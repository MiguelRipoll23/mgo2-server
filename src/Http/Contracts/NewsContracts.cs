using System.ComponentModel.DataAnnotations;

namespace Mgo2Server.Http.Contracts;

/// <summary>One news article as the API publishes it.</summary>
/// <param name="Id">Identifier of the article.</param>
/// <param name="Important">Whether the article is highlighted.</param>
/// <param name="Time">Unix timestamp of the article.</param>
/// <param name="Topic">Topic line of the article.</param>
/// <param name="Message">Body of the article.</param>
public sealed record NewsItemContract(int Id, bool Important, int Time, string Topic, string Message);

/// <summary>Fields accepted when an article is created.</summary>
public sealed class NewsRequest
{
    /// <summary>Whether the article is highlighted.</summary>
    public bool Important { get; set; }

    /// <summary>Unix timestamp of the article.</summary>
    public int Time { get; set; }

    /// <summary>Topic line of the article.</summary>
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public required string Topic { get; set; }

    /// <summary>Body of the article.</summary>
    [Required]
    [MinLength(1)]
    public required string Message { get; set; }
}

/// <summary>Fields accepted when an article is partly updated.</summary>
public sealed class NewsPatchRequest
{
    /// <summary>Whether the article is highlighted.</summary>
    public bool? Important { get; set; }

    /// <summary>Unix timestamp of the article.</summary>
    public int? Time { get; set; }

    /// <summary>Topic line of the article.</summary>
    [StringLength(128, MinimumLength = 1)]
    public string? Topic { get; set; }

    /// <summary>Body of the article.</summary>
    [MinLength(1)]
    public string? Message { get; set; }
}
