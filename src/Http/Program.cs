using Mgo2Server.Http.Authentication;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Discord;
using Mgo2Server.Http.Endpoints;
using Mgo2Server.Http.Endpoints.Authenticated;
using Mgo2Server.Http.Endpoints.Public;
using Mgo2Server.Http.Errors;
using Mgo2Server.Http.Middleware;
using Mgo2Server.Http.Options;
using Mgo2Server.Http.Services;
using Mgo2Server.Infrastructure.DependencyInjection;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Telemetry;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddServerLogging(builder.Configuration);

// The API issues no Data Protection payloads, so the framework's advisory that
// the Linux key ring is unencrypted is noise. The floor stays at Error, so a
// genuine key-ring failure is still reported.
builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.Error);

var httpPort = int.TryParse(builder.Configuration["HTTP_PORT"], out var configuredPort)
    ? configuredPort
    : PortConstants.HttpPort;

var internalGrpcPort = int.TryParse(builder.Configuration["INTERNAL_GRPC_PORT"], out var configuredGrpcPort)
    ? configuredGrpcPort
    : PortConstants.InternalGrpcPort;

// Bind Kestrel directly. The image clears the base image's inherited
// ASPNETCORE_HTTP_PORTS, so no server URL is generated for the host to override
// and the app owns the binding outright.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(httpPort);

    // The coordination endpoint speaks HTTP/2 without TLS. It is reached by the
    // gameplay lobbies over the internal network of the deployment and never by
    // a game client, so it is a port of its own rather than a scheme of the
    // public one, which the console's HTTP client cannot speak over HTTP/2.
    options.ListenAnyIP(
        internalGrpcPort,
        listen => listen.Protocols = HttpProtocols.Http2);
});

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

var discordOptions = new DiscordOptions
{
    Enabled = bool.TryParse(builder.Configuration["DISCORD_ENABLED"], out var discordEnabled) && discordEnabled,
    BotToken = builder.Configuration["DISCORD_BOT_TOKEN"] ?? string.Empty,
    GatewayUrl = builder.Configuration["DISCORD_GATEWAY_URL"] is { Length: > 0 } gatewayUrl
        ? gatewayUrl
        : DiscordOptions.DefaultGatewayUrl,
    GuildIdentifier = builder.Configuration["DISCORD_GUILD_ID"] ?? string.Empty,
    PlayerCountChannelIdentifier = builder.Configuration["DISCORD_PLAYER_COUNT_CHANNEL_ID"] ?? string.Empty,
    ModeratorRoleIdentifier = builder.Configuration["DISCORD_MODERATOR_ROLE_ID"] ?? string.Empty,
    ManagerRoleIdentifier = builder.Configuration["DISCORD_MANAGER_ROLE_ID"] ?? string.Empty,
};

builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(httpApiOptions));
builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(discordOptions));
builder.Services.AddServerServices(builder.Configuration);
builder.Services.AddServerTelemetry(builder.Configuration, "mgo2-http");

builder.Services.AddHttpClient();
builder.Services.AddSingleton<PolicyService>();
builder.Services.AddSingleton<HelpService>();
builder.Services.AddSingleton<VersionService>();
builder.Services.AddSingleton<LauncherService>();
builder.Services.AddSingleton<RankingResponseService>();

// The coordination the HTTP API owns: the streams of the gameplay lobbies, the
// global player count they feed and the flash news that is relayed back down
// the same streams.
builder.Services.AddGrpc();
builder.Services.AddSingleton<LobbyPresenceService>();
builder.Services.AddSingleton<LobbyConnectionRegistryService>();
builder.Services.AddSingleton<PlayerPresenceNotificationService>();
builder.Services.AddSingleton<FlashNewsDispatcherService>();

// Discord is optional and only ever observes and relays: it is switched off by
// configuration, and every failure of it is logged and absorbed by the service
// that made the call. Slash commands arrive over the gateway socket; the
// command registration and the answers to Discord are the REST side of it.
builder.Services.AddSingleton<DiscordRestClientService>();
builder.Services.AddSingleton<IDiscordMessageService>(
    provider => provider.GetRequiredService<DiscordRestClientService>());
builder.Services.AddSingleton<IDiscordInteractionResponder>(
    provider => provider.GetRequiredService<DiscordRestClientService>());
builder.Services.AddSingleton<DiscordPlayerCountService>();
builder.Services.AddSingleton<IPlayerPresenceObserver>(
    provider => provider.GetRequiredService<DiscordPlayerCountService>());
builder.Services.AddSingleton<DiscordCommandService>();
builder.Services.AddSingleton<DiscordGatewayClientService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<DiscordGatewayClientService>());
builder.Services.AddHostedService<DiscordStartupService>();

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
            Description = "HTTP API for the Metal Gear Online 2 private server. " +
                "Provides endpoints for player authentication, account management, " +
                "lobby and game session coordination, news and flash news broadcasts, " +
                "and server policy distribution.",
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

// The API reference lives below the API itself, leaving the root for the
// registration page served from the static directory.
app.MapScalarApiReference("/api", reference =>
{
    reference
        .WithTitle("MGO2 HTTP API")
        .WithOpenApiRoutePattern("/.well-known/openapi")
        .ExpandAllTags();
});

app.MapPublicEndpoints();
app.MapAuthenticatedEndpoints();
app.MapGrpcService<LobbyCoordinationGrpcService>();

// The API is where accounts are created, so it publishes the account total. The
// provider is built here because the API is the only entry point that starts
// its host; the console servers activate it explicitly.
app.Services.ActivateServerTelemetry();
await app.Services.GetRequiredService<ServerMetricsService>().ReportTotalUsersAsync();

app.Logger.LogInformation(
    "HTTP API listening on port {Port}, coordinating the game lobbies on port {GrpcPort}",
    httpPort,
    internalGrpcPort);

await app.RunAsync();
