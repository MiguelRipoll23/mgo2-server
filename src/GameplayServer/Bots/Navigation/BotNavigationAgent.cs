namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The agent a bot asks for a route, which is the one place navigation is
/// requested from.
/// </summary>
/// <remarks>
/// <para>
/// <b>The agent is where a movement decision becomes a request and nothing
/// more.</b> It does not know the map, the mode or the role: it hands the request
/// to the provider and returns what the provider says, so a bot that has nowhere
/// to go is told so by the same object that will route it once data exists.
/// </para>
/// <para>
/// It also keeps the bot's own navigation state — the last result it was given —
/// so a later tick can see that a route was refused rather than re-asking into a
/// void. The state is a result and not a position: no coordinate is remembered
/// that a source did not state.
/// </para>
/// </remarks>
/// <param name="provider">The provider routes are asked of.</param>
public sealed class BotNavigationAgent(IBotNavigationProvider provider)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, BotNavigationResult> lastResults = [];

    /// <summary>
    /// Asks for a route on a bot's behalf and keeps the answer.
    /// </summary>
    /// <param name="botName">The bot the request is for.</param>
    /// <param name="request">Where it starts and ends.</param>
    /// <returns>What the provider answered.</returns>
    public BotNavigationResult Navigate(string botName, BotNavigationRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(botName);
        ArgumentNullException.ThrowIfNull(request);

        var result = provider.FindPath(request);
        lock (gate)
        {
            lastResults[botName] = result;
        }

        return result;
    }

    /// <summary>
    /// The last answer a bot was given.
    /// </summary>
    /// <param name="botName">The bot the answer was for.</param>
    /// <returns>The answer, or <c>null</c> while the bot has asked nothing.</returns>
    public BotNavigationResult? LastResult(string botName)
    {
        lock (gate)
        {
            return lastResults.TryGetValue(botName, out var result) ? result : null;
        }
    }

    /// <summary>Drops every bot's navigation state, because the room has emptied.</summary>
    public void Reset()
    {
        lock (gate)
        {
            lastResults.Clear();
        }
    }

    /// <summary>Forgets a bot's navigation state, because the bot is gone.</summary>
    /// <param name="botName">The bot that left.</param>
    public void Forget(string botName)
    {
        lock (gate)
        {
            lastResults.Remove(botName);
        }
    }
}
