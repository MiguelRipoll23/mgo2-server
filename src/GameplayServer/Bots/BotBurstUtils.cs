namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// How a bot's trigger spends its rounds: three at a time a fifth of a second
/// apart, then the pause the recorded hit rate leaves.
/// </summary>
/// <remarks>
/// <b>The working host's bots fire bursts, and this host fired single rounds.</b>
/// <c>game-with-bots-workingserver</c> writes hits on one victim in runs of up to
/// seven with 0.19–0.25 s between them — slot 2 takes eight from t+81.69 to
/// t+83.03, and its 303 gaps have a tenth percentile of 0.232 s — where
/// <c>bots1.pcapng</c> writes one hit per victim every five seconds and never two
/// within three hundred milliseconds. The rate itself is the recorded one: the
/// same <see cref="BotSimulationService.ShotIntervalMilliseconds"/> a hit, spent
/// three rounds at a time, so a burst is followed by three of those intervals.
/// </remarks>
public static class BotBurstUtils
{
    /// <summary>Rounds one burst holds.</summary>
    public const int Rounds = 3;

    /// <summary>
    /// Interval between two rounds of one burst, in milliseconds: the capture's
    /// short gaps sit at 0.19–0.25 s, and a fifth of a second is inside them.
    /// </summary>
    public const int RoundIntervalMilliseconds = 200;

    /// <summary>
    /// Whether a round leaves the barrel this beat, and when the next one may.
    /// </summary>
    /// <remarks>
    /// The rounds left ride in <see cref="BotBodyState.BurstRemaining"/> and the
    /// next time the trigger may be pulled in
    /// <see cref="BotBodyState.NextShotMilliseconds"/>, so a bot that loses its
    /// enemy drops the rest of the burst and one that keeps it fires again two
    /// hundred milliseconds later.
    /// </remarks>
    /// <param name="body">The body whose trigger this is.</param>
    /// <param name="firing">Whether it has an enemy in range this beat.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns><c>true</c> when a round is fired now.</returns>
    public static bool Ready(BotBodyState body, bool firing, long now)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (!firing)
        {
            body.BurstRemaining = 0;
            return false;
        }

        if (now < body.NextShotMilliseconds)
        {
            return false;
        }

        body.BurstRemaining = body.BurstRemaining > 0 ? body.BurstRemaining - 1 : Rounds - 1;
        body.NextShotMilliseconds = now
            + (body.BurstRemaining > 0
                ? RoundIntervalMilliseconds
                : Rounds * BotSimulationService.ShotIntervalMilliseconds);
        return true;
    }
}
