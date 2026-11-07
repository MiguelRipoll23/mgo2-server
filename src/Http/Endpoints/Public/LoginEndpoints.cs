using Microsoft.Extensions.Logging;
using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Discord;
using Mgo2Server.Shared.Domain.Authentication;
using Mgo2Server.Shared.Domain.Games;

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
        GameService gameService,
        DiscordLoginNotificationService loginNotifications,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var form = LoginForm.From(await RequestBodyValidation.ReadFormAsync(context, cancellationToken));
        var issues = RequestBodyValidation.Validate(form);
        if (issues.Count > 0)
        {
            // The rejection body answers the client; the log is how the reason is
            // read afterwards, because the client only reports that it was refused.
            loggerFactory.CreateLogger(nameof(LoginEndpoints)).LogWarning(
                "Login request from {RemoteAddress} rejected: {Issues}",
                context.Connection.RemoteIpAddress,
                string.Join("; ", issues));

            return RequestBodyValidation.Reject([.. issues]);
        }

        var attempt = await authenticationService.LoginAsync(form.Name!, form.Passwd!, cancellationToken);

        // The address the client states here is the only place a player's own
        // external address is learned: the connection push that follows reports the
        // endpoint the server socket sees, which is the tailnet or LAN address and
        // not the one other players dial. The rows written are the account's
        // character connections, and a player that states none, or states a tailnet
        // address, leaves every row as it was.
        if (attempt.AccountIdentifier is { } accountIdentifier)
        {
            var written = await gameService.UpdatePublicAddressAsync(
                accountIdentifier,
                form.IpAddress,
                cancellationToken);
            if (written > 0)
            {
                loggerFactory.CreateLogger(nameof(LoginEndpoints)).LogInformation(
                    "Account {AccountIdentifier}: public address set on {RowCount} connection row(s)",
                    accountIdentifier,
                    written);
            }
        }

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
