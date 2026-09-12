using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Services;
using Mgo2Server.Shared.Domain.Authentication;

namespace Mgo2Server.Http.Endpoints;

/// <summary>The login endpoint.</summary>
internal static class LoginEndpoints
{
    /// <summary>Maps the login endpoint.</summary>
    /// <param name="app">Application the endpoint is added to.</param>
    public static void MapLoginEndpoints(this WebApplication app)
    {
        app.MapPost("/Z4qIOLmQBOj4NQo0uHx3q0mE51Fe/", LoginAsync)
            .DisableAntiforgery()
            .WithTags("Game")
            .WithSummary("Login")
            .WithDescription("Authenticates a player and returns a session token");
    }

    private static async Task<IResult> LoginAsync(
        AuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var form = LoginForm.From(await RequestBodyValidation.ReadFormAsync(context, cancellationToken));
        var issues = RequestBodyValidation.Validate(form);
        if (issues.Count > 0)
        {
            return RequestBodyValidation.Reject([.. issues]);
        }

        var response = await authenticationService.LoginAsync(form.Name!, form.Passwd!, cancellationToken);

        // The login answers with a plain-text body, whatever the documented
        // description of the route says.
        return Results.Text(response, RequestBodyValidation.PlainTextContentType);
    }
}
