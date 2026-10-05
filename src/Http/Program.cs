using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Discord;
using Mgo2Server.Http.Endpoints;
using Mgo2Server.Http.Endpoints.Public;
using Mgo2Server.Http.Errors;
using Mgo2Server.Http.Middleware;
using Mgo2Server.Http.Options;
using Mgo2Server.Http.Services;
using Mgo2Server.Infrastructure.DependencyInjection;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.FileProviders;
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

var httpApiOptions = new HttpApiOptions();

var discordOptions = new DiscordOptions
{
    Enabled = bool.TryParse(builder.Configuration["DISCORD_ENABLED"], out var discordEnabled) && discordEnabled,
    BotToken = builder.Configuration["DISCORD_BOT_TOKEN"] ?? string.Empty,
    GatewayUrl = builder.Configuration["DISCORD_GATEWAY_URL"] is { Length: > 0 } gatewayUrl
        ? gatewayUrl
        : DiscordOptions.DefaultGatewayUrl,
    GuildIdentifier = builder.Configuration["DISCORD_GUILD_ID"] ?? string.Empty,
    PlayerCountChannelIdentifier = builder.Configuration["DISCORD_PLAYER_COUNT_CHANNEL_ID"] ?? string.Empty,
    LoginsChannelIdentifier = builder.Configuration["DISCORD_LOGINS_CHANNEL_ID"] ?? string.Empty,
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
builder.Services.AddSingleton<DiscordLoginNotificationService>();
builder.Services.AddSingleton<DiscordChannelRenameService>();
builder.Services.AddSingleton<DiscordEventScheduleCommandService>();
builder.Services.AddSingleton<DiscordCommandService>();
builder.Services.AddSingleton<DiscordGatewayClientService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<DiscordGatewayClientService>());
builder.Services.AddHostedService<DiscordStartupService>();

// The health route is read by a page deployed outside this service, so it is
// answered under an allow-all policy: it carries nothing of the caller's, and a
// fixed origin list would only break the next deployment of that page. Every
// other route is same-origin and stays outside the policy.
builder.Services.AddCors(options => options.AddPolicy(
    HealthEndpoints.CorsPolicyName,
    policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

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
                "Provides the endpoints a game client needs: player authentication, " +
                "account management, rankings, patch distribution and server policy. " +
                "The dashboards and the moderator tooling read the same database from " +
                "outside this service, so no administrative surface is published here.",
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
app.UseCors();

// The patch files ship inside the image and are never fetched from an upstream
// launcher, so they are served straight out of the static directory rather than
// through an endpoint of their own.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(httpApiOptions.LocalLauncherPath),
    RequestPath = "/files",
});

app.MapOpenApi("/.well-known/openapi");
app.MapOpenApi("/.well-known/openapi.json");

// The API reference is the root of the service: the routes below it are all the
// paths a game client calls, and none of them answers a bare GET, so the root is
// free to carry the reference. The tags the reference used to be grouped by are
// gone from the routes, so it lists them as one flat set.
app.MapScalarApiReference("/", reference =>
{
    reference
        .WithTitle("MGO2 HTTP API")
        .WithOpenApiRoutePattern("/.well-known/openapi");
});

app.MapPublicEndpoints();
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
