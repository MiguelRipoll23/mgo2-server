using Grpc.Core;
using Mgo2Server.GameplayServer.Bots;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Grpc;

/// <summary>
/// Answers the lobby's request to pause or resume this room's bots.
/// </summary>
/// <remarks>
/// <b>The room identifier is the guard.</b> The control port is reachable by any
/// process that can reach the host, so the request is applied only when it names
/// the room this process is hosting. A lobby that read a stale room row, or a
/// request aimed at another gameplay server, changes nothing.
/// </remarks>
/// <param name="pause">The room's trigger gate.</param>
/// <param name="match">This process's room, which owns the identifier to check.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class BotPauseGrpcService(
    BotPauseService pause,
    MatchService match,
    ILogger<BotPauseGrpcService> logger) : GameplayControl.GameplayControlBase
{
    /// <inheritdoc />
    public override Task<SetBotsPausedResponse> SetBotsPaused(
        SetBotsPausedRequest request,
        ServerCallContext context)
    {
        var gameIdentifier = match.MatchIdentifier;
        if (gameIdentifier == 0 || request.GameIdentifier != gameIdentifier)
        {
            logger.LogWarning(
                "Refused a bot pause for room {RequestedGameIdentifier}: this host serves {GameIdentifier}",
                request.GameIdentifier,
                gameIdentifier);

            return Task.FromResult(new SetBotsPausedResponse
            {
                Applied = false,
                Paused = pause.IsPaused,
                Detail = "The room is not hosted here.",
            });
        }

        var changed = pause.SetPaused(request.Paused);
        if (changed)
        {
            logger.LogInformation(
                "Room {GameIdentifier}: bots are {BotState}",
                gameIdentifier,
                request.Paused ? "holding their fire" : "firing again");
        }

        return Task.FromResult(new SetBotsPausedResponse
        {
            Applied = true,
            Paused = pause.IsPaused,
            Detail = request.Paused ? "Bots paused." : "Bots resumed.",
        });
    }
}
