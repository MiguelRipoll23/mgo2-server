using Microsoft.AspNetCore.Http;

namespace Mgo2Server.Http.Contracts;

/// <summary>Reply of a request whose body does not hold the documented shape.</summary>
/// <param name="Success">Always <c>false</c>.</param>
/// <param name="Error">Description of what did not hold.</param>
public sealed record ValidationFailureResponse(bool Success, ValidationError Error);

/// <summary>Description of a rejected body.</summary>
/// <param name="Name">Name of the error.</param>
/// <param name="Message">Fields that did not hold, separated by semicolons.</param>
public sealed record ValidationError(string Name, string Message);

/// <summary>Fields the client posts to the data list endpoint.</summary>
public sealed class DataListForm
{
    /// <summary>Server group the client asks about.</summary>
    public string? ServerGroupIdentifier { get; set; }

    /// <summary>Language code of the client.</summary>
    public string? Language { get; set; }

    /// <summary>Identifier of the player, when the client knows it.</summary>
    public string? PlayerIdentifier { get; set; }

    /// <summary>Reads the form the client posted.</summary>
    /// <param name="form">Form of the request, empty when none was posted.</param>
    public static DataListForm From(IFormCollection form) => new()
    {
        ServerGroupIdentifier = LoginForm.Read(form, "svrgid"),
        Language = LoginForm.Read(form, "lang"),
        PlayerIdentifier = LoginForm.Read(form, "pid"),
    };
}

/// <summary>Fields the client posts to the version check endpoint.</summary>
public sealed class CheckVersionForm
{
    /// <summary>Comma-separated version, product and seed of the client.</summary>
    public string? Parameters { get; set; }

    /// <summary>Reads the form the client posted.</summary>
    /// <param name="form">Form of the request, empty when none was posted.</param>
    public static CheckVersionForm From(IFormCollection form) => new()
    {
        Parameters = LoginForm.Read(form, "p"),
    };
}
