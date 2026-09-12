using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Services;
using Mgo2Server.Shared.Domain.Authentication;

namespace Mgo2Server.Http.Endpoints;

/// <summary>
/// The endpoints the game client reaches without a token: the login, the policy
/// document, the version check, the data list and the patch files.
/// </summary>
internal static class PublicEndpoints
{
    /// <summary>Content type the login and the data list are answered with.</summary>
    private const string PlainTextContentType = "text/plain;charset=UTF-8";

    /// <summary>Maps the public endpoints.</summary>
    /// <param name="app">Application the endpoints are added to.</param>
    public static void MapPublicEndpoints(this WebApplication app)
    {
        app.MapPost("/Z4qIOLmQBOj4NQo0uHx3q0mE51Fe/", LoginAsync)
            .DisableAntiforgery()
            .WithTags("Game")
            .WithSummary("Login")
            .WithDescription("Authenticates a player and returns a session token");

        app.MapGet("/jp/mgo2/policy/policy.txt", GetPolicyAsync)
            .WithTags("Game")
            .WithSummary("Policy document")
            .WithDescription("Returns the terms of service policy document");

        // The client posts to /jp/mgo2//patch/checkver.html and
        // /jp/mgo2//patch//checkver.html; the path normaliser collapses the
        // repeated slashes before routing, so one route serves both.
        app.MapPost("/jp/mgo2/patch/checkver.html", CheckVersionAsync)
            .DisableAntiforgery()
            .WithTags("Game")
            .WithSummary("Check version")
            .WithDescription("Returns the result of the version check");

        app.MapPost("/jp/mgo2/data/datalist.html", GetDataListAsync)
            .DisableAntiforgery()
            .WithTags("Game")
            .WithSummary("Data list")
            .WithDescription("Game updates list");

        // The patch files are mirrored straight from the launcher, so they have
        // no request or response contract of their own and stay out of the API
        // reference; documenting them would only add an empty group.
        app.MapMethods("/files/{*path}", ["GET", "HEAD"], GetFileAsync)
            .ExcludeFromDescription();
    }

    private static async Task<IResult> LoginAsync(
        AuthenticationService authenticationService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var form = LoginForm.From(await ReadFormAsync(context, cancellationToken));
        var issues = RequestBodyValidation.Validate(form);
        if (issues.Count > 0)
        {
            return RequestBodyValidation.Reject([.. issues]);
        }

        var response = await authenticationService.LoginAsync(form.Name!, form.Passwd!, cancellationToken);

        // The login answers with a plain-text body, whatever the documented
        // description of the route says.
        return Results.Text(response, PlainTextContentType);
    }

    private static async Task<IResult> GetDataListAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var form = DataListForm.From(await ReadFormAsync(context, cancellationToken));
        var issues = RequestBodyValidation.Validate(form);
        return issues.Count > 0
            ? RequestBodyValidation.Reject([.. issues])
            : Results.Text(string.Empty, PlainTextContentType);
    }

    private static async Task CheckVersionAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var form = CheckVersionForm.From(await ReadFormAsync(context, cancellationToken));
        var issues = RequestBodyValidation.Validate(form);
        if (issues.Count > 0)
        {
            await RequestBodyValidation.Reject([.. issues]).ExecuteAsync(context);
            return;
        }

        await VersionService.WriteCheckVersionResponseAsync(context, cancellationToken);
    }

    /// <summary>
    /// Reads the posted form. A request that carries no form at all is reported
    /// as an empty form, so the field rules answer it with the same rejection as
    /// a form that is missing its fields.
    /// </summary>
    /// <param name="context">Request being handled.</param>
    /// <param name="cancellationToken">Token that cancels the read.</param>
    private static async Task<IFormCollection> ReadFormAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return new FormCollection([]);
        }
    }

    private static async Task<IResult> GetPolicyAsync(
        PolicyService policyService,
        CancellationToken cancellationToken)
    {
        var body = await policyService.GetPolicyAsync(cancellationToken);
        return Results.Text(body, "text/html; charset=utf-8");
    }

    private static Task<IResult> GetFileAsync(
        FileService fileService,
        HttpContext context,
        string path,
        CancellationToken cancellationToken) =>
        fileService.GetFileAsync(
            path,
            HttpMethods.IsHead(context.Request.Method),
            context.Request.Headers.Range.Count > 0 ? context.Request.Headers.Range.ToString() : null,
            cancellationToken);
}
