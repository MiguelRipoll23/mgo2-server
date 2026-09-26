using Mgo2Server.Infrastructure.DependencyInjection;
using Mgo2Server.GameLobbyServer;
using Mgo2Server.GameLobbyServer.Commands;
using Mgo2Server.GameLobbyServer.Coordination;
using Mgo2Server.GameLobbyServer.Maintenance;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Domain.News;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Telemetry;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddServerLogging(builder.Configuration);

builder.Services.AddServerServices(builder.Configuration);
builder.Services.AddServerTelemetry(builder.Configuration, "mgo2-game-lobby");
builder.Services.AddCommandHandlers();

// The coordination stream of this lobby. It both reports the presence of this
// lobby to the HTTP API and receives the flash news the API relays, and it is
// the publisher the lobby tracker reports a player through. Replacing rather
// than adding keeps the tracker and the stream on the same instance.
builder.Services.AddSingleton<FlashNewsService>();
builder.Services.AddSingleton<LobbyIdentityService>();
builder.Services.AddSingleton<FakePlayerRequestHandlerService>();
builder.Services.AddSingleton<FakeTeamRequestHandlerService>();
builder.Services.AddSingleton<FakeTeamStateRequestHandlerService>();
builder.Services.AddSingleton<FakeTeamQueryRequestHandlerService>();
builder.Services.AddSingleton<FakeTeamPairingRequestHandlerService>();
builder.Services.AddSingleton<FakeTeamMemberStateRequestHandlerService>();
builder.Services.AddSingleton<FakeHostRoomRequestHandlerService>();
builder.Services.AddSingleton<LobbyEventQueueService>();
builder.Services.AddSingleton<LobbyCommandApplyService>();
builder.Services.AddSingleton<LobbyCoordinationClientService>();
builder.Services.Replace(ServiceDescriptor.Singleton<ILobbyPresencePublisher>(
    provider => provider.GetRequiredService<LobbyCoordinationClientService>()));

builder.Services.AddSingleton<LobbyHeartbeatService>();
builder.Services.AddSingleton<LobbyCleanupService>();
builder.Services.AddSingleton<GameCleanupService>();
builder.Services.AddSingleton<AutomatchTickerService>();
builder.Services.AddSingleton<CharacterPresenceTickerService>();
builder.Services.AddSingleton<EventOutcomeTickerService>();
builder.Services.AddSingleton<EventSessionCleanupService>();
builder.Services.AddSingleton<EventAssignmentTickerService>();
builder.Services.AddSingleton<CharacterPresenceCleanupService>();
builder.Services.AddSingleton<GameLobbyServerRunner>();

var host = builder.Build();

var runner = host.Services.GetRequiredService<GameLobbyServerRunner>();
var lobbyOptions = host.Services.GetRequiredService<IOptions<LobbyOptions>>().Value;
var automatchOptions = host.Services.GetRequiredService<IOptions<AutomatchOptions>>().Value;
var eventOptions = host.Services.GetRequiredService<IOptions<EventOptions>>().Value;
var options = host.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GameLobbyServer");

lobbyOptions.Validate();

// Refused here rather than at first use, so a schedule that cannot be honoured
// stops the process instead of closing the feature in a way that looks deliberate.
automatchOptions.Validate();

// The same reasoning as the schedule above: a zone or reward table that cannot
// be honoured stops the process rather than advertising a wrong window or paying
// a prize that never applies.
eventOptions.Validate();

logger.LogInformation(
    "Automatching is {State} ({Schedule})",
    automatchOptions.Enabled ? "enabled" : "disabled",
    string.IsNullOrWhiteSpace(automatchOptions.Windows)
        ? "open all day"
        : $"{automatchOptions.Windows} in {automatchOptions.TimeZone}");

logger.LogInformation(
    "Starting gameplay lobby {LobbyName} ({Subtype}), announcing {AnnouncedIpAddress}",
    lobbyOptions.Name,
    lobbyOptions.Subtype,
    options.AnnouncedIpAddress);

using var cancellation = new CancellationTokenSource();

// Both stops reach the runner: the interrupt a person sends from a terminal, and
// the SIGTERM a rollout sends. The runner closes the listener on it and then waits
// for the players in this lobby to leave, which is the long one of the four.
using var stopSignals = ShutdownSignalUtils.OnStopRequested(cancellation.Cancel, logger);

try
{
    await runner.RunAsync(cancellation.Token);
}
catch (OperationCanceledException)
{
    // Normal shutdown.
}
finally
{
    await runner.StopAsync();
}
