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
    /// <summary>
    /// Rounds one burst holds, drawn in the shares <c>working-server-v2</c> writes them:
    /// of its 173 runs of hits on one victim 134 are a single hit, 29 are two and 8 are
    /// three, so a bot mostly fires once and the old fixed three was a bot that never did.
    /// </summary>
    /// <returns>One, two or three.</returns>
    public static int RollRounds()
    {
        var roll = Random.Shared.NextDouble();
        return roll < 0.78 ? 1 : roll < 0.95 ? 2 : 3;
    }

    /// <summary>
    /// Interval between two rounds of one burst, in milliseconds: the capture's
    /// short gaps sit at 0.19–0.25 s, and a fifth of a second is inside them.
    /// </summary>
    public const int RoundIntervalMilliseconds = 200;

    /// <summary>
    /// Rounds a magazine holds: the <c>0x0F</c> the weapon pair starts at in
    /// <c>shooting-and-reloading.pcapng</c> and returns to after every reload.
    /// </summary>
    public const int MagazineRounds = 0x0f;

    /// <summary>
    /// How long a reload runs, in milliseconds.
    /// </summary>
    /// <remarks>
    /// <b>Measured on <c>.pcapngs/actions/shooting-and-reloading.pcapng</c>.</b>
    /// The nine-byte <c>0x0B</c> runs t+19.88 to 23.34, t+25.48 to 28.94 and
    /// t+41.81 to 45.27 — 3.43 to 3.47 s — with the weapon pair's ammo at
    /// <c>00</c> at the start and <c>0F</c> at the end.
    /// </remarks>
    public const int ReloadMilliseconds = 3_460;

    /// <summary>
    /// How long the <c>0x48</c> recovery beat holds, after a reload and after an
    /// engagement's last firing beat.
    /// </summary>
    /// <remarks>
    /// <c>shooting-and-reloading</c> writes the three-byte <c>08 FF 00</c> for six
    /// records, 0.17 s, at t+35.71 to 35.87 after one firing run;
    /// <c>stun-by-throwing-to-ground</c> slot 6 writes it from t+46.22, standing,
    /// the beat its trigger stops. Other runs go straight back to firing, so this
    /// is the beat behind an attack and not a second reload.
    /// </remarks>
    public const int RecoverMilliseconds = 170;

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

        if (body.BurstRemaining > 0)
        {
            body.BurstRemaining--;
        }
        else
        {
            body.BurstSize = RollRounds();
            body.BurstRemaining = body.BurstSize - 1;
        }

        body.NextShotMilliseconds = now
            + (body.BurstRemaining > 0
                ? RoundIntervalMilliseconds
                : body.BurstSize * BotSimulationService.ShotIntervalMilliseconds);
        return true;
    }
}
