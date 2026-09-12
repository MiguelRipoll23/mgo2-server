using System.ComponentModel.DataAnnotations;

namespace Mgo2Server.Http.Contracts;

/// <summary>Fields of a registration request.</summary>
public sealed class RegistrationRequest
{
    /// <summary>Display name of the new account.</summary>
    [Required]
    [StringLength(32, MinimumLength = 3)]
    [RegularExpression("^[a-zA-Z0-9_\\-]+$", ErrorMessage = "Letters, digits, _ and - only")]
    public required string DisplayName { get; set; }

    /// <summary>Password of the new account, in clear text.</summary>
    [Required]
    [StringLength(128, MinimumLength = 6)]
    public required string Password { get; set; }
}

/// <summary>Account returned after a successful registration.</summary>
/// <param name="Id">Identifier of the account.</param>
/// <param name="DisplayName">Display name of the account.</param>
public sealed record RegistrationResponseContract(int Id, string DisplayName);
