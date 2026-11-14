namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// A bot's reload cycle: the magazine it spends, the pause that refills it and
/// the one recovery beat behind it.
/// </summary>
/// <remarks>
/// <b>Measured on <c>.pcapngs/actions/shooting-and-reloading.pcapng</c>.</b>
/// Slot 8's magazine falls <c>0F → 0E → … → 00</c> as the trigger spends it, the
/// nine-byte <c>0x0B</c> runs for 3.43 to 3.47 s with the weapon pair on
/// <c>00</c>, and the pair reads <c>0F</c> on the beat the reload ends. One run
/// closes on the three-byte <c>08 FF 00</c>, six records over 0.17 s, which is
/// the recovery beat <see cref="BotBurstUtils.RecoverMilliseconds"/> holds.
/// The bodies the cycle writes are <see cref="BotStateBodiesUtils"/>'s.
/// </remarks>
public static class BotReloadService
{
    /// <summary>
    /// Starts a reload when the magazine is spent, and holds the trigger through
    /// the reload and the recovery beat that follows it.
    /// </summary>
    /// <param name="body">The bot taking its tick.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns><c>true</c> when the bot stands and pulls no trigger this tick.</returns>
    public static bool ShouldHoldFire(BotBodyState body, long now)
    {
        ArgumentNullException.ThrowIfNull(body);

        // The last round's beat is the capture's `0F -> 00`; the reload opens on
        // the tick behind it, as its `0x0B` run does.
        if (body.MagazineRounds <= 0 && body.ReloadUntilMilliseconds == 0)
        {
            body.ReloadUntilMilliseconds = now + BotBurstUtils.ReloadMilliseconds;
        }

        if (body.ReloadUntilMilliseconds != 0)
        {
            if (now < body.ReloadUntilMilliseconds)
            {
                Hold(body);
                return true;
            }

            body.ReloadUntilMilliseconds = 0;
            body.MagazineRounds = BotBurstUtils.MagazineRounds;
            body.RecoverUntilMilliseconds = now + BotBurstUtils.RecoverMilliseconds;
            body.NextShotMilliseconds = Math.Max(
                body.NextShotMilliseconds,
                body.RecoverUntilMilliseconds);
        }

        if (body.RecoverUntilMilliseconds != 0)
        {
            if (now < body.RecoverUntilMilliseconds)
            {
                Hold(body);
                return true;
            }

            body.RecoverUntilMilliseconds = 0;
        }

        return false;
    }

    /// <summary>Spends the round a fire beat fired.</summary>
    /// <param name="body">The bot whose trigger was pulled.</param>
    public static void SpendRound(BotBodyState body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.ShotThisBeat && body.MagazineRounds > 0)
        {
            body.MagazineRounds--;
        }
    }

    /// <summary>
    /// The pause both halves of the cycle take: no target, no trigger, no route.
    /// </summary>
    /// <param name="body">The body standing still.</param>
    private static void Hold(BotBodyState body)
    {
        body.Moving = false;
        body.TargetSlot = BotBodyState.NoTarget;
        body.ShotThisBeat = false;
        body.BurstRemaining = 0;
        body.WasFiring = false;
    }
}
