using Grpc.Core;
using Grpc.Net.Client;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Chat;

/// <summary>
/// Reaches the gameplay host of a room to pause or resume its bots.
/// </summary>
/// <remarks>
/// <para>
/// <b>The room row names the host, and the host row names the endpoint.</b> The
/// lobby does not keep a second map of gameplay containers: the game's
/// <c>host_id</c> is the gameplay character, and the endpoint that character
/// registered is the same public/private pair a joining client is handed. The
/// control listener runs on the same port number as the host's UDP socket, over
/// TCP, so no second port has to be registered.
/// </para>
/// <para>
/// Both halves of the endpoint are tried: a lobby inside the same network can
/// use the private address even when the public one needs NAT hairpinning. The
/// call is bounded so a chat command never waits on a host that is gone.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class GameplayControlClientService(
    ILogger<GameplayControlClientService> logger)
{
    /// <summary>How long one room's host is given to answer.</summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Asks the room's host to pause or resume the bots.
    /// </summary>
    /// <param name="endpoint">The gameplay host's registered endpoint pair.</param>
    /// <param name="gameIdentifier">Room the command is for.</param>
    /// <param name="paused">Whether the bots should hold their fire.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>Whether the host applied the change, and a line describing it.</returns>
    public async Task<BotPauseResult> SetBotsPausedAsync(
        ConnectionInformation endpoint,
        int gameIdentifier,
        bool paused,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        foreach (var candidate in Endpoints(endpoint))
        {
            try
            {
                using var channel = GrpcChannel.ForAddress(
                    $"http://{candidate.Address}:{candidate.Port}");
                var client = new GameplayControl.GameplayControlClient(channel);
                var response = await client.SetBotsPausedAsync(
                    new SetBotsPausedRequest
                    {
                        GameIdentifier = gameIdentifier,
                        Paused = paused,
                    },
                    deadline: DateTime.UtcNow.Add(Deadline),
                    cancellationToken: cancellationToken);

                if (response.Applied)
                {
                    logger.LogDebug(
                        "Room {GameIdentifier}: gameplay host {Address}:{Port} applied the bot pause",
                        gameIdentifier,
                        candidate.Address,
                        candidate.Port);
                    return new BotPauseResult(true, response.Detail);
                }

                logger.LogDebug(
                    "Room {GameIdentifier}: gameplay host {Address}:{Port} refused the bot pause: {Detail}",
                    gameIdentifier,
                    candidate.Address,
                    candidate.Port,
                    response.Detail);
            }
            catch (RpcException exception)
            {
                logger.LogDebug(
                    exception,
                    "Room {GameIdentifier}: gameplay host {Address}:{Port} did not answer the bot pause",
                    gameIdentifier,
                    candidate.Address,
                    candidate.Port);
            }
        }

        return new BotPauseResult(false, "The gameplay server did not answer.");
    }

    /// <summary>
    /// The endpoint pairs a room's host registered, public first.
    /// </summary>
    /// <param name="endpoint">Registered endpoint pair.</param>
    /// <returns>The address and port pairs, without duplicates.</returns>
    private static IEnumerable<(string Address, int Port)> Endpoints(ConnectionInformation endpoint)
    {
        var seen = new HashSet<(string, int)>();

        if (!string.IsNullOrWhiteSpace(endpoint.PublicIpAddress) && endpoint.PublicPort > 0)
        {
            var publicPair = (endpoint.PublicIpAddress, endpoint.PublicPort);
            seen.Add(publicPair);
            yield return publicPair;
        }

        if (!string.IsNullOrWhiteSpace(endpoint.PrivateIpAddress)
            && endpoint.PrivatePort > 0
            && seen.Add((endpoint.PrivateIpAddress, endpoint.PrivatePort)))
        {
            yield return (endpoint.PrivateIpAddress, endpoint.PrivatePort);
        }
    }
}

/// <summary>Outcome of one bot pause request to a gameplay room.</summary>
/// <param name="Applied">Whether the host applied the change.</param>
/// <param name="Detail">The host's line, or why none arrived.</param>
public readonly record struct BotPauseResult(bool Applied, string Detail);
