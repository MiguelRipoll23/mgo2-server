using System.Text.RegularExpressions;
using Mgo2Server.Http.Contracts;

namespace Mgo2Server.Http.Endpoints;

/// <summary>
/// Checks the public request bodies that have to be refused before their handler
/// runs. Every rejected body carries the same envelope, naming the failed rule
/// and the fields it applies to.
/// </summary>
internal static partial class RequestBodyValidation
{
    /// <summary>Name reported for a rejected body.</summary>
    private const string ValidationErrorName = "ValidationError";

    /// <summary>Login name of an account is at most this many characters.</summary>
    private const int LoginNameMaximumLength = 15;

    /// <summary>Password hash the client posts is exactly this many characters.</summary>
    private const int PasswordHashLength = 32;

    /// <summary>Display name of an account is between these lengths.</summary>
    private const int DisplayNameMinimumLength = 3;
    private const int DisplayNameMaximumLength = 32;

    /// <summary>Password of an account is between these lengths.</summary>
    private const int PasswordMinimumLength = 6;
    private const int PasswordMaximumLength = 128;

    [GeneratedRegex("^[a-zA-Z0-9_-]+$")]
    private static partial Regex DisplayNamePattern { get; }

    /// <summary>Builds the reply of a body the endpoint refuses.</summary>
    /// <param name="issues">Descriptions of the fields that did not hold.</param>
    public static IResult Reject(params string[] issues) =>
        Results.Json(
            new ValidationFailureResponse(false, new ValidationError(ValidationErrorName, string.Join("; ", issues))),
            statusCode: StatusCodes.Status400BadRequest);

    /// <summary>Checks the login form.</summary>
    /// <param name="form">Form the client posted.</param>
    /// <returns>The issues found, or an empty list when the form is valid.</returns>
    public static List<string> Validate(LoginForm form)
    {
        List<string> issues = [];

        Require(form.Name, "name", issues);
        Require(form.Passwd, "passwd", issues);
        Require(form.Product, "product", issues);
        Require(form.Lang, "lang", issues);
        Require(form.Tz, "tz", issues);
        Require(form.Disk, "disk", issues);
        Require(form.Ps3, "ps3", issues);
        Require(form.Stime, "stime", issues);
        Require(form.Seed, "seed", issues);
        RequireNumber(form.Lang, "lang", issues);
        RequireNumber(form.Tz, "tz", issues);
        RequireNumber(form.Disk, "disk", issues);
        RequireNumber(form.Ps3, "ps3", issues);
        RequireNumber(form.Stime, "stime", issues);

        if (issues.Count == 0)
        {
            if (form.Name!.Length > LoginNameMaximumLength)
            {
                issues.Add($"name: at most {LoginNameMaximumLength} characters");
            }

            if (form.Passwd!.Length != PasswordHashLength)
            {
                issues.Add($"passwd: exactly {PasswordHashLength} characters");
            }
        }

        return issues;
    }

    /// <summary>Checks a registration request.</summary>
    /// <param name="request">Request the client posted.</param>
    /// <returns>The issues found, or an empty list when the request is valid.</returns>
    public static List<string> Validate(RegistrationRequest request)
    {
        List<string> issues = [];

        Require(request.DisplayName, "displayName", issues);
        Require(request.Password, "password", issues);

        if (issues.Count > 0)
        {
            return issues;
        }

        if (request.DisplayName.Length is < DisplayNameMinimumLength or > DisplayNameMaximumLength)
        {
            issues.Add($"displayName: between {DisplayNameMinimumLength} and {DisplayNameMaximumLength} characters");
        }
        else if (!DisplayNamePattern.IsMatch(request.DisplayName))
        {
            issues.Add("displayName: letters, digits, _ and - only");
        }

        if (request.Password.Length is < PasswordMinimumLength or > PasswordMaximumLength)
        {
            issues.Add($"password: between {PasswordMinimumLength} and {PasswordMaximumLength} characters");
        }

        return issues;
    }

    /// <summary>Checks the fields of a data list request.</summary>
    /// <param name="form">Form the client posted.</param>
    /// <returns>The issues found, or an empty list when the form is valid.</returns>
    public static List<string> Validate(DataListForm form)
    {
        List<string> issues = [];

        RequireNumber(form.ServerGroupIdentifier, "svrgid", issues);
        RequireNumber(form.Language, "lang", issues);
        RequireNumber(form.PlayerIdentifier, "pid", issues);

        return issues;
    }

    /// <summary>Checks the fields of a version check request.</summary>
    /// <param name="form">Form the client posted.</param>
    /// <returns>The issues found, or an empty list when the form is valid.</returns>
    public static List<string> Validate(CheckVersionForm form)
    {
        List<string> issues = [];

        Require(form.Parameters, "p", issues);

        return issues;
    }

    /// <summary>Reports a field the request did not carry.</summary>
    /// <param name="value">Value the client sent.</param>
    /// <param name="fieldName">Name of the field.</param>
    /// <param name="issues">Issues collected so far.</param>
    private static void Require(string? value, string fieldName, List<string> issues)
    {
        if (string.IsNullOrEmpty(value))
        {
            issues.Add($"{fieldName}: required");
        }
    }

    /// <summary>Reports a field that is missing or is not a number.</summary>
    /// <param name="value">Value the client sent.</param>
    /// <param name="fieldName">Name of the field.</param>
    /// <param name="issues">Issues collected so far.</param>
    private static void RequireNumber(string? value, string fieldName, List<string> issues)
    {
        if (string.IsNullOrEmpty(value))
        {
            issues.Add($"{fieldName}: required");
        }
        else if (!long.TryParse(value, out _))
        {
            issues.Add($"{fieldName}: expected a number");
        }
    }
}
