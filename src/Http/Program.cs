using Mgo2Server.Http.Authentication;
using Mgo2Server.Http.Endpoints;
using Mgo2Server.Http.Errors;
using Mgo2Server.Http.Middleware;
using Mgo2Server.Http.Options;
using Mgo2Server.Http.Services;
using Mgo2Server.Infrastructure.DependencyInjection;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.News;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddServerLogging(builder.Configuration);

var httpPort = int.TryParse(builder.Configuration["HTTP_PORT"], out var configuredPort)
    ? configuredPort
    : PortConstants.HttpPort;

builder.WebHost.UseUrls($"http://0.0.0.0:{httpPort}");

var httpApiOptions = new HttpApiOptions
{
    LauncherServer = builder.Configuration["LAUNCHER_SERVER"] ?? "http://mgo2pc.com",
    JwtSecret = builder.Configuration["JWT_SECRET"] ?? string.Empty,
};

// The API refuses to start without the secret, because every authenticated
// route would otherwise be trivially forgeable.
if (string.IsNullOrEmpty(httpApiOptions.JwtSecret))
{
    throw new InvalidOperationException("JWT_SECRET environment variable is required");
}

builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(httpApiOptions));
builder.Services.AddServerServices(builder.Configuration);

builder.Services.AddHttpClient();
builder.Services.AddSingleton<PolicyService>();
builder.Services.AddSingleton<VersionService>();
builder.Services.AddSingleton<FileService>();
builder.Services.AddSingleton<FlashNewsService>();

// The API registers a single scheme, and a single registered scheme is also the
// default one, so the handler runs for the anonymous routes as well. It answers
// "no result" rather than a failure when a request carries no token, which is
// what keeps the public routes from reporting an authentication failure the way
// they did after the port; the protected groups, which require authorization,
// still turn a missing token into a 401.
builder.Services.AddAuthentication(BearerTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>(
        BearerTokenAuthenticationHandler.SchemeName,
        _ => { });

builder.Services.AddAuthorization();
builder.Services.AddOpenApi(options =>
{
    // The document carries the identity clients expect, so a generated client
    // describes the same API.
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "MGO2 HTTP API",
            Version = "1.0.0",
            Description = "MGO2 server HTTP API",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
        };

        return Task.CompletedTask;
    });
});
builder.Services.AddExceptionHandler<ServerExceptionHandler>();

// A body the endpoint cannot read is reported through the exception handler as
// well, so every rejected request carries the same envelope.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

var app = builder.Build();

app.UseExceptionHandler(_ => { });

// Runs before routing, because it rewrites the path the routes are matched on.
app.UseMiddleware<LegacyPathNormalizer>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi("/.well-known/openapi");
app.MapOpenApi("/.well-known/openapi.json");

// The API reference is served at the root of the API.
app.MapScalarApiReference("/", reference =>
{
    reference
        .WithTitle("MGO2 HTTP API")
        .WithOpenApiRoutePattern("/.well-known/openapi")
        .ExpandAllTags()
        .AddPreferredSecuritySchemes("bearer")
        .EnablePersistentAuthentication()
        .EnableDarkMode();
});

app.MapPublicEndpoints();

var authenticated = app.MapGroup("/");
authenticated.MapNewsEndpoints();
authenticated.MapLobbyEndpoints();
authenticated.MapGameEndpoints();
authenticated.MapFlashNewsEndpoints();

app.Logger.LogInformation("HTTP API listening on port {Port}", httpPort);

await app.RunAsync();
