using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Discord;
using Mgo2Server.Shared.Domain.Authentication;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>The login endpoint.</summary>
internal static class LoginEndpoints
{
    /// <summary>
    /// Header the client presents its host fingerprint in. It is the value the
    /// mgo2-plugin sends, and it is reported to the logins channel unchanged.
    /// </summary>
    private const string AuthHeaderName = "auth";

    /// <summary>Maps the login endpoint.</summary>
    /// <param name="group">Group the endpoint is added to.</param>
    public static void MapLoginEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/Z4qIOLmQBOj4NQo0uHx3q0mE51Fe/", LoginAsync)
            .DisableAntiforgery()
            .WithDescription("Authenticates a player and returns a session token");
    }

    private static async Task<IResult> LoginAsync(
        AuthenticationService authenticationService,
        DiscordLoginNotificationService loginNotifications,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var form = LoginForm.From(await RequestBodyValidation.ReadFormAsync(context, cancellationToken));
        var issues = RequestBodyValidation.Validate(form);
        if (issues.Count > 0)
        {
            return RequestBodyValidation.Reject([.. issues]);
        }

        var attempt = await authenticationService.LoginAsync(form.Name!, form.Passwd!, cancellationToken);

        // Every credential attempt is written in the logins channel, the ones
        // that failed included: a fingerprint that never reaches an account is
        // exactly what a spoofed client is read for.
        loginNotifications.LoginAttempted(
            form.Name!,
            context.Request.Headers[AuthHeaderName].ToString(),
            attempt.Succeeded);

        // The login answers with a plain-text body, whatever the documented
        // description of the route says.
        return Results.Text(attempt.Reply, RequestBodyValidation.PlainTextContentType);
    }
}
