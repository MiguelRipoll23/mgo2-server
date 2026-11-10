namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Takes the ticks of a room's bots.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists so the population does not depend on the decision stack.</b> A
/// bot's tick needs perception, which reads the lobby-slot arrays, and those
/// arrays name the population's own bots; a population that called the controller
/// itself would be part of a dependency cycle. The population hands its bots to
/// this service, and the service is the only thing that knows a tick exists.
/// </para>
/// <para>
/// It takes the bots as an argument rather than reading them, which is what keeps
/// it free of the population and testable without a room.
/// </para>
/// </remarks>
/// <param name="controller">Runs one bot's tick.</param>
public sealed class BotPlayService(BotController controller)
{
    /// <summary>
    /// Ticks every bot in slot order.
    /// </summary>
    /// <param name="bots">The room's bots.</param>
    /// <param name="matchIdentifier">The room they are in.</param>
    /// <param name="roundNumber">The round the room is on.</param>
    /// <param name="rule">The round's rule.</param>
    /// <param name="map">The round's map.</param>
    /// <param name="cancellationToken">Token that cancels the ticks.</param>
    public async Task PlayAsync(
        IReadOnlyList<BotParticipant> bots,
        int matchIdentifier,
        int roundNumber,
        int rule,
        int map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bots);

        foreach (var bot in bots.OrderBy(bot => bot.SessionSlot))
        {
            await controller.TickAsync(
                bot,
                matchIdentifier,
                roundNumber,
                rule,
                map,
                cancellationToken);
        }
    }

    /// <summary>Drops the decision stack's state, because the room has emptied.</summary>
    public void Reset() => controller.Reset();

    /// <summary>Forgets a bot's state, because the bot is gone.</summary>
    /// <param name="botName">The bot that left.</param>
    public void Forget(string botName) => controller.Forget(botName);
}
