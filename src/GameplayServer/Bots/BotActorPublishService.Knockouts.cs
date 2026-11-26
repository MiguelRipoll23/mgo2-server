namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The knock-out half of the actor writer: whether a slot is in the stun or sleep
/// a knock-out record left, for a player and a bot alike.
/// </summary>
/// <remarks>
/// It is split from <c>BotActorPublishService.Players.cs</c> for the size rule.
/// The state it reads is already kept there and in <c>BotSimulationService</c>;
/// this is the one question both halves answer the same way.
/// </remarks>
public sealed partial class BotActorPublishService
{
    /// <summary>
    /// Whether a slot is knocked out rather than merely down: a player in the
    /// stun or sleep the knock-out records set, or a bot whose stamina bar is
    /// empty and whose body has not got up yet.
    /// </summary>
    /// <remarks>
    /// <b>A corpse is not knocked out.</b> The two are the different halves of a
    /// down: the health record puts a body at zero and the stun family spends its
    /// stamina, and the Sneaking dog-tag hold is the second one. A caller that
    /// treated either as the other would count a hold on a dead body.
    /// </remarks>
    /// <param name="slot">Slot to test.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns><c>true</c> when the slot is in a knock-out.</returns>
    public bool IsActorKnockedOut(byte slot, long now)
    {
        if (IsPlayerStunned(slot, now))
        {
            return true;
        }

        var map = roundState.RoundSettings?.Map ?? matchSettings.Map;
        return bots.Bots.Any(bot =>
            bot.SessionSlot == slot
            && simulation.BodyOf(bot, map) is { } body
            && (body.Sleeping || body.StunnedUntilMilliseconds > now));
    }
}
